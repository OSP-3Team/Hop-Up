using HopUp.FarmEconomy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Shop
{
    /// <summary>상점 창 아래 거래 패널. 구매 탭에서는 구매, 판매 탭에서는 판매를 처리한다.</summary>
    public class ShopPurchaseUI : MonoBehaviour
    {
        [SerializeField] private ShopWindowUI shopWindow;

        [Header("표시")]
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text quantityText;
        [SerializeField] private TMP_Text totalText;
        [SerializeField] private TMP_Text messageText;

        [Header("버튼")]
        [SerializeField] private Button minusButton;
        [SerializeField] private Button plusButton;
        [SerializeField] private Button buyButton;
        [SerializeField] private TMP_Text buyButtonText;

        [SerializeField] private Color okColor = Color.white;
        [SerializeField] private Color failColor = new Color(1f, 0.4f, 0.4f);

        private EconomyHub hub;
        private int quantity = 1;

        private bool IsSellTab => shopWindow.CurrentCategory == ShopCategory.Sell;

        private void Start()
        {
            hub = EconomyHub.Instance;
            if (hub == null || shopWindow == null) return;

            shopWindow.OnEntrySelected += OnEntrySelected;
            shopWindow.OnCategoryChanged += OnCategoryChanged;
            shopWindow.OnOpenChanged += OnOpenChanged;
            hub.Wallet.Changed += OnCoinsChanged;

            minusButton.onClick.AddListener(() => ChangeQuantity(-1));
            plusButton.onClick.AddListener(() => ChangeQuantity(+1));
            buyButton.onClick.AddListener(OnActionClicked);

            ShowMessage("", true);
            Refresh();
        }

        private void OnDestroy()
        {
            if (shopWindow != null)
            {
                shopWindow.OnEntrySelected -= OnEntrySelected;
                shopWindow.OnCategoryChanged -= OnCategoryChanged;
                shopWindow.OnOpenChanged -= OnOpenChanged;
            }
            if (hub != null) hub.Wallet.Changed -= OnCoinsChanged;
        }

        private void OnEntrySelected(ShopEntry entry) { quantity = 1; Refresh(); }
        private void OnCategoryChanged(ShopCategory category) { quantity = 1; ShowMessage("", true); Refresh(); }
        private void OnOpenChanged(bool open) { if (open) { quantity = 1; ShowMessage("", true); Refresh(); } }
        private void OnCoinsChanged(int coins) => Refresh();

        private void ChangeQuantity(int delta)
        {
            quantity += delta;
            Refresh();
        }

        private void Refresh()
        {
            if (hub == null) return;
            if (IsSellTab) RefreshSell();
            else RefreshBuy();
        }

        private void ShowEmpty(string actionLabel)
        {
            nameText.text = "항목을 선택하세요";
            quantityText.text = "-";
            totalText.text = "";
            buyButtonText.text = actionLabel;
            minusButton.interactable = plusButton.interactable = buyButton.interactable = false;
        }

        // ───────── 구매 ─────────

        private void RefreshBuy()
        {
            ShopItem item = shopWindow.SelectedItem;
            if (item == null) { ShowEmpty("구매"); return; }

            int max = hub.Shop.GetMaxPurchasable(item);
            quantity = Mathf.Clamp(quantity, 1, Mathf.Max(1, max));

            int total = 0;
            for (int i = 0; i < quantity; i++) total += hub.Shop.GetUnitPrice(item, i);

            nameText.text = item.DisplayName;
            quantityText.text = quantity.ToString();
            totalText.text = $"합계 {total:N0}";

            minusButton.interactable = quantity > 1;
            plusButton.interactable = quantity < max;
            buyButton.interactable = max > 0;
            buyButtonText.text = max > 0 ? "구매" : BuyReasonText(item);
        }

        // 살 수 없을 때 버튼에 보여 줄 이유 (ShopService 판정 순서와 같게)
        private string BuyReasonText(ShopItem item)
        {
            if (hub.Shop.IsLocked(item)) return "잠김";
            if (hub.Shop.GetLimitRemaining(item) <= 0) return "최대";
            if (hub.Wallet.Coins < hub.Shop.GetUnitPrice(item)) return "코인 부족";
            return "공간 부족";
        }

        private void Buy()
        {
            ShopItem item = shopWindow.SelectedItem;
            if (item == null) return;

            PurchaseResult r = hub.Shop.TryPurchase(item, quantity);
            switch (r.Status)
            {
                case PurchaseStatus.Success:
                    ShowMessage($"{item.DisplayName} {r.Purchased}개 구매 (-{r.CoinsSpent:N0})", true); break;
                case PurchaseStatus.Partial:
                    ShowMessage($"{r.Purchased}개만 구매했어요 (-{r.CoinsSpent:N0})", true); break;
                case PurchaseStatus.NotEnoughCoins:
                    ShowMessage("코인이 부족해요", false); break;
                case PurchaseStatus.InventoryFull:
                    ShowMessage("인벤토리 공간이 부족해요", false); break;
                case PurchaseStatus.LockedOrLimit:
                    ShowMessage("아직 살 수 없거나 최대치예요", false); break;
                default:
                    ShowMessage("구매할 수 없어요", false); break;
            }
        }

        // ───────── 판매 ─────────

        private void RefreshSell()
        {
            string cropId = shopWindow.SelectedSellCropId;
            if (cropId == null) { ShowEmpty("판매"); return; }

            int owned = shopWindow.OwnedCount(cropId);
            int price = hub.Sales.GetSellPrice(cropId);
            quantity = Mathf.Clamp(quantity, 1, Mathf.Max(1, owned));

            CropDef crop = hub.Config.FindCrop(cropId);
            nameText.text = crop != null ? crop.name : cropId;
            quantityText.text = quantity.ToString();
            totalText.text = $"받는 코인 {quantity * price:N0}";

            minusButton.interactable = quantity > 1;
            plusButton.interactable = quantity < owned;
            buyButton.interactable = owned > 0;
            buyButtonText.text = owned > 0 ? "판매" : "보유 없음";
        }

        private void Sell()
        {
            string cropId = shopWindow.SelectedSellCropId;
            if (cropId == null) return;

            int before = shopWindow.OwnedCount(cropId);
            int coins = hub.Sales.Sell(cropId, quantity);
            int sold = before - shopWindow.OwnedCount(cropId);

            if (coins > 0)
            {
                CropDef crop = hub.Config.FindCrop(cropId);
                string name = crop != null ? crop.name : cropId;
                ShowMessage($"{name} {sold}개 판매 (+{coins:N0})", true);
            }
            else
            {
                ShowMessage("판매할 당근이 없어요", false);
            }
        }

        // ───────── 공통 ─────────

        private void OnActionClicked()
        {
            if (IsSellTab) Sell();
            else Buy();

            quantity = 1;
            shopWindow.RefreshKeepSelection(); // 가격·보유 개수가 바뀌었으니 목록 갱신
        }

        private void ShowMessage(string text, bool ok)
        {
            messageText.text = text;
            messageText.color = ok ? okColor : failColor;
        }
    }
}