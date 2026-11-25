using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using HopUp.FarmEconomy;

namespace Project.UI
{
    // 봇 목록의 한 줄: 상태 문구 + 씨앗 넣기·당근 받기 버튼
    public class BotEntryUI : MonoBehaviour
    {
        // shortcut: 봇에 넣을 수 있는 작물이 일반·빠른 둘뿐이라 버튼을 고정함, botAllowed 작물이 늘면 버튼을 작물 목록으로 만들기
        public static readonly string NormalSeedId = ItemIds.Seed("normal");
        public static readonly string FastSeedId = ItemIds.Seed("fast");

        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button normalSeedButton;
        [SerializeField] private Button fastSeedButton;
        [SerializeField] private Button collectButton;

        public int BotIndex { get; private set; }

        public void Setup(int botIndex, Action<int, string> onInsertSeed, Action<int> onCollect)
        {
            BotIndex = botIndex;
            normalSeedButton.onClick.RemoveAllListeners();
            fastSeedButton.onClick.RemoveAllListeners();
            collectButton.onClick.RemoveAllListeners();
            normalSeedButton.onClick.AddListener(() => onInsertSeed?.Invoke(BotIndex, NormalSeedId));
            fastSeedButton.onClick.AddListener(() => onInsertSeed?.Invoke(BotIndex, FastSeedId));
            collectButton.onClick.AddListener(() => onCollect?.Invoke(BotIndex));
        }

        public void Refresh(string displayName, string status, bool canNormal, bool canFast, bool canCollect)
        {
            nameText.text = displayName;
            statusText.text = status;
            normalSeedButton.interactable = canNormal;
            fastSeedButton.interactable = canFast;
            collectButton.interactable = canCollect;
        }
    }
}
