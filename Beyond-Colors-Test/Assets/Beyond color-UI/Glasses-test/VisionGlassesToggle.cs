using UnityEngine;

namespace BeyondColors
{
    [DisallowMultipleComponent]
    public sealed class VisionGlassesToggle : MonoBehaviour
    {
        private static readonly int EffectEnabled = Shader.PropertyToID("_BeyondColorsVisionGlasses");

        private void OnEnable() => SetGlasses(false);
        private void OnDisable() => SetGlasses(false);

        public void SetGlasses(bool equipped)
        {
            Shader.SetGlobalFloat(EffectEnabled, equipped ? 1f : 0f);
        }
    }
}
