using System;

namespace HopUp.FarmEconomy
{
    /// <summary>
    /// 상점 수량 스테퍼(+ / - / 최대). 수량은 1 이상이고 살 수 있는 최대(MaxPurchasable)를 넘지 않는다.
    /// 살 수 있는 수량이 0이어도 표시 수량은 1로 둔다.
    /// </summary>
    public sealed class PurchaseQuantityStepper
    {
        int maxPurchasable;

        public int Quantity { get; private set; }
        public int MaxPurchasable { get { return maxPurchasable; } }

        public event Action<int> Changed;

        public PurchaseQuantityStepper()
        {
            Quantity = 1;
        }

        /// <summary>살 수 있는 최대 수량이 바뀌었을 때(코인·한도·인벤토리 변화) 호출. 현재 수량을 범위 안으로 맞춘다.</summary>
        public void SetMaxPurchasable(int max)
        {
            maxPurchasable = Math.Max(0, max);
            Set(Quantity);
        }

        public void Plus() { Set(Quantity + 1); }
        public void Minus() { Set(Quantity - 1); }
        public void SetToMax() { Set(maxPurchasable); }
        public void Reset() { Set(1); }

        void Set(int value)
        {
            int clamped = Math.Max(1, Math.Min(value, Math.Max(1, maxPurchasable)));
            if (clamped == Quantity) return;
            Quantity = clamped;
            var handler = Changed;
            if (handler != null) handler(Quantity);
        }
    }
}
