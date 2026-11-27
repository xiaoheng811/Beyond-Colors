using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BeyondColors
{
    public sealed class ChapterOneUI : MonoBehaviour
    {
        [SerializeField] private string mainMenuScene = "Assets/Beyond color-UI/Scenes/UI Beyond Color.unity";
        [SerializeField] private GameObject chapterCard = null;
        [SerializeField] private GameObject hud = null;
        [SerializeField] private GameObject pauseCard = null;
        [SerializeField] private GameObject darkPanels = null;
        [SerializeField] private CanvasGroup help = null;
        [SerializeField] private CanvasGroup settings = null;
        [SerializeField] private TMP_Text clueCounter = null;
        [SerializeField] private TMP_Text visionStatus = null;
        [SerializeField] private GameObject inspectPrompt = null;
        [SerializeField, Min(1)] private int totalClues = 3;
        [SerializeField, Min(0)] private int initialClues = 0;
        [SerializeField] private bool showInspectPromptOnStart = true;
        [SerializeField] private string cluesFormat = "Clues  {0}/{1}";
        [SerializeField] private string visionOffText = "Vision OFF";
        [SerializeField] private string visionOnText = "Vision ON";
        [Header("Chinese UI Text")]
        [SerializeField] private string chineseCluesFormat = "线索  {0}/{1}";
        [SerializeField] private string chineseVisionOffText = "眼镜未佩戴";
        [SerializeField] private string chineseVisionOnText = "眼镜已佩戴";
        [SerializeField] private LocalizedLabel[] chapterLabels =
        {
            new("Chapter Number", "第一章"),
            new("Chapter Name", "美术教室"),
            new("Description", "你被困在学校的美术楼里。探索空荡的教室和令人不安的角落，利用模拟红绿色觉差异的眼镜找出隐藏线索，找到离开的路。"),
            new("Start Chapter", "开始本章"),
            new("Back", "返回")
        };
        [SerializeField] private LocalizedLabel[] hudLabels =
        {
            new("Label", "目标"),
            new("Objective Text", "找到 3 条线索，解锁教室的门"),
            new("Inspect Text", "检查")
        };
        [SerializeField] private LocalizedLabel[] pauseLabels =
        {
            new("Pause Title", "已暂停"),
            new("Resume", "继续游戏"),
            new("Help and Guide", "操作指南"),
            new("Settings", "设置")
        };
        [SerializeField] private UnityEvent onInspect = new();
        [SerializeField] private UnityEvent<bool> onVisionChanged = new();
        [SerializeField] private Behaviour[] gameplayControls = Array.Empty<Behaviour>();

        private enum Page { Intro, Gameplay, Pause, Help, Settings }

        [Serializable]
        private sealed class LocalizedLabel
        {
            public string objectName;
            [TextArea] public string chinese;
            [NonSerialized] public TMP_Text target;
            [NonSerialized] public string english;

            public LocalizedLabel(string objectName, string chinese)
            {
                this.objectName = objectName;
                this.chinese = chinese;
            }
        }

        private Page page;
        private float previousTimeScale;
        private bool ownsPause;
        private bool visionOn;
        private bool inspectAvailable;
        private bool leaving;
        private bool[] controlStates;
        private MainMenu languageMenu;
        private bool chinese;
        private int foundClues;

        private void Awake()
        {
            controlStates = new bool[gameplayControls.Length];
            for (int i = 0; i < gameplayControls.Length; i++)
                if (gameplayControls[i] != null)
                    controlStates[i] = gameplayControls[i].enabled;

            PreparePage(help);
            PreparePage(settings);
            if (inspectPrompt == null && hud != null)
            {
                var prompt = hud.transform.Find("Inspect Prompt");
                if (prompt != null)
                    inspectPrompt = prompt.gameObject;
            }

            SetInspectAvailable(showInspectPromptOnStart);
            SetClueCount(initialClues);
            visionStatus.text = visionOffText;
            Show(Page.Intro);
        }

        private void Start()
        {
            languageMenu = GetComponentInParent<MainMenu>();
            if (languageMenu == null)
                return;

            CacheLabels(chapterCard, chapterLabels);
            CacheLabels(hud, hudLabels);
            CacheLabels(pauseCard, pauseLabels);
            languageMenu.LanguageChanged += RefreshLanguage;
            RefreshLanguage();
        }

        private static void CacheLabels(GameObject root, LocalizedLabel[] labels)
        {
            if (root == null)
                return;

            var children = root.GetComponentsInChildren<Transform>(true);
            foreach (var entry in labels)
            {
                var item = children.FirstOrDefault(child => child.name == entry.objectName);
                entry.target = item != null ? item.GetComponentInChildren<TMP_Text>(true) : null;
                if (entry.target != null)
                    entry.english = entry.target.text;
            }
        }

        private void RefreshLanguage()
        {
            chinese = languageMenu.IsChinese;
            ApplyLabels(chapterLabels);
            ApplyLabels(hudLabels);
            ApplyLabels(pauseLabels);
            SetClueCount(foundClues);
            RefreshVisionText();
        }

        private void ApplyLabels(LocalizedLabel[] labels)
        {
            foreach (var entry in labels)
                languageMenu.SetText(entry.target, chinese ? entry.chinese : entry.english);
        }

        private void Update()
        {
            var keys = Keyboard.current;
            if (keys == null || leaving)
                return;

            if (keys.escapeKey.wasPressedThisFrame)
            {
                switch (page)
                {
                    case Page.Gameplay: Pause(); break;
                    case Page.Pause: Resume(); break;
                    case Page.Help:
                    case Page.Settings: ReturnToPause(); break;
                }
                return;
            }

            if (page != Page.Gameplay)
                return;

            if (keys.yKey.wasPressedThisFrame)
                SetVision(!visionOn);
            if (inspectAvailable && keys.eKey.wasPressedThisFrame)
                onInspect.Invoke();
        }

        private void PreparePage(CanvasGroup group)
        {
            if (group.TryGetComponent<Animator>(out var pageAnimator))
                pageAnimator.enabled = false;

            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
            foreach (var animator in group.GetComponentsInChildren<Animator>(true))
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;

            BindClose(group, "Content/Close Button");
            BindClose(group, "Shortcut List/Keyboard/Close");
        }

        private void BindClose(CanvasGroup group, string path)
        {
            if (group.transform.Find(path) is Transform child && child.TryGetComponent<Button>(out var button))
            {
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(ReturnToPause);
            }
        }

        public void StartChapter()
        {
            if (page == Page.Intro && !leaving)
                Show(Page.Gameplay);
        }

        public void Pause()
        {
            if (page == Page.Gameplay)
                Show(Page.Pause);
        }

        public void Resume()
        {
            if (page == Page.Pause)
                Show(Page.Gameplay);
        }

        public void ShowHelp()
        {
            if (page == Page.Pause)
                Show(Page.Help);
        }

        public void ShowSettings()
        {
            if (page == Page.Pause)
                Show(Page.Settings);
        }

        public void ReturnToPause()
        {
            if (page == Page.Help || page == Page.Settings)
                Show(Page.Pause);
        }

        public void BackToMenu()
        {
            if (leaving || !Application.CanStreamedLevelBeLoaded(mainMenuScene))
                return;

            leaving = true;
            RestoreTime();
            SceneManager.LoadSceneAsync(mainMenuScene, LoadSceneMode.Single);
        }

        private void Show(Page next)
        {
            page = next;
            bool playing = next == Page.Gameplay;
            if (playing)
                RestoreTime();
            else if (!ownsPause)
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                ownsPause = true;
            }

            chapterCard.SetActive(next == Page.Intro);
            hud.SetActive(playing);
            pauseCard.SetActive(next == Page.Pause);
            help.gameObject.SetActive(next == Page.Help);
            settings.gameObject.SetActive(next == Page.Settings);
            darkPanels.SetActive(next == Page.Help || next == Page.Settings);

            for (int i = 0; i < gameplayControls.Length; i++)
                if (gameplayControls[i] != null)
                    gameplayControls[i].enabled = playing && controlStates[i];

            if (!playing)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        public void SetClueCount(int found)
        {
            foundClues = Mathf.Clamp(found, 0, totalClues);
            SetText(clueCounter, string.Format(chinese ? chineseCluesFormat : cluesFormat, foundClues, totalClues));
        }

        public void SetInspectAvailable(bool available)
        {
            inspectAvailable = available;
            if (inspectPrompt != null)
                inspectPrompt.SetActive(available);
        }

        public void SetVision(bool enabled)
        {
            if (visionOn == enabled)
                return;

            visionOn = enabled;
            RefreshVisionText();
            onVisionChanged.Invoke(enabled);
        }

        private void RefreshVisionText()
        {
            var status = visionOn
                ? (chinese ? chineseVisionOnText : visionOnText)
                : (chinese ? chineseVisionOffText : visionOffText);
            SetText(visionStatus, status);
        }

        private void SetText(TMP_Text label, string value)
        {
            if (languageMenu != null)
                languageMenu.SetText(label, value);
            else if (label != null)
                label.text = value;
        }

        private void RestoreTime()
        {
            if (!ownsPause)
                return;

            Time.timeScale = previousTimeScale;
            ownsPause = false;
        }

        private void OnDestroy()
        {
            if (languageMenu != null)
                languageMenu.LanguageChanged -= RefreshLanguage;
            RestoreTime();
        }
    }
}
