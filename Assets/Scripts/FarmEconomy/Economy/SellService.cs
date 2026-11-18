using System;

namespace HopUp.FarmEconomy
{
    /// <summary>수확한 당근 판매. 작물별 sellPrice로 코인을 지갑에 넣는다.</summary>
    public sealed class SellService
    {
        readonly BalanceConfig cfg;
        readonly CarrotStock stock;
        readonly Wallet wallet;

        /// <summary>(작물 ID, 판 개수, 받은 코인).</summary>
        public event Action<string, int, int> Sold;

        public SellService(BalanceConfig cfg, CarrotStock stock, Wallet wallet)
        {
            this.cfg = cfg;
            this.stock = stock;
            this.wallet = wallet;
        }

        public int GetSellPrice(string cropId)
        {
            CropDef crop = cfg.FindCrop(cropId);
            return crop == null ? 0 : crop.sellPrice;
        }

        /// <summary>한 작물을 count개까지(가진 만큼만) 판다. 받은 코인을 반환.</summary>
        public int Sell(string cropId, int count)
        {
            CropDef crop = cfg.FindCrop(cropId);
            if (crop == null || count <= 0) return 0;
            int n = Math.Min(count, stock.Count(cropId));
            if (n <= 0) return 0;
            if (!stock.TryRemove(cropId, n)) return 0;

            int coins = n * crop.sellPrice;
            wallet.Add(coins);
            var handler = Sold;
            if (handler != null) handler(cropId, n, coins);
            return coins;
        }

        /// <summary>가진 당근을 모두 판다. 받은 코인 합계를 반환.</summary>
        public int SellAll()
        {
            int total = 0;
            foreach (CropDef crop in cfg.crops)
                total += Sell(crop.id, stock.Count(crop.id));
            return total;
        }
    }
}
