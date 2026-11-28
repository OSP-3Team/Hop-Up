using System.Collections;
using TMPro;
using UnityEngine;

namespace Project.UI
{
    /// <summary>화면 아래에 짧은 안내 문구를 잠깐 띄웠다가 사라지게 한다.</summary>
    public class MenuToast : MonoBehaviour
    {
        [SerializeField] private TMP_Text text;
        [SerializeField] private float showTime = 2f;
        [SerializeField] private float fadeTime = 0.4f;

        private Coroutine routine;

        private void Awake()
        {
            if (text == null) text = GetComponent<TMP_Text>();
            text.alpha = 0f;
        }

        public void Show(string message)
        {
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(ShowRoutine(message));
        }

        private IEnumerator ShowRoutine(string message)
        {
            text.text = message;
            text.alpha = 1f;
            yield return new WaitForSecondsRealtime(showTime);

            float t = 0f;
            while (t < fadeTime)
            {
                t += Time.unscaledDeltaTime;
                text.alpha = 1f - t / fadeTime;
                yield return null;
            }
            text.alpha = 0f;
            routine = null;
        }
    }
}