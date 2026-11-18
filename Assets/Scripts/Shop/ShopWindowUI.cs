using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Project.Shop
{
    public class ShopWindowUI : MonoBehaviour
    {
        private static readonly string[] CategoryNames =
            { "발판", "씨앗·재료", "확장", "꾸미기", "판매" };

        [SerializeField, Tooltip("열고 닫을 패널. 이 스크립트가 붙은 오브젝트와 달라야 한다")]
        private GameObject windowRoot;

        [SerializeField, Tooltip("ShopCategory 순서: 발판, 씨앗·재료, 확장, 꾸미기, 판매")]
        private Button[] tabButtons;

        [SerializeField, Tooltip("임시: 현재 카테고리 이름 (3단계에서 목록으로 교체)")]
        private TMP_Text categoryLabel;

        [SerializeField] private Color normalTabColor = new Color(0.25f, 0.25f, 0.28f, 1f);
        [SerializeField] private Color selectedTabColor = new Color(0.95f, 0.75f, 0.2f, 1f);

        private ShopCategory currentCategory = ShopCategory.Platform;

        public bool IsOpen => windowRoot.activeSelf;
        public ShopCategory CurrentCategory => currentCategory;

        // 창이 열리고 닫힐 때 발생 (열렸으면 true)
        public event Action<bool> OnOpenChanged;

        // 카테고리가 바뀔 때 발생
        public event Action<ShopCategory> OnCategoryChanged;

        private void Awake()
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int index = i; // 클릭한 탭 번호를 기억하기 위한 복사
                tabButtons[i].onClick.AddListener(() => SelectCategory((ShopCategory)index));
            }

            windowRoot.SetActive(false);
            SelectCategory(ShopCategory.Platform);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.eKey.wasPressedThisFrame)
                SetOpen(!IsOpen);
        }

        public void SetOpen(bool open)
        {
            if (windowRoot.activeSelf == open) return;

            windowRoot.SetActive(open);
            OnOpenChanged?.Invoke(open);
        }

        public void SelectCategory(ShopCategory category)
        {
            currentCategory = category;
            categoryLabel.text = $"카테고리: {CategoryNames[(int)category]}";
            RefreshTabs();
            OnCategoryChanged?.Invoke(category);
        }

        private void RefreshTabs()
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                var image = tabButtons[i].targetGraphic as Image;
                if (image != null)
                    image.color = (i == (int)currentCategory) ? selectedTabColor : normalTabColor;
            }
        }
    }
}