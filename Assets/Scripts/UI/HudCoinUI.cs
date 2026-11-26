using HopUp.FarmEconomy;
using TMPro;
using UnityEngine;

namespace Project.UI
{
    /// <summary>HUD 왼쪽 위에 현재 코인을 표시한다. EconomyHub의 Wallet을 읽는다.</summary>
    public class HudCoinUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text coinText;
        [SerializeField] private string format = "{0:N0}";

        private Wallet wallet;

        private void Start()
        {
            // EconomyHub가 먼저 준비된 뒤에 연결하려고 Start에서 찾는다
            if (EconomyHub.Instance == null)
            {
                Debug.LogWarning("[HudCoinUI] EconomyHub를 찾지 못했습니다.");
                return;
            }

            wallet = EconomyHub.Instance.Wallet;
            wallet.Changed += OnCoinsChanged;
            OnCoinsChanged(wallet.Coins);
        }

        private void OnDestroy()
        {
            if (wallet != null) wallet.Changed -= OnCoinsChanged;
        }

        private void OnCoinsChanged(int coins)
        {
            if (coinText != null) coinText.text = string.Format(format, coins);
        }
    }
}