Shader "Beyond Colors/Vision Glasses"
{
    Properties
    {
        [Toggle] _PreviewGlasses ("Preview Glasses", Float) = 0
        _GlassesSize ("Glasses Size", Range(0.55, 1.0)) = 0.98
        _FrameColor ("Frame Color", Color) = (0.035, 0.035, 0.055, 1)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "VisionGlasses"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float _PreviewGlasses;
            float _GlassesSize;
            float4 _FrameColor;
            CBUFFER_END

            float _BeyondColorsVisionGlasses;

            float3 SimulateDeuteranopia(float3 color)
            {
                // Machado, Oliveira and Fernandes (2009), full severity.
                return saturate(float3(
                    dot(color, float3(0.367322, 0.860646, -0.227968)),
                    dot(color, float3(0.280085, 0.672501,  0.047413)),
                    dot(color, float3(-0.011820, 0.042940, 0.968881))
                ));
            }

            // Scan-line bounds of the traced outer rim (xy) and lens opening (zw).
            // Sampling in square-root height space retains the top and bottom curves.
            static const int lastRow = 24;
            static const float4 profileRows[25] =
            {
                float4(288.9691, 288.9691, 289.9187, 289.9187),
                float4(238.0976, 346.5055, 244.6911, 342.6567),
                float4(194.0345, 412.3028, 205.0502, 404.6010),
                float4(156.2877, 488.8758, 170.4894, 471.6364),
                float4(124.6720, 573.7316, 140.5238, 518.8574),
                float4(99.9639, 644.7271, 118.3671, 552.9277),
                float4(81.8350, 677.2747, 102.7747, 576.8975),
                float4(66.8354, 679.3442, 89.8878, 592.2914),
                float4(53.8487, 675.1532, 78.4228, 599.9925),
                float4(41.2551, 644.3459, 69.7915, 600.7499),
                float4(37.4578, 636.9528, 71.7273, 599.3463),
                float4(41.1775, 633.3876, 79.7570, 597.4434),
                float4(48.6042, 629.6669, 89.6089, 594.6191),
                float4(56.6570, 624.9552, 98.6798, 590.6574),
                float4(66.3525, 618.8308, 108.2920, 585.6256),
                float4(78.5528, 610.9740, 119.8383, 579.3987),
                float4(94.1439, 601.6129, 134.5011, 571.8161),
                float4(114.3471, 590.8230, 153.5238, 562.2571),
                float4(141.6320, 577.3957, 178.0747, 550.6082),
                float4(178.8970, 561.1545, 209.8355, 536.7385),
                float4(222.4380, 541.8993, 249.1706, 520.5032),
                float4(270.0662, 519.4009, 295.6061, 501.7421),
                float4(320.6352, 493.3997, 348.6119, 480.2718),
                float4(381.6798, 463.5842, 397.4651, 455.8939),
                float4(429.3484, 429.3484, 428.3827, 428.3827)
            };

            float2 ProfileBounds(float y, float2 yLimits, bool lens)
            {
                float t = saturate((y - yLimits.x) / (yLimits.y - yLimits.x));
                float s = 0.5 * sqrt(2.0 * min(t, 1.0 - t));
                float sampleRow = (t <= 0.5 ? s : 1.0 - s) * lastRow;
                int i = min((int)sampleRow, lastRow - 1);
                float f = sampleRow - i;

                // Reflect the guard samples to preserve smooth end tangents.
                float4 b = profileRows[i];
                float4 c = profileRows[i + 1];
                float4 a = i > 0 ? profileRows[max(i - 1, 0)] : 2.0 * b - c;
                float4 d = i < lastRow - 1 ? profileRows[min(i + 2, lastRow)] : 2.0 * c - b;
                float4 bounds = b + 0.5 * f * (c - a + f *
                    (2.0 * a - 5.0 * b + 4.0 * c - d + f * (3.0 * (b - c) + d - a)));
                return lens ? bounds.zw : bounds.xy;
            }

            float ProfileDistance(float2 p, float2 yLimits, bool lens)
            {
                float2 bounds = ProfileBounds(p.y, yLimits, lens);
                float dX = max(bounds.x - p.x, p.x - bounds.y);
                float dY = max(yLimits.x - p.y, p.y - yLimits.y);
                return max(dX, dY);
            }

            float InsideEdge(float distance)
            {
                float feather = max(0.5 * fwidth(distance), 0.001);
                return 1.0 - smoothstep(-feather, feather, distance);
            }

            float4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float4 sceneColor = SAMPLE_TEXTURE2D_X_LOD(
                    _BlitTexture, sampler_LinearClamp, input.texcoord, 0);

                if (max(_PreviewGlasses, _BeyondColorsVisionGlasses) < 0.5)
                    return sceneColor;

                float2 screenSize = GetScaledScreenParams().xy;
                float2 position = input.positionCS.xy;
                #if !UNITY_UV_STARTS_AT_TOP
                    position.y = screenSize.y - position.y;
                #endif
                float fittedWidth = min(screenSize.x, screenSize.y * (16.0 / 9.0));
                float2 p = (position - 0.5 * screenSize) * (1370.0 / (fittedWidth * _GlassesSize));
                p = float2(abs(p.x), 395.0 - p.y);

                float outerDistance = ProfileDistance(p, float2(164.3008, 634.3308), false);
                float lensDistance = ProfileDistance(p, float2(197.8853, 603.6558), true);

                // A raised bridge with a curved lower edge joins the rims above the nose.
                float xSquared = p.x * p.x;
                float bridgeTop = 199.0 - 0.00012 * xSquared;
                float bridgeBottom = 261.0 + 0.0043 * xSquared;
                float bridgeDistance = max(p.x - 122.0, max(bridgeTop - p.y, p.y - bridgeBottom));
                float rimDistance = min(outerDistance, bridgeDistance);
                float inside = InsideEdge(lensDistance);
                float rim = saturate(InsideEdge(rimDistance) - inside);

                float topLight = 1.0 - smoothstep(172.0, 365.0, p.y);
                float outerBevel = saturate(1.0 + rimDistance / 8.0);
                float lensBevel = 1.0 - saturate(lensDistance / 6.0);
                float3 frameColor = _FrameColor.rgb * (0.45 + 0.35 * topLight
                    + 0.70 * outerBevel * topLight + 0.17 * lensBevel);

                // Small metal hinge details near the outer temples.
                float2 hinge = abs((p - float2(657.0, 247.0)) / float2(11.0, 18.0));
                float hingeCoverage = InsideEdge(length(hinge) - 1.0);
                float metalLight = 0.27 + 0.55 * saturate((255.0 - p.y) / 24.0);
                frameColor = lerp(frameColor, float3(0.35, 0.20, 0.095) * metalLight, hingeCoverage);

                float3 viewColor = lerp(sceneColor.rgb, SimulateDeuteranopia(sceneColor.rgb), inside);
                viewColor = lerp(viewColor, frameColor, rim * _FrameColor.a);
                return float4(viewColor, sceneColor.a);
            }
            ENDHLSL
        }
    }
}
