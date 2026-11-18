using System;
using System.Collections.Generic;

namespace HopUp.FarmEconomy
{
    public enum SeedInsertResult { Ok, NoSuchBot, NotSeed, BotNotAllowed, OtherSeedInside, Full, NoSeedInInventory }

    /// <summary>농사 봇 한 대의 수량 상태(AI-01~03). 변경은 FarmBotService를 통해서만 한다.</summary>
    public sealed class BotModel
    {
        public const int MaxSeeds = 30;

        public int Index { get; internal set; }
        /// <summary>들어 있는 씨앗의 작물 ID. SeedCount가 0이면 null.</summary>
        public string SeedCropId { get; internal set; }
        public int SeedCount { get; internal set; }
        /// <summary>봇이 수확해 들고 있는 당근(작물 ID별). 플레이어가 수령해야 창고로 간다.</summary>
        public CarrotStock Carrots { get; } = new CarrotStock();
    }

    /// <summary>세이브용 봇 상태. 상태 머신·위치·예약은 저장하지 않는다(불러오면 모든 봇이 대기에서 시작).</summary>
    public sealed class BotSnapshot
    {
        public string seedCropId;
        public int seedCount;
        public Dictionary<string, int> carrots = new Dictionary<string, int>();
    }

    /// <summary>
    /// 농사 봇 데이터(씨앗 투입, 당근 보관·수령, 저장). 봇 수의 정본은 ShopService.BotsOwned이고,
    /// 씬의 FarmBot(botIndex)이 Get(botIndex)로 자기 데이터를 찾는다. 상태 머신은 FarmBot에 있다.
    /// </summary>
    public sealed class FarmBotService
    {
        readonly BalanceConfig cfg;
        readonly IInventory inventory;
        readonly CarrotStock playerStock;
        readonly ShopService shop;
        readonly List<BotModel> bots = new List<BotModel>();

        /// <summary>씨앗·당근 수량이 바뀌거나 불러오기로 덮였을 때.</summary>
        public event Action<BotModel> BotChanged;

        public FarmBotService(BalanceConfig cfg, IInventory inventory, CarrotStock playerStock, ShopService shop)
        {
            this.cfg = cfg;
            this.inventory = inventory;
            this.playerStock = playerStock;
            this.shop = shop;
        }

        public int Count { get { return shop.BotsOwned; } }

        /// <summary>보유하지 않은 번호면 null. 같은 번호는 늘 같은 BotModel을 돌려준다.</summary>
        public BotModel Get(int index)
        {
            if (index < 0 || index >= shop.BotsOwned) return null;
            while (bots.Count <= index) bots.Add(new BotModel { Index = bots.Count });
            return bots[index];
        }

        // ───────── 플레이어 상호작용 ─────────

        /// <summary>
        /// 손에 든 씨앗(seedItemId, 든 수량 heldCount)을 봇에 넣는다. 최대 30개, 한 종류만, botAllowed 작물만.
        /// 낙하 중인지·든 아이템이 무엇인지는 호출자(플레이어 상호작용)가 판단한다.
        /// </summary>
        public SeedInsertResult InsertSeeds(int botIndex, string seedItemId, int heldCount)
        {
            BotModel bot = Get(botIndex);
            if (bot == null) return SeedInsertResult.NoSuchBot;
            string cropId = ItemIds.CropIdFromSeed(seedItemId);
            if (cropId == null) return SeedInsertResult.NotSeed;
            CropDef crop = cfg.FindCrop(cropId);
            if (crop == null || !crop.botAllowed) return SeedInsertResult.BotNotAllowed;
            if (bot.SeedCount > 0 && bot.SeedCropId != cropId) return SeedInsertResult.OtherSeedInside;

            if (bot.SeedCount >= BotModel.MaxSeeds) return SeedInsertResult.Full;
            int want = Math.Min(heldCount, BotModel.MaxSeeds - bot.SeedCount);
            if (want <= 0) return SeedInsertResult.NoSeedInInventory;
            int moved = inventory.TryRemove(seedItemId, want);  // 실제로 뺀 만큼만 넣어 씨앗이 생기거나 사라지지 않게
            if (moved <= 0) return SeedInsertResult.NoSeedInInventory;

            bot.SeedCropId = cropId;
            bot.SeedCount += moved;
            Raise(bot);
            return SeedInsertResult.Ok;
        }

        /// <summary>봇 당근을 종류별로 전부 플레이어 창고로 옮긴다. 옮긴 총 개수를 반환.</summary>
        public int CollectCarrots(int botIndex)
        {
            BotModel bot = Get(botIndex);
            if (bot == null || bot.Carrots.Total == 0) return 0;
            int total = 0;
            foreach (KeyValuePair<string, int> kv in bot.Carrots.Snapshot())
            {
                playerStock.Add(kv.Key, kv.Value);
                total += kv.Value;
            }
            bot.Carrots.Restore(null);
            Raise(bot);
            return total;
        }

        /// <summary>에임 안내 문구. 예: "씨앗 12/30 당근 · 당근 38". 씨앗이 없으면 종류 자리는 비운다.</summary>
        public string Describe(int botIndex)
        {
            BotModel bot = Get(botIndex);
            if (bot == null) return string.Empty;
            CropDef crop = cfg.FindCrop(bot.SeedCropId);
            string seedName = bot.SeedCount > 0 && crop != null ? " " + crop.name : string.Empty;
            return "씨앗 " + bot.SeedCount + "/" + BotModel.MaxSeeds + seedName + " · 당근 " + bot.Carrots.Total;
        }

        // ───────── 상태 머신(FarmBot)이 쓰는 창구 ─────────

        /// <summary>심기에 성공한 뒤에만 부른다. 씨앗을 미리 빼면 이동 포기·저장 때 씨앗이 사라진다.</summary>
        public void ConsumeSeed(BotModel bot)
        {
            if (bot == null || bot.SeedCount <= 0) return;
            bot.SeedCount--;
            if (bot.SeedCount == 0) bot.SeedCropId = null;
            Raise(bot);
        }

        public void AddHarvest(BotModel bot, string cropId, int amount)
        {
            if (bot == null || amount <= 0) return;
            bot.Carrots.Add(cropId, amount);
            Raise(bot);
        }

        // ───────── 저장/불러오기 ─────────

        public List<BotSnapshot> Capture()
        {
            var list = new List<BotSnapshot>();
            for (int i = 0; i < Count; i++)
            {
                BotModel bot = Get(i);
                list.Add(new BotSnapshot
                {
                    seedCropId = bot.SeedCropId,
                    seedCount = bot.SeedCount,
                    carrots = bot.Carrots.Snapshot()
                });
            }
            return list;
        }

        /// <summary>
        /// ShopService.RestoreBots 다음에 부른다. 모델 객체는 바꾸지 않고 값만 덮는다(씬의 봇이 참조를 들고 있음).
        /// 세이브 파일은 신뢰하지 않는다: 씨앗은 0~30으로 자르고, 없는 작물이나 봇 금지 작물이면 비운다.
        /// </summary>
        public void Restore(IList<BotSnapshot> snapshots)
        {
            Get(Count - 1);  // 보유 수만큼 모델 준비
            for (int i = 0; i < bots.Count; i++)
            {
                BotModel bot = bots[i];
                BotSnapshot s = snapshots != null && i < snapshots.Count && i < Count ? snapshots[i] : null;
                CropDef crop = s != null ? cfg.FindCrop(s.seedCropId) : null;
                int seeds = s != null ? Math.Max(0, Math.Min(s.seedCount, BotModel.MaxSeeds)) : 0;
                bool seedsValid = seeds > 0 && crop != null && crop.botAllowed;

                bot.SeedCropId = seedsValid ? crop.id : null;
                bot.SeedCount = seedsValid ? seeds : 0;
                bot.Carrots.Restore(s != null ? s.carrots : null);
                Raise(bot);
            }
        }

        void Raise(BotModel bot)
        {
            var handler = BotChanged;
            if (handler != null) handler(bot);
        }
    }
}
