using System;
using System.Collections;
using System.Collections.Generic;
using Michsky.UI.Dark;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeyondColors
{
    public sealed class MainMenu : MonoBehaviour
    {
        private const string LanguageKey = "BeyondColors.Menu.Language";

        internal bool IsChinese => PlayerPrefs.GetInt(LanguageKey) == 1;
        internal event Action LanguageChanged;

        [Serializable]
        public sealed class Translation
        {
            public TextMeshProUGUI text;
            public string english;
            public string chinese;
        }

        [Serializable]
        public sealed class ButtonTranslation
        {
            public ButtonManager button;
            public string english;
            public string chinese;
        }

        [SerializeField] private string firstScene = "Assets/Scence-1.unity";
        [SerializeField] private Font chineseFont;
        [SerializeField] private HorizontalSelector languageSelector;
        [SerializeField] private ModalWindowManager exitWindow;
        [SerializeField] private Translation[] labels = Array.Empty<Translation>();
        [SerializeField] private ButtonTranslation[] buttons = Array.Empty<ButtonTranslation>();
        private string exitTitle;
        private string exitDescription;
        private TMP_FontAsset fallbackFont;
        private readonly List<TMP_FontAsset> fontsUsingFallback = new();
        private readonly List<Translation> confirmationLabels = new();
        private readonly List<ButtonTranslation> confirmationButtons = new();
        private bool loading;

        private void Awake()
        {
            fallbackFont = TMP_FontAsset.CreateFontAsset(chineseFont);
            exitTitle = exitWindow.title;
            exitDescription = exitWindow.description;
            FindExitConfirmations();
            InitializeVolumeDefaults();
            languageSelector.defaultIndex = Mathf.Clamp(PlayerPrefs.GetInt(LanguageKey), 0, 1);
            ApplyLanguage(languageSelector.defaultIndex);
        }

        public void SetLanguage(int index)
        {
            index = Mathf.Clamp(index, 0, 1);
            PlayerPrefs.SetInt(LanguageKey, index);
            PlayerPrefs.Save();
            ApplyLanguage(index);
            LanguageChanged?.Invoke();
        }

        private void ApplyLanguage(int index)
        {
            bool chinese = index == 1;
            foreach (var label in EnumerateLabels())
                SetText(label.text, chinese ? label.chinese : label.english);

            foreach (var entry in EnumerateButtons())
            {
                entry.button.buttonText = chinese ? entry.chinese : entry.english;
                SetText(entry.button.normalText, entry.button.buttonText);
                SetText(entry.button.highlightedText, entry.button.buttonText);
            }

            AddFallback(languageSelector.label);
            AddFallback(languageSelector.labelHelper);
            exitWindow.title = chinese ? "退出" : exitTitle;
            exitWindow.description = chinese ? "确定要退出游戏吗？" : exitDescription;
            SetText(exitWindow.titleObject, exitWindow.title);
            SetText(exitWindow.descriptionObject, exitWindow.description);
        }

        internal void SetText(TMP_Text label, string value)
        {
            if (label == null)
                return;

            AddFallback(label);
            label.text = value;
        }

        private void AddFallback(TMP_Text label)
        {
            if (fallbackFont == null || label == null || label.font == null)
                return;

            var font = label.font;
            font.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
            if (font.fallbackFontAssetTable.Contains(fallbackFont))
                return;

            font.fallbackFontAssetTable.Add(fallbackFont);
            fontsUsingFallback.Add(font);
        }

        private IEnumerable<Translation> EnumerateLabels()
        {
            foreach (var label in labels) yield return label;
            foreach (var label in confirmationLabels) yield return label;
        }

        private IEnumerable<ButtonTranslation> EnumerateButtons()
        {
            foreach (var button in buttons) yield return button;
            foreach (var button in confirmationButtons) yield return button;
        }

        private void FindExitConfirmations()
        {
            foreach (var button in exitWindow.GetComponentsInChildren<ButtonManager>(true))
            {
                string chinese = TranslateConfirmation(button.buttonText);
                if (chinese != null)
                    confirmationButtons.Add(new ButtonTranslation { button = button, english = button.buttonText, chinese = chinese });
            }

            foreach (var text in exitWindow.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                string chinese = TranslateConfirmation(text.text);
                if (chinese != null)
                    confirmationLabels.Add(new Translation { text = text, english = text.text, chinese = chinese });
            }
        }

        private static string TranslateConfirmation(string value) => value switch
        {
            "YES" => "确认",
            "NO" or "CANCEL" => "取消",
            _ => null
        };

        private void InitializeVolumeDefaults()
        {
            var quality = GetComponentInChildren<QualityManager>(true);
            if (quality == null)
                return;

            RememberVolumeDefault(quality.masterSlider);
            RememberVolumeDefault(quality.musicSlider);
            RememberVolumeDefault(quality.sfxSlider);
        }

        private static void RememberVolumeDefault(SliderManager slider)
        {
            if (slider == null || slider.mainSlider == null)
                return;

            string key = slider.sliderTag + "Slider";
            if (!PlayerPrefs.HasKey(key))
                PlayerPrefs.SetFloat(key, slider.mainSlider.value);
        }

        private void OnDestroy()
        {
            if (fallbackFont == null)
                return;

            foreach (var font in fontsUsingFallback)
                if (font != null)
                    font.fallbackFontAssetTable.Remove(fallbackFont);

            Destroy(fallbackFont);
        }

        public void Play()
        {
            if (!loading)
                StartCoroutine(LoadFirstScene());
        }

        private IEnumerator LoadFirstScene()
        {
            loading = true;
            yield return new WaitForSecondsRealtime(0.15f);
            SceneManager.LoadSceneAsync(firstScene, LoadSceneMode.Single);
        }
    }
}
