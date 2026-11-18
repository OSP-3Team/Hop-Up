using System;
using System.Collections.Generic;

namespace HopUp.FarmEconomy
{
    public enum ShopItemKind { Scaffold, Seed, InventorySlot, Bot, PlotExpansion }

    public sealed class ShopItem
    {
        public string Id;
        public string DisplayName;
        public ShopItemKind Kind;
        /// <summary>인벤토리에 들어가는 상품이면 그 아이템 ID. 칸 확장·봇·밭 확장은 null.</summary>
        public string InventoryItemId;
        public string CropId;
        public int ScaffoldTier;
    }

    public enum PurchaseStatus { Success, Partial, LockedOrLimit, NotEnoughCoins, InventoryFull, InvalidRequest }

    public struct PurchaseResult
    {
        public PurchaseStatus Status;
        public int Requested;
        public int Purchased;
        public int CoinsSpent;

        public bool IsOk { get { return Status == PurchaseStatus.Success || Status == PurchaseStatus.Partial; } }
    }

    /// <summary>
    /// 상점·경제(SYS-03). 흐름도 "상점 구매"를 그대로 구현한다.
    /// N = min(요청 수, 코인으로 살 수 있는 수, 한도 잔여, 인벤토리 수용량). N이 0이면 사유를
    /// (잠금·한도) → (코인) → (인벤토리) 순서로 판정하고, N이 요청보다 작으면 N개만 사서 부분 구매로 알린다.
    /// 가격: 발판 = 기본가 x 지대 배수(1,2,4,8), 씨앗·그 외 = 설정 가격(칸·봇·밭 확장은 구매 횟수에 따라 오르는 가격표).
    /// </summary>
    public sealed class ShopService
    {
        public const int MaxQuantity = 999;
        public const string SlotItemId = "shop_slot";
        public const string BotItemId = "shop_bot";
        public const string PlotItemId = "shop_plot";

        readonly BalanceConfig cfg;
        readonly Wallet wallet;
        readonly ZoneProgress zone;
        readonly IInventory inventory;
        readonly FarmField field;
        readonly List<ShopItem> items = new List<ShopItem>();

        /// <summary>구매한 농사 봇 수. 봇 시스템(장예준님)이 이 값과 Purchased 이벤트로 봇을 생성한다.</summary>
        public int BotsOwned { get; private set; }

        /// <summary>(상품, 지급된 수량, 사용한 코인). 플레이 로그(SYS-04)가 구독해 발판 사용량을 기록할 수 있다.</summary>
        public event Action<ShopItem, int, int> Purchased;

        public IReadOnlyList<ShopItem> Items { get { return items; } }

        public ShopService(BalanceConfig cfg, Wallet wallet, ZoneProgress zone, IInventory inventory, FarmField field)
        {
            this.cfg = cfg;
            this.wallet = wallet;
            this.zone = zone;
            this.inventory = inventory;
            this.field = field;
            BuildCatalog();
        }

        void BuildCatalog()
        {
            foreach (ScaffoldDef s in cfg.shop.scaffolds)
            {
                string id = ItemIds.Scaffold(s.tier);
                items.Add(new ShopItem { Id = id, DisplayName = s.name, Kind = ShopItemKind.Scaffold, InventoryItemId = id, ScaffoldTier = s.tier });
            }
            foreach (CropDef c in cfg.crops)
            {
                string id = ItemIds.Seed(c.id);
                items.Add(new ShopItem { Id = id, DisplayName = c.name + " 씨앗", Kind = ShopItemKind.Seed, InventoryItemId = id, CropId = c.id });
            }
            items.Add(new ShopItem { Id = SlotItemId, DisplayName = "인벤토리 칸", Kind = ShopItemKind.InventorySlot });
            items.Add(new ShopItem { Id = BotItemId, DisplayName = "농사 봇", Kind = ShopItemKind.Bot });
            items.Add(new ShopItem { Id = PlotItemId, DisplayName = "밭 확장", Kind = ShopItemKind.PlotExpansion });
        }

        public ShopItem Find(string id)
        {
            for (int i = 0; i < items.Count; i++)
                if (items[i].Id == id) return items[i];
            return null;
        }

        // ───────── 가격·한도·잠금 ─────────

        /// <summary>다음 1개의 단가. offset은 같은 주문에서 이미 사기로 한 개수(가격이 오르는 상품용).</summary>
        public int GetUnitPrice(ShopItem item, int offset = 0)
        {
            switch (item.Kind)
            {
                case ShopItemKind.Scaffold:
                {
                    int basePrice = 0;
                    foreach (ScaffoldDef s in cfg.shop.scaffolds)
                        if (s.tier == item.ScaffoldTier) { basePrice = s.basePrice; break; }
                    int[] mult = cfg.zones.scaffoldPriceMultipliers;
                    return basePrice * mult[Math.Min(zone.Stage, mult.Length - 1)];
                }
                case ShopItemKind.Seed:
                {
                    CropDef crop = cfg.FindCrop(item.CropId);
                    return crop == null ? 0 : crop.seedPrice;
                }
                case ShopItemKind.InventorySlot:
                    return Pick(cfg.shop.slotPrices, inventory.SlotCount - cfg.shop.startSlots + offset);
                case ShopItemKind.Bot:
                    return Pick(cfg.shop.botPrices, BotsOwned + offset);
                case ShopItemKind.PlotExpansion:
                {
                    int idx = Math.Max(0, field.UnlockedNormalPlots - cfg.farm.startPlotCount + offset);
                    double price = cfg.farm.plotExpansionBasePrice * Math.Pow(cfg.farm.plotExpansionPriceGrowth, idx);
                    return (int)Math.Round(price);
                }
            }
            return 0;
        }

        static int Pick(int[] table, int index)
        {
            if (table == null || table.Length == 0) return 0;
            return table[Math.Max(0, Math.Min(index, table.Length - 1))];
        }

        /// <summary>지대 단계 등으로 아직 열리지 않은 상품인가.</summary>
        public bool IsLocked(ShopItem item)
        {
            if (item.Kind == ShopItemKind.Seed)
            {
                CropDef crop = cfg.FindCrop(item.CropId);
                return crop == null || crop.unlockStage > zone.Stage;
            }
            if (item.Kind == ShopItemKind.Bot) return zone.Stage < cfg.shop.botUnlockStage;
            return false;
        }

        /// <summary>한도까지 더 살 수 있는 수량(무제한이면 int.MaxValue).</summary>
        public int GetLimitRemaining(ShopItem item)
        {
            switch (item.Kind)
            {
                case ShopItemKind.InventorySlot:
                    return Math.Max(0, Math.Min(cfg.shop.maxSlots, inventory.MaxSlots) - inventory.SlotCount);
                case ShopItemKind.Bot:
                    return Math.Max(0, cfg.shop.maxBots - BotsOwned);
                case ShopItemKind.PlotExpansion:
                    return Math.Max(0, field.NormalPlotCap - field.UnlockedNormalPlots);
            }
            return int.MaxValue;
        }

        int AffordableCount(ShopItem item, int requested)
        {
            long budget = wallet.Coins;
            int n = 0;
            while (n < requested)
            {
                int price = GetUnitPrice(item, n);
                if (budget < price) break;
                budget -= price;
                n++;
            }
            return n;
        }

        int InventoryCapacity(ShopItem item)
        {
            return item.InventoryItemId == null ? int.MaxValue : inventory.FreeCapacityFor(item.InventoryItemId);
        }

        /// <summary>지금 살 수 있는 최대 수량 N(스테퍼의 최대값).</summary>
        public int GetMaxPurchasable(ShopItem item)
        {
            if (item == null || IsLocked(item)) return 0;
            int limit = GetLimitRemaining(item);
            if (limit <= 0) return 0;
            int want = Math.Min(MaxQuantity, limit);
            return Math.Min(AffordableCount(item, want), InventoryCapacity(item));
        }

        // ───────── 구매 ─────────

        public PurchaseResult TryPurchase(ShopItem item, int requested)
        {
            var result = new PurchaseResult { Requested = requested, Status = PurchaseStatus.InvalidRequest };
            if (item == null || requested <= 0) return result;
            requested = Math.Min(requested, MaxQuantity);
            result.Requested = requested;

            bool locked = IsLocked(item);
            int limit = GetLimitRemaining(item);
            int affordable = (locked || limit <= 0) ? 0 : AffordableCount(item, Math.Min(requested, limit));
            int capacity = InventoryCapacity(item);
            int n = locked ? 0 : Math.Min(Math.Min(requested, limit), Math.Min(affordable, capacity));

            if (n <= 0)
            {
                if (locked || limit <= 0) result.Status = PurchaseStatus.LockedOrLimit;
                else if (affordable <= 0) result.Status = PurchaseStatus.NotEnoughCoins;
                else result.Status = PurchaseStatus.InventoryFull;
                return result;
            }

            // 지급 전에 개당 가격을 확정해 둔다(지급 후에는 가격표 위치가 바뀐다)
            var prices = new int[n];
            int cost = 0;
            for (int i = 0; i < n; i++)
            {
                prices[i] = GetUnitPrice(item, i);
                cost += prices[i];
            }
            if (!wallet.TrySpend(cost))
            {
                result.Status = PurchaseStatus.NotEnoughCoins;
                return result;
            }

            int delivered = Deliver(item, n);
            if (delivered < n)
            {
                int refund = 0;
                for (int i = delivered; i < n; i++) refund += prices[i];
                wallet.Refund(refund);
                cost -= refund;
            }
            if (delivered <= 0)
            {
                result.Status = PurchaseStatus.InventoryFull;
                return result;
            }

            result.Purchased = delivered;
            result.CoinsSpent = cost;
            result.Status = delivered == requested ? PurchaseStatus.Success : PurchaseStatus.Partial;
            var handler = Purchased;
            if (handler != null) handler(item, delivered, cost);
            return result;
        }

        int Deliver(ShopItem item, int n)
        {
            switch (item.Kind)
            {
                case ShopItemKind.Scaffold:
                case ShopItemKind.Seed:
                    return inventory.TryAdd(item.InventoryItemId, n);
                case ShopItemKind.InventorySlot:
                {
                    int done = 0;
                    while (done < n && inventory.TryExpandSlots(1)) done++;
                    return done;
                }
                case ShopItemKind.Bot:
                    BotsOwned += n;
                    return n;
                case ShopItemKind.PlotExpansion:
                    return field.ExpandNormalPlots(n);
            }
            return 0;
        }

        /// <summary>불러오기용.</summary>
        public void RestoreBots(int botsOwned)
        {
            BotsOwned = Math.Max(0, Math.Min(botsOwned, cfg.shop.maxBots));
        }
    }
}
