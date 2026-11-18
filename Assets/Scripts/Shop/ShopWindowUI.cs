using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Project.UI;

namespace Project.Shop
{
    public class ShopWindowUI : MonoBehaviour, IUIWindow
    {
        [SerializeField] private ShopCatalog catalog;
        [SerializeField] private CoinWallet wallet;

        [SerializeField, Tooltip("열고 닫을 패널. 이 스크립트가 붙은 오브젝트와 달라야 한다")]
        private GameObject windowRoot;

        [SerializeField, Tooltip("ShopCategory 순서: 발판, 씨앗·재료, 확장, 꾸미기, 판매")]
        private Button[] tabButtons;

        [SerializeField] private ShopEntryUI entryPrefab;
        [SerializeField] private Transform entryParent;

        [SerializeField] private Color normalTabColor = new Color(0.25f, 0.25f, 0.28f, 1f);
        [SerializeField] private Color selectedTabColor = new Color(0.95f, 0.75f, 0.2f, 1f);

        private readonly List<ShopEntryUI> entryUIs = new List<ShopEntryUI>();
        private ShopCategory currentCategory = ShopCategory.Platform;
        private ShopEntry selectedEntry;

        public bool IsOpen => windowRoot.activeSelf;
        public ShopCategory CurrentCategory => currentCategory;
        public ShopEntry SelectedEntry => selectedEntry;

        // 창이 열리고 닫힐 때 발생 (열렸으면 true)
        public event Action<bool> OnOpenChanged;

        // 카테고리가 바뀔 때 발생
        public event Action<ShopCategory> OnCategoryChanged;

        // 항목을 클릭해서 선택했을 때 발생 (구매 처리는 5.4에서 이 이벤트를 받아서)
        public event Action<ShopEntry> OnEntrySelected;

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

        private void OnEnable()
        {
            wallet.OnCoinsChanged += RefreshPrices;
        }

        private void OnDisable()
        {
            wallet.OnCoinsChanged -= RefreshPrices;
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
            if (open) RefreshPrices();
            OnOpenChanged?.Invoke(open);
        }

        public void SelectCategory(ShopCategory category)
        {
            currentCategory = category;
            selectedEntry = null;

            RefreshTabs();
            RebuildList();
            OnCategoryChanged?.Invoke(category);
        }

        private void SelectEntry(ShopEntry entry)
        {
            selectedEntry = entry;
            RefreshSelection();
            OnEntrySelected?.Invoke(entry);
        }

        // 현재 카테고리의 항목으로 목록을 새로 만듦
        private void RebuildList()
        {
            foreach (var ui in entryUIs)
            {
                ui.gameObject.SetActive(false); // 레이아웃에서 바로 빠지게
                Destroy(ui.gameObject);
            }
            entryUIs.Clear();

            foreach (var entry in catalog.GetEntries(currentCategory))
            {
                var ui = Instantiate(entryPrefab, entryParent);
                ui.Setup(entry, SelectEntry);
                entryUIs.Add(ui);
            }

            RefreshPrices();
            RefreshSelection();
        }

        // 코인이 모자란 항목은 가격을 빨간색으로 (판매 탭은 돈을 받는 쪽이라 제외)
        private void RefreshPrices()
        {
            bool isSell = currentCategory == ShopCategory.Sell;
            foreach (var ui in entryUIs)
                ui.SetPriceShort(!isSell && wallet.Coins < ui.Entry.price);
        }

        private void RefreshSelection()
        {
            foreach (var ui in entryUIs)
                ui.SetSelected(ui.Entry == selectedEntry);
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