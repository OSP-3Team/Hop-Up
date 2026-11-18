using System;

namespace HopUp.FarmEconomy
{
    /// <summary>코인 지갑. 누적 번 돈·쓴 돈도 함께 센다(플레이 로그용).</summary>
    public sealed class Wallet
    {
        public int Coins { get; private set; }
        public long LifetimeEarned { get; private set; }
        public long LifetimeSpent { get; private set; }

        /// <summary>코인이 바뀔 때마다 현재 코인을 알린다.</summary>
        public event Action<int> Changed;

        public void Add(int amount)
        {
            if (amount <= 0) return;
            Coins = (int)Math.Min((long)int.MaxValue, (long)Coins + amount);
            LifetimeEarned += amount;
            Raise();
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0 || amount > Coins) return false;
            if (amount == 0) return true;
            Coins -= amount;
            LifetimeSpent += amount;
            Raise();
            return true;
        }

        /// <summary>구매 도중 지급이 덜 됐을 때 되돌려 준다(쓴 돈 기록도 되돌린다).</summary>
        public void Refund(int amount)
        {
            if (amount <= 0) return;
            Coins = (int)Math.Min((long)int.MaxValue, (long)Coins + amount);
            LifetimeSpent = Math.Max(0L, LifetimeSpent - amount);
            Raise();
        }

        public void Restore(int coins, long lifetimeEarned, long lifetimeSpent)
        {
            Coins = Math.Max(0, coins);
            LifetimeEarned = Math.Max(0L, lifetimeEarned);
            LifetimeSpent = Math.Max(0L, lifetimeSpent);
            Raise();
        }

        void Raise()
        {
            var handler = Changed;
            if (handler != null) handler(Coins);
        }
    }
}
