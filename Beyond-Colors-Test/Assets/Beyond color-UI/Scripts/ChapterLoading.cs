using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BeyondColors
{
    public sealed class ChapterLoading : MonoBehaviour
    {
        [SerializeField] private string levelScene = "Assets/Beyond color-UI/Scenes/Scence-1.unity";
        [SerializeField, Range(3f, 5f)] private float minimumSeconds = 4f;
        [SerializeField] private GameObject overlay = null;
        [SerializeField] private Image progressFill = null;
        [SerializeField] private TMP_Text percentage = null;
        [Header("Loading Text")]
        [SerializeField] private TMP_Text chapterTitle = null;
        [SerializeField] private TMP_Text loadingText = null;
        [SerializeField] private string chineseChapterTitle = "第一章 / 美术教室";
        [SerializeField] private string chineseLoadingText = "加载中…";

        private bool loading;
        private MainMenu menu;
        private string englishChapterTitle;
        private string englishLoadingText;

        private void Awake()
        {
            menu = GetComponent<MainMenu>();
            if (chapterTitle == null)
                chapterTitle = FindOverlayText("Chapter Title");
            if (loadingText == null)
                loadingText = FindOverlayText("Loading Text");

            englishChapterTitle = chapterTitle != null ? chapterTitle.text : string.Empty;
            englishLoadingText = loadingText != null ? loadingText.text : string.Empty;
        }

        private TMP_Text FindOverlayText(string name)
        {
            if (overlay == null)
                return null;

            var child = overlay.transform.Find("Progress Content/" + name);
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        public void Play()
        {
            if (loading || !Application.CanStreamedLevelBeLoaded(levelScene))
                return;

            loading = true;
            if (menu != null)
            {
                bool chinese = menu.IsChinese;
                menu.SetText(chapterTitle, chinese ? chineseChapterTitle : englishChapterTitle);
                menu.SetText(loadingText, chinese ? chineseLoadingText : englishLoadingText);
            }
            overlay.SetActive(true);
            UpdateProgress(0f);
            StartCoroutine(Load());
        }

        private IEnumerator Load()
        {
            yield return null;
            var operation = SceneManager.LoadSceneAsync(levelScene, LoadSceneMode.Single);
            if (operation == null)
            {
                overlay.SetActive(false);
                loading = false;
                yield break;
            }

            operation.allowSceneActivation = false;
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < minimumSeconds || operation.progress < 0.9f)
            {
                float timeProgress = Mathf.Clamp01((Time.realtimeSinceStartup - start) / minimumSeconds);
                UpdateProgress(Mathf.Min(0.99f, timeProgress, operation.progress / 0.9f));
                yield return null;
            }

            UpdateProgress(1f);
            yield return new WaitForSecondsRealtime(0.08f);
            operation.allowSceneActivation = true;
        }

        private void UpdateProgress(float value)
        {
            var fill = progressFill.rectTransform;
            fill.anchorMax = new Vector2(value, fill.anchorMax.y);
            percentage.text = Mathf.FloorToInt(value * 100f) + "%";
        }
    }
}
