using System;
using UnityEngine;

namespace Project.Shop
{
    public class CoinWallet : MonoBehaviour
    {
        [SerializeField, Min(0)] private int coins = 100; // 테스트용 시작 값

        // 코인이 바뀔 때마다 발생
        public event Action OnCoinsChanged;

        public int Coins => coins;

        public void Add(int amount)
        {
            if (amount <= 0) return;
            coins += amount;
            OnCoinsChanged?.Invoke();
        }

        // 코인이 모자라면 아무것도 빼지 않고 false
        public bool TrySpend(int amount)
        {
            if (amount < 0 || coins < amount) return false;
            coins -= amount;
            OnCoinsChanged?.Invoke();
            return true;
        }

        // 테스트용: 컴포넌트 점 세 개(⋮) 메뉴에서 실행
        [ContextMenu("Add 10 Coins")]
        private void DebugAdd() => Add(10);

        [ContextMenu("Spend 10 Coins")]
        private void DebugSpend() => TrySpend(10);
    }
}