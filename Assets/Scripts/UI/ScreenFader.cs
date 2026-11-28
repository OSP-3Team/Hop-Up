using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.UI
{
    /// <summary>
    /// 화면을 어둡게 한 뒤 씬을 불러오고, 새 씬에서 다시 밝게 한다.
    /// 씬이 바뀌어도 사라지지 않도록 DontDestroyOnLoad로 유지한다.
    /// </summary>
    public class ScreenFader : MonoBehaviour
    {
        public static ScreenFader Instance { get; private set; }

        [SerializeField] private CanvasGroup fadeGroup;
        [SerializeField] private float fadeOutTime = 0.5f;
        [SerializeField] private float fadeInTime = 0.7f;

        public bool IsBusy { get; private set; }

        private void Awake()
        {
            // 메인 메뉴로 다시 돌아왔을 때 하나만 남긴다
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SetAlpha(0f);
        }

        public void FadeOutAndLoad(string sceneName)
        {
            if (IsBusy) return;
            StartCoroutine(LoadRoutine(sceneName));
        }

        private IEnumerator LoadRoutine(string sceneName)
        {
            IsBusy = true;
            fadeGroup.blocksRaycasts = true; // 전환 중 클릭 막기

            yield return Fade(0f, 1f, fadeOutTime);

            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            while (!op.isDone) yield return null;
            yield return null; // 새 씬이 한 프레임 준비될 때까지

            yield return Fade(1f, 0f, fadeInTime);

            fadeGroup.blocksRaycasts = false;
            IsBusy = false;
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(from, to, t / duration));
                yield return null;
            }
            SetAlpha(to);
        }

        private void SetAlpha(float a)
        {
            if (fadeGroup != null) fadeGroup.alpha = a;
        }
    }
}