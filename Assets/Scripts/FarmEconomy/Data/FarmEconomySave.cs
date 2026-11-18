using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace HopUp.FarmEconomy
{
    /// <summary>
    /// 세이브 시스템(SYS-02, 이성재님)이 각 시스템의 저장 조각을 모으는 인터페이스.
    /// SaveManager는 Key로 루트 JSON에 Capture() 결과를 넣고, 불러올 때 같은 Key의 객체를 Restore()에 넘긴다.
    /// </summary>
    public interface ISaveSection
    {
        string Key { get; }
        JObject Capture();
        /// <summary>데이터가 손상됐거나 없으면 false(이 경우 현재 상태를 건드리지 않는다).</summary>
        bool Restore(JObject data);
    }

    /// <summary>농사·경제 저장 데이터(JSON). 인벤토리 내용·칸 수, 플레이어 위치는 각 담당 시스템이 저장한다.</summary>
    public sealed class FarmEconomySave
    {
        public int version = 1;
        public int coins;
        public long lifetimeEarned;
        public long lifetimeSpent;
        public Dictionary<string, int> carrots = new Dictionary<string, int>();
        public int zoneStage;
        public int botsOwned;
        public int unlockedNormalPlots;
        public List<PlotSnapshot> plots = new List<PlotSnapshot>();
        public List<string> discoveredRecipes = new List<string>();
        public int fallProtectionCharges;
        public long savedAtUnix;
    }

    public sealed class FarmEconomySaveSection : ISaveSection
    {
        public const string SectionKey = "farmEconomy";

        readonly Wallet wallet;
        readonly CarrotStock stock;
        readonly ZoneProgress zone;
        readonly FarmField field;
        readonly ShopService shop;
        readonly CookingService cooking;

        public FarmEconomySaveSection(Wallet wallet, CarrotStock stock, ZoneProgress zone, FarmField field, ShopService shop, CookingService cooking)
        {
            this.wallet = wallet;
            this.stock = stock;
            this.zone = zone;
            this.field = field;
            this.shop = shop;
            this.cooking = cooking;
        }

        public string Key { get { return SectionKey; } }

        public JObject Capture()
        {
            var dto = new FarmEconomySave
            {
                coins = wallet.Coins,
                lifetimeEarned = wallet.LifetimeEarned,
                lifetimeSpent = wallet.LifetimeSpent,
                carrots = stock.Snapshot(),
                zoneStage = zone.Stage,
                botsOwned = shop.BotsOwned,
                unlockedNormalPlots = field.UnlockedNormalPlots,
                plots = field.Capture(),
                discoveredRecipes = cooking.DiscoveredRecipeIds.ToList(),
                fallProtectionCharges = cooking.FallProtectionCharges,
                savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            return JObject.FromObject(dto);
        }

        public bool Restore(JObject data)
        {
            if (data == null) return false;
            FarmEconomySave dto;
            try
            {
                dto = data.ToObject<FarmEconomySave>();
            }
            catch (Exception)
            {
                return false;
            }
            if (dto == null) return false;

            wallet.Restore(dto.coins, dto.lifetimeEarned, dto.lifetimeSpent);
            stock.Restore(dto.carrots);
            zone.Restore(dto.zoneStage);
            shop.RestoreBots(dto.botsOwned);
            field.Restore(dto.unlockedNormalPlots, dto.plots);
            cooking.RestoreState(dto.discoveredRecipes, dto.fallProtectionCharges);

            if (dto.savedAtUnix > 0)
            {
                double away = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - dto.savedAtUnix;
                field.AdvanceOffline(away);
            }
            return true;
        }
    }
}
