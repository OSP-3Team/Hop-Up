using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HopUp.FarmEconomy
{
    /// <summary>아이템 ID와 개수. 재료, 시작 아이템 등에 쓴다.</summary>
    public sealed class ItemStack
    {
        public string itemId;
        public int count;

        public ItemStack() { }
        public ItemStack(string itemId, int count) { this.itemId = itemId; this.count = count; }
    }

    public sealed class ScaffoldDef
    {
        public int tier;
        public string name;
        /// <summary>0단계(지대 배수 x1) 기준 가격.</summary>
        public int basePrice;
    }

    public sealed class CropDef
    {
        public string id;
        public string name;
        public float growSeconds;
        public int harvestAmount = 1;
        public int seedPrice;
        public int sellPrice;
        public int unlockStage;
        /// <summary>황금 전용 칸에서만 자라는 작물인가.</summary>
        public bool golden;
        public bool botAllowed = true;
    }

    public sealed class EffectDef
    {
        public string type;
        public int charges = 1;
    }

    public sealed class RecipeDef
    {
        public string id;
        public string name;
        public List<ItemStack> ingredients = new List<ItemStack>();
        public string resultItemId;
        public EffectDef effect;
    }

    public sealed class FarmConfig
    {
        public int startPlotCount;
        public int[] plotCapByStage = new int[0];
        public int goldenPlotCount;
        public int goldenPlotUnlockStage;
        /// <summary>물을 주면 성장 시간이 이 비율만큼 줄어든다(0.2 = 20%).</summary>
        public float waterGrowthReduction;
        /// <summary>이어하기 때 오프라인 성장 한도(초). 0이면 오프라인 성장 없음.</summary>
        public double offlineGrowthCapSeconds;
        public double plotExpansionBasePrice;
        public double plotExpansionPriceGrowth = 1.0;
    }

    public sealed class ZoneConfig
    {
        /// <summary>정거장 높이(m). 이 높이에 도달하면 다음 지대 단계가 열린다.</summary>
        public float[] stationHeights = new float[0];
        public int[] scaffoldPriceMultipliers = new int[0];
    }

    public sealed class ShopConfig
    {
        public int startSlots = 1;
        public int maxSlots = 1;
        public int inventoryStackLimit = 1;
        public int maxBots;
        public int botUnlockStage;
        public int[] slotPrices = new int[0];
        public int[] botPrices = new int[0];
        public List<ScaffoldDef> scaffolds = new List<ScaffoldDef>();
    }

    public sealed class CookingConfig
    {
        public int maxFallProtectionCharges = 1;
        public List<RecipeDef> recipes = new List<RecipeDef>();
    }

    public sealed class NewGameConfig
    {
        public int startingCoins;
        public List<ItemStack> startingItems = new List<ItemStack>();
    }

    /// <summary>농사·상점·경제 밸런스 값 전체. StreamingAssets/Balance/farm_economy.json에서 읽는다.</summary>
    public sealed class BalanceConfig
    {
        public FarmConfig farm = new FarmConfig();
        public ZoneConfig zones = new ZoneConfig();
        public ShopConfig shop = new ShopConfig();
        public List<CropDef> crops = new List<CropDef>();
        public CookingConfig cooking = new CookingConfig();
        public NewGameConfig newGame = new NewGameConfig();

        /// <summary>지대 단계 개수(정거장 수 + 1).</summary>
        [JsonIgnore]
        public int StageCount { get { return zones.stationHeights.Length + 1; } }

        public CropDef FindCrop(string id)
        {
            if (id == null) return null;
            for (int i = 0; i < crops.Count; i++)
                if (crops[i].id == id) return crops[i];
            return null;
        }

        public RecipeDef FindRecipe(string id)
        {
            if (id == null) return null;
            for (int i = 0; i < cooking.recipes.Count; i++)
                if (cooking.recipes[i].id == id) return cooking.recipes[i];
            return null;
        }

        /// <summary>값이 말이 되는지 검사한다. 비어 있으면 문제 없음.</summary>
        public List<string> Validate()
        {
            var e = new List<string>();
            int stages = StageCount;

            // 지대
            float prev = 0f;
            foreach (float h in zones.stationHeights)
            {
                if (h <= prev) e.Add("zones.stationHeights는 0보다 크고 오름차순이어야 합니다.");
                prev = h;
            }
            if (zones.scaffoldPriceMultipliers.Length < stages)
                e.Add("zones.scaffoldPriceMultipliers는 지대 단계 수(" + stages + ")만큼 필요합니다.");
            foreach (int m in zones.scaffoldPriceMultipliers)
                if (m <= 0) e.Add("zones.scaffoldPriceMultipliers에 0 이하 값이 있습니다.");

            // 농장
            if (farm.plotCapByStage.Length < 1) e.Add("farm.plotCapByStage가 비어 있습니다.");
            else
            {
                for (int i = 0; i < farm.plotCapByStage.Length; i++)
                {
                    if (farm.plotCapByStage[i] <= 0) e.Add("farm.plotCapByStage에 0 이하 값이 있습니다.");
                    if (i > 0 && farm.plotCapByStage[i] < farm.plotCapByStage[i - 1])
                        e.Add("farm.plotCapByStage는 줄어들 수 없습니다.");
                }
                if (farm.startPlotCount < 0 || farm.startPlotCount > farm.plotCapByStage[0])
                    e.Add("farm.startPlotCount는 0 이상, 0단계 칸 상한 이하여야 합니다.");
            }
            if (farm.goldenPlotCount < 0) e.Add("farm.goldenPlotCount는 0 이상이어야 합니다.");
            if (farm.goldenPlotUnlockStage < 0 || farm.goldenPlotUnlockStage >= stages)
                e.Add("farm.goldenPlotUnlockStage가 지대 단계 범위를 벗어났습니다.");
            if (farm.waterGrowthReduction < 0f || farm.waterGrowthReduction >= 1f)
                e.Add("farm.waterGrowthReduction은 0 이상 1 미만이어야 합니다.");
            if (farm.offlineGrowthCapSeconds < 0.0) e.Add("farm.offlineGrowthCapSeconds는 0 이상이어야 합니다.");
            if (farm.plotExpansionBasePrice < 0.0) e.Add("farm.plotExpansionBasePrice는 0 이상이어야 합니다.");
            if (farm.plotExpansionPriceGrowth <= 0.0) e.Add("farm.plotExpansionPriceGrowth는 0보다 커야 합니다.");

            // 상점
            if (shop.startSlots < 1) e.Add("shop.startSlots는 1 이상이어야 합니다.");
            if (shop.maxSlots < shop.startSlots) e.Add("shop.maxSlots는 startSlots 이상이어야 합니다.");
            if (shop.inventoryStackLimit < 1) e.Add("shop.inventoryStackLimit는 1 이상이어야 합니다.");
            if (shop.slotPrices.Length < shop.maxSlots - shop.startSlots)
                e.Add("shop.slotPrices는 살 수 있는 칸 수(" + (shop.maxSlots - shop.startSlots) + ")만큼 필요합니다.");
            if (shop.maxBots < 0) e.Add("shop.maxBots는 0 이상이어야 합니다.");
            if (shop.botPrices.Length < shop.maxBots)
                e.Add("shop.botPrices는 최대 봇 수(" + shop.maxBots + ")만큼 필요합니다.");
            if (shop.botUnlockStage < 0 || shop.botUnlockStage >= stages)
                e.Add("shop.botUnlockStage가 지대 단계 범위를 벗어났습니다.");
            if (shop.scaffolds.Count == 0) e.Add("shop.scaffolds가 비어 있습니다.");
            var tiers = new HashSet<int>();
            foreach (ScaffoldDef s in shop.scaffolds)
            {
                if (s == null) { e.Add("shop.scaffolds에 빈 항목이 있습니다."); continue; }
                if (!tiers.Add(s.tier)) e.Add("shop.scaffolds의 tier가 겹칩니다: " + s.tier);
                if (s.basePrice <= 0) e.Add("발판 " + s.tier + "단계의 basePrice는 0보다 커야 합니다.");
            }

            // 작물
            if (crops.Count == 0) e.Add("crops가 비어 있습니다.");
            var cropIds = new HashSet<string>();
            foreach (CropDef c in crops)
            {
                if (c == null || string.IsNullOrEmpty(c.id)) { e.Add("crops에 id가 없는 항목이 있습니다."); continue; }
                if (!cropIds.Add(c.id)) e.Add("crops의 id가 겹칩니다: " + c.id);
                if (c.growSeconds <= 0f) e.Add("작물 " + c.id + "의 growSeconds는 0보다 커야 합니다.");
                if (c.harvestAmount < 1) e.Add("작물 " + c.id + "의 harvestAmount는 1 이상이어야 합니다.");
                if (c.seedPrice < 0 || c.sellPrice < 0) e.Add("작물 " + c.id + "의 가격은 0 이상이어야 합니다.");
                if (c.unlockStage < 0 || c.unlockStage >= stages) e.Add("작물 " + c.id + "의 unlockStage가 지대 단계 범위를 벗어났습니다.");
            }

            // 요리
            if (cooking.maxFallProtectionCharges < 1) e.Add("cooking.maxFallProtectionCharges는 1 이상이어야 합니다.");
            var recipeIds = new HashSet<string>();
            foreach (RecipeDef r in cooking.recipes)
            {
                if (r == null || string.IsNullOrEmpty(r.id)) { e.Add("cooking.recipes에 id가 없는 항목이 있습니다."); continue; }
                if (!recipeIds.Add(r.id)) e.Add("레시피 id가 겹칩니다: " + r.id);
                if (string.IsNullOrEmpty(r.resultItemId)) e.Add("레시피 " + r.id + "에 resultItemId가 없습니다.");
                if (r.ingredients == null || r.ingredients.Count == 0) { e.Add("레시피 " + r.id + "에 재료가 없습니다."); continue; }
                foreach (ItemStack ing in r.ingredients)
                {
                    if (ing == null || string.IsNullOrEmpty(ing.itemId) || ing.count < 1)
                    {
                        e.Add("레시피 " + r.id + "의 재료가 올바르지 않습니다.");
                        continue;
                    }
                    string cropId = ItemIds.CropIdFromCarrot(ing.itemId);
                    if (cropId != null && FindCrop(cropId) == null)
                        e.Add("레시피 " + r.id + "의 재료 " + ing.itemId + "에 해당하는 작물이 없습니다.");
                }
                if (r.effect != null)
                {
                    if (r.effect.type != EffectTypes.FallProtection) e.Add("레시피 " + r.id + "의 효과 종류를 알 수 없습니다: " + r.effect.type);
                    if (r.effect.charges < 1) e.Add("레시피 " + r.id + "의 효과 charges는 1 이상이어야 합니다.");
                }
            }

            if (newGame.startingCoins < 0) e.Add("newGame.startingCoins는 0 이상이어야 합니다.");
            return e;
        }
    }
}
