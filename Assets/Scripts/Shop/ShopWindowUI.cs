using System;
using System.Collections.Generic;
using HopUp.FarmEconomy;
using Project.Inventory;
using Project.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Project.Shop
{
    public class ShopWindowUI : MonoBehaviour, IUIWindow
    {
        [Header("데이터")]
        [SerializeField, Tooltip("아이콘을 찾을 때 사용")]
        private InventoryManager inventory;
        [SerializeField] private Sprite slotIcon;
        [SerializeField] private Sprite botIcon;
        [SerializeField] private Sprite plotIcon;
        [SerializeField] private Sprite carrotIcon;

        [Header("화면")]
        [SerializeField, Tooltip("열고 닫을 패널. 이 스크립트가 붙은 오브젝트와 달라야 한다")]
        private GameObject windowRoot;

        [SerializeField, Tooltip("ShopCategory 순서: 발판, 씨앗·재료, 확장, 꾸미기, 판매")]
        private Button[] tabButtons;

        [SerializeField] private ShopEntryUI entryPrefab;
        [SerializeField] private Transform entryParent;

        [SerializeField] private Color normalTabColor = new Color(0.25f, 0.25f, 0.28f, 1f);
        [SerializeField] private Color selectedTabColor = new Color(0.95f, 0.75f, 0.2f, 1f);

        private readonly List<ShopEntryUI> entryUIs = new List<ShopEntryUI>();
        private readonly Dictionary<ShopEntry, ShopItem> itemOf = new Dictionary<ShopEntry, ShopItem>();
        private readonly Dictionary<ShopEntry, string> sellCropOf = new Dictionary<ShopEntry, string>();   // ← 이 줄 추가
        private ShopCategory currentCategory = ShopCategory.Platform;
        private ShopEntry selectedEntry;
        private EconomyHub hub;

        public bool IsOpen => windowRoot.activeSelf;
        public ShopCategory CurrentCategory => currentCategory;
        public ShopEntry SelectedEntry => selectedEntry;

        /// <summary>선택한 항목의 FarmEconomy 상품 (구매 처리용)</summary>
        public ShopItem SelectedItem =>
            selectedEntry != null && itemOf.TryGetValue(selectedEntry, out var item) ? item : null;
        
        /// <summary>판매 탭에서 선택한 당근 종류 (normal, fast, golden)</summary>
        public string SelectedSellCropId =>
            selectedEntry != null && sellCropOf.TryGetValue(selectedEntry, out var id) ? id : null;

        /// <summary>판매할 수 있는 당근 개수. 당근이 인벤토리 아이템으로 바뀌면 이 줄만 고친다.</summary>
        public int OwnedCount(string cropId) => hub != null ? hub.Carrots.Count(cropId) : 0;

        public event Action<bool> OnOpenChanged;
        public event Action<ShopCategory> OnCategoryChanged;
        public event Action<ShopEntry> OnEntrySelected;

        private void Awake()
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int index = i; // 클릭한 탭 번호를 기억하기 위한 복사
                tabButtons[i].onClick.AddListener(() => SelectCategory((ShopCategory)index));
            }
            windowRoot.SetActive(false);
        }

        private void Start()
        {
            hub = EconomyHub.Instance;
            if (hub == null)
            {
                Debug.LogWarning("[ShopWindowUI] EconomyHub를 찾지 못했습니다.");
                return;
            }
            hub.Wallet.Changed += OnCoinsChanged;
            SelectCategory(ShopCategory.Platform);
        }

        private void OnDestroy()
        {
            if (hub != null) hub.Wallet.Changed -= OnCoinsChanged;
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
            if (open) RebuildList(); // 지대에 따라 발판 가격이 바뀌니 열 때마다 새로 만든다
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

        /// <summary>구매 후 가격·상태를 새로 고친다. 선택한 항목은 유지한다.</summary>
        public void RefreshKeepSelection()
        {
            string keepId = selectedEntry != null ? selectedEntry.id : null;
            selectedEntry = null;
            RebuildList();

            if (keepId != null)
            {
                foreach (var ui in entryUIs)
                    if (ui.Entry.id == keepId) { selectedEntry = ui.Entry; break; }
            }
            RefreshSelection();
            OnEntrySelected?.Invoke(selectedEntry);
        }

        // 현재 카테고리의 상품으로 목록을 새로 만듦
        private void RebuildList()
        {
            foreach (var ui in entryUIs)
            {
                ui.gameObject.SetActive(false); // 레이아웃에서 바로 빠지게
                Destroy(ui.gameObject);
            }
            entryUIs.Clear();
            itemOf.Clear();
            sellCropOf.Clear();

            if (hub == null) return;

            if (currentCategory == ShopCategory.Sell)
            {
                // 판매 탭: 당근 종류별로 보유 개수와 판매가를 보여 준다
                foreach (CropDef crop in hub.Config.crops)
                {
                    var entry = new ShopEntry
                    {
                        id = "sell_" + crop.id,
                        category = currentCategory,
                        displayName = $"{crop.name} (보유 {OwnedCount(crop.id)})",
                        price = hub.Sales.GetSellPrice(crop.id),
                        icon = carrotIcon,
                    };
                    AddEntry(entry);
                    sellCropOf[entry] = crop.id;
                }
            }
            else
            {
                foreach (var item in hub.Shop.Items)
                {
                    if (CategoryOf(item.Kind) != currentCategory) continue;

                    var entry = new ShopEntry
                    {
                        id = item.Id,
                        category = currentCategory,
                        displayName = item.DisplayName,
                        price = hub.Shop.GetUnitPrice(item),
                        item = item.InventoryItemId != null ? inventory.GetItemData(item.InventoryItemId) : null,
                    };
                    entry.icon = IconOf(item, entry.item);
                    AddEntry(entry);
                    itemOf[entry] = item;
                }
            }

            RefreshPrices();
            RefreshSelection();
        }

        private void AddEntry(ShopEntry entry)
        {
            var ui = Instantiate(entryPrefab, entryParent);
            ui.Setup(entry, SelectEntry);
            entryUIs.Add(ui);
        }
        // ShopCategory 순서: 0 발판, 1 씨앗·재료, 2 확장, 3 꾸미기, 4 판매
        private static ShopCategory CategoryOf(ShopItemKind kind)
        {
            switch (kind)
            {
                case ShopItemKind.Scaffold: return (ShopCategory)0;
                case ShopItemKind.Seed: return (ShopCategory)1;
                default: return (ShopCategory)2; // 인벤토리 칸, 농사 봇, 밭 확장
            }
        }

        private Sprite IconOf(ShopItem item, ItemData data)
        {
            switch (item.Kind)
            {
                case ShopItemKind.InventorySlot: return slotIcon;
                case ShopItemKind.Bot: return botIcon;
                case ShopItemKind.PlotExpansion: return plotIcon;
                default: return data != null ? data.icon : null;
            }
        }

        private void OnCoinsChanged(int coins) => RefreshPrices();

        // 살 수 없는 항목(코인 부족, 잠김, 한도 도달)은 가격을 빨간색으로
        private void RefreshPrices()
        {
            if (hub == null) return;
            bool isSell = currentCategory == ShopCategory.Sell;
            foreach (var ui in entryUIs)
            {
                if (isSell || !itemOf.TryGetValue(ui.Entry, out var item))
                {
                    ui.SetPriceShort(false);
                    continue;
                }
                ui.SetPriceShort(hub.Shop.GetMaxPurchasable(item) <= 0);
            }
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