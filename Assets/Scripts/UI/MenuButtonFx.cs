using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.UI
{
    /// <summary>메뉴 버튼: 마우스를 올리면 부드럽게 커지고 글자색이 바뀐다.</summary>
    public class MenuButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private float hoverScale = 1.08f;
        [SerializeField, Tooltip("커지고 작아지는 속도")] private float speed = 12f;

        [SerializeField, Tooltip("비우면 자식에서 자동으로 찾음")] private TMP_Text label;
        [SerializeField] private Color normalTextColor = Color.white;
        [SerializeField] private Color hoverTextColor = new Color(1f, 0.85f, 0.3f);
        [SerializeField] private Color disabledTextColor = new Color(1f, 1f, 1f, 0.35f);

        private Button button;
        private bool hovered;

        private void Awake()
        {
            button = GetComponent<Button>();
            if (label == null) label = GetComponentInChildren<TMP_Text>();
        }

        private void OnDisable()
        {
            hovered = false;
            transform.localScale = Vector3.one;
        }

        public void OnPointerEnter(PointerEventData eventData) => hovered = true;
        public void OnPointerExit(PointerEventData eventData) => hovered = false;

        private void Update()
        {
            bool interactable = button == null || button.interactable;
            bool active = hovered && interactable;

            // 목표 크기로 부드럽게 다가감 (일시정지 중에도 동작하도록 unscaled 시간 사용)
            float target = active ? hoverScale : 1f;
            float s = Mathf.Lerp(transform.localScale.x, target, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
            transform.localScale = Vector3.one * s;

            if (label != null)
                label.color = !interactable ? disabledTextColor : (active ? hoverTextColor : normalTextColor);
        }
    }
}