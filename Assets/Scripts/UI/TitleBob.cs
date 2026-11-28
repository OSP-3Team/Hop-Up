using UnityEngine;

namespace Project.UI
{
    /// <summary>타이틀을 위아래로 살짝 띄우고 크기를 미세하게 바꿔 둥실거리게 한다.</summary>
    public class TitleBob : MonoBehaviour
    {
        [SerializeField, Tooltip("위아래로 움직이는 거리(픽셀)")] private float height = 12f;
        [SerializeField, Tooltip("한 번 오르내리는 시간(초)")] private float period = 3f;
        [SerializeField, Tooltip("크기 변화량 (0.03 = 3%)")] private float scaleAmount = 0.03f;

        private RectTransform rect;
        private Vector2 startPos;

        private void Awake()
        {
            rect = (RectTransform)transform;
            startPos = rect.anchoredPosition;
        }

        private void Update()
        {
            float s = Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / period);
            rect.anchoredPosition = startPos + new Vector2(0f, s * height);
            rect.localScale = Vector3.one * (1f + s * scaleAmount);
        }
    }
}