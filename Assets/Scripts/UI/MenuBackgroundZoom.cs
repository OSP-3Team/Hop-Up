using UnityEngine;

namespace Project.UI
{
    /// <summary>메인 메뉴 배경을 천천히 확대·축소해 살아 있는 느낌을 준다.</summary>
    public class MenuBackgroundZoom : MonoBehaviour
    {
        [SerializeField, Tooltip("가장 클 때 배율")] private float maxScale = 1.08f;
        [SerializeField, Tooltip("확대→축소 한 번 왕복에 걸리는 시간(초)")] private float period = 20f;
        [SerializeField, Tooltip("좌우로 살짝 흘러가는 거리(픽셀)")] private float driftX = 25f;

        private RectTransform rect;
        private Vector2 startPos;

        private void Awake()
        {
            rect = (RectTransform)transform;
            startPos = rect.anchoredPosition;
        }

        private void Update()
        {
            // 0 → 1 → 0을 부드럽게 반복
            float t = (1f - Mathf.Cos(Time.unscaledTime * Mathf.PI * 2f / period)) * 0.5f;

            rect.localScale = Vector3.one * Mathf.Lerp(1f, maxScale, t);
            // 원래 크기일 때는 제자리, 확대될수록 옆으로 흘러감 (가장자리 빈틈 방지)
            rect.anchoredPosition = startPos + new Vector2(Mathf.Lerp(0f, driftX, t), 0f);
        }
    }
}