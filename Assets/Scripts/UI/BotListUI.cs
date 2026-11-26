using System.Collections.Generic;
using TMPro;
using UnityEngine;
using HopUp.FarmEconomy;
using Project.Inventory;

namespace Project.UI
{
    // 인벤토리 창('i')의 레시피 옆 봇 영역. 봇을 하나라도 보유했을 때만 보이고, 보유한 봇마다 한 줄씩 만든다.
    // 창이 열릴 때마다(OnEnable) 보유 수를 다시 본다. 창이 열린 동안에는 상점이 닫혀 있어 봇 수가 바뀌지 않는다.
    public class BotListUI : MonoBehaviour
    {
        [SerializeField] private InventoryManager inventory;
        [SerializeField, Tooltip("봇이 없으면 숨길 영역. 이 스크립트가 붙은 오브젝트와 달라야 한다")]
        private GameObject botArea;
        [SerializeField] private BotEntryUI entryPrefab;
        [SerializeField] private Transform entryParent;
        [SerializeField] private TMP_Text messageText;

        private readonly List<BotEntryUI> entries = new List<BotEntryUI>();
        private EconomyHub hub;

        private void OnEnable()
        {
            hub = EconomyHub.Instance;
            if (hub == null)
            {
                Debug.LogError("[BotListUI] 씬에 EconomyHub가 없습니다.", this);
                return;
            }

            int count = hub.Bots.Count;
            botArea.SetActive(count > 0);
            BuildEntries(count);

            inventory.OnInventoryChanged += Refresh;
            hub.Bots.BotChanged += OnBotChanged;

            SetMessage(string.Empty);
            Refresh();
        }

        private void OnDisable()
        {
            inventory.OnInventoryChanged -= Refresh;
            if (hub != null) hub.Bots.BotChanged -= OnBotChanged;
        }

        private void BuildEntries(int count)
        {
            // 불러오기로 봇 수가 줄었을 수도 있어 남는 줄은 지움
            while (entries.Count > count)
            {
                BotEntryUI last = entries[entries.Count - 1];
                entries.RemoveAt(entries.Count - 1);
                last.gameObject.SetActive(false); // 레이아웃에서 바로 빠지게
                Destroy(last.gameObject);
            }
            while (entries.Count < count)
            {
                BotEntryUI entry = Instantiate(entryPrefab, entryParent);
                entry.Setup(entries.Count, OnInsertSeedClicked, OnCollectClicked);
                entries.Add(entry);
            }
        }

        private void OnBotChanged(BotModel bot)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (hub == null) return;

            foreach (BotEntryUI entry in entries)
            {
                BotModel bot = hub.Bots.Get(entry.BotIndex);
                if (bot == null) continue;

                bool hasRoom = bot.SeedCount < BotModel.MaxSeeds;
                entry.Refresh(
                    $"봇 {entry.BotIndex + 1}",
                    hub.Bots.Describe(entry.BotIndex),
                    hasRoom && hub.Inventory.CountOf(BotEntryUI.NormalSeedId) > 0,
                    hasRoom && hub.Inventory.CountOf(BotEntryUI.FastSeedId) > 0,
                    bot.Carrots.Total > 0);
            }
        }

        private void OnInsertSeedClicked(int botIndex, string seedItemId)
        {
            BotModel bot = hub.Bots.Get(botIndex);
            int before = bot != null ? bot.SeedCount : 0;
            SeedInsertResult result = hub.Bots.InsertSeeds(botIndex, seedItemId, hub.Inventory.CountOf(seedItemId));

            switch (result)
            {
                case SeedInsertResult.Ok:
                    SetMessage($"봇 {botIndex + 1}에 씨앗 {bot.SeedCount - before}개를 넣었어요");
                    break;
                case SeedInsertResult.OtherSeedInside:
                    SetMessage("다른 씨앗이 들어 있어요. 다 심을 때까지 기다려 주세요");
                    break;
                case SeedInsertResult.Full:
                    SetMessage("씨앗이 가득 찼어요");
                    break;
                case SeedInsertResult.NoSeedInInventory:
                    SetMessage("씨앗이 없어요");
                    break;
                default:
                    SetMessage("봇에 넣을 수 없는 씨앗이에요");
                    break;
            }
        }

        private void OnCollectClicked(int botIndex)
        {
            int amount = hub.Bots.CollectCarrots(botIndex);
            SetMessage(amount > 0 ? $"당근 {amount}개를 받았어요" : "받을 당근이 없어요");
        }

        private void SetMessage(string message)
        {
            if (messageText != null) messageText.text = message;
        }
    }
}
