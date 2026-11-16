using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace HopUp.FarmEconomy.Tests
{
    /// <summary>EditMode 테스트(Window > General > Test Runner > EditMode). 순수 C# 로직만 검증한다.</summary>
    public class FarmEconomyTests
    {
        sealed class RecordingDropper : IItemDropper
        {
            public readonly List<string> Dropped = new List<string>();
            public void DropInFrontOfPlayer(string itemId, int count) { Dropped.Add(itemId + "x" + count); }
        }

        sealed class Rig
        {
            public readonly BalanceConfig Cfg;
            public readonly Wallet Wallet = new Wallet();
            public readonly CarrotStock Stock = new CarrotStock();
            public readonly ZoneProgress Zone;
            public readonly InMemoryInventory Inv;
            public readonly FarmField Field;
            public readonly ShopService Shop;
            public readonly SellService Sales;
            public readonly CookingService Cooking;
            public readonly RecordingDropper Dropper = new RecordingDropper();
            public readonly FarmEconomySaveSection Save;

            public Rig()
            {
                Cfg = BalanceConfigLoader.CreateDefault();
                Zone = new ZoneProgress(Cfg.zones.stationHeights);
                Inv = new InMemoryInventory(Cfg.shop.startSlots, Cfg.shop.maxSlots, Cfg.shop.inventoryStackLimit);
                Field = new FarmField(Cfg, Zone, Inv, Stock);
                Shop = new ShopService(Cfg, Wallet, Zone, Inv, Field);
                Sales = new SellService(Cfg, Stock, Wallet);
                Cooking = new CookingService(Cfg, Inv, Stock, Dropper);
                Save = new FarmEconomySaveSection(Wallet, Stock, Zone, Field, Shop, Cooking);
            }
        }

        // ───────── 설정 ─────────

        [Test]
        public void Config_Default_IsValid()
        {
            BalanceConfig cfg = BalanceConfigLoader.CreateDefault();
            Assert.AreEqual(0, cfg.Validate().Count);
            Assert.AreEqual(3, cfg.crops.Count);
            Assert.AreEqual(4, cfg.StageCount);
        }

        [Test]
        public void Config_StreamingAssetsFile_ParsesAndMatchesDefault()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Balance/farm_economy.json");
            Assert.IsTrue(File.Exists(path), "farm_economy.json이 StreamingAssets/Balance에 있어야 함");
            BalanceConfig fromFile;
            List<string> errors;
            Assert.IsTrue(BalanceConfigLoader.TryParse(File.ReadAllText(path), out fromFile, out errors), string.Join("; ", errors));
            BalanceConfig def = BalanceConfigLoader.CreateDefault();
            Assert.AreEqual(def.crops.Count, fromFile.crops.Count);
            Assert.AreEqual(def.shop.scaffolds[2].basePrice, fromFile.shop.scaffolds[2].basePrice);
        }

        [Test]
        public void Config_BadJson_IsRejected()
        {
            BalanceConfig cfg;
            List<string> errors;
            Assert.IsFalse(BalanceConfigLoader.TryParse("{ not json", out cfg, out errors));
            Assert.IsFalse(BalanceConfigLoader.TryParse("{\"crops\": []}", out cfg, out errors));
            Assert.IsNull(cfg);
            Assert.IsNotEmpty(errors);
        }

        // ───────── 지대·인벤토리 ─────────

        [Test]
        public void Zone_StageForHeight_AndNeverDecreases()
        {
            var z = new ZoneProgress(new float[] { 75f, 150f, 225f });
            Assert.AreEqual(0, z.StageForHeight(74.9f));
            Assert.AreEqual(1, z.StageForHeight(75f));
            Assert.AreEqual(3, z.StageForHeight(300f));
            int raised = 0;
            z.StageUnlocked += s => raised++;
            Assert.IsTrue(z.NotifyHeight(80f));
            Assert.IsFalse(z.NotifyHeight(10f));
            Assert.AreEqual(1, z.Stage);
            Assert.AreEqual(1, raised);
        }

        [Test]
        public void Inventory_StackRules()
        {
            var inv = new InMemoryInventory(2, 10, 10);
            Assert.AreEqual(15, inv.TryAdd("a", 15));
            Assert.AreEqual(5, inv.TryAdd("a", 6));
            Assert.AreEqual(20, inv.CountOf("a"));
            Assert.AreEqual(0, inv.FreeCapacityFor("a"));
            Assert.AreEqual(12, inv.TryRemove("a", 12));
            Assert.AreEqual(8, inv.CountOf("a"));
            Assert.IsFalse(inv.TryExpandSlots(9));
            Assert.IsTrue(inv.TryExpandSlots(8));
            Assert.AreEqual(10, inv.SlotCount);
        }

        // ───────── 상점 ─────────

        [Test]
        public void Shop_ScaffoldPrice_DoublesPerZone()
        {
            var rig = new Rig();
            ShopItem s1 = rig.Shop.Find("scaffold_1");
            Assert.AreEqual(10, rig.Shop.GetUnitPrice(s1));
            rig.Zone.NotifyHeight(75f);
            Assert.AreEqual(20, rig.Shop.GetUnitPrice(s1));
            rig.Zone.NotifyHeight(150f);
            Assert.AreEqual(40, rig.Shop.GetUnitPrice(s1));
            rig.Zone.NotifyHeight(225f);
            Assert.AreEqual(80, rig.Shop.GetUnitPrice(s1));
            Assert.AreEqual(480, rig.Shop.GetUnitPrice(rig.Shop.Find("scaffold_3")));
        }

        [Test]
        public void Shop_PartialPurchase_WhenCoinsRunOut()
        {
            var rig = new Rig();
            rig.Wallet.Add(25);
            PurchaseResult r = rig.Shop.TryPurchase(rig.Shop.Find("scaffold_1"), 5);
            Assert.AreEqual(PurchaseStatus.Partial, r.Status);
            Assert.AreEqual(2, r.Purchased);
            Assert.AreEqual(20, r.CoinsSpent);
            Assert.AreEqual(5, rig.Wallet.Coins);
            Assert.AreEqual(2, rig.Inv.CountOf("scaffold_1"));
        }

        [Test]
        public void Shop_MaxPurchasable_MatchesPurchase()
        {
            var rig = new Rig();
            rig.Wallet.Add(25);
            ShopItem s1 = rig.Shop.Find("scaffold_1");
            int n = rig.Shop.GetMaxPurchasable(s1);
            Assert.AreEqual(2, n);
            Assert.AreEqual(PurchaseStatus.Success, rig.Shop.TryPurchase(s1, n).Status);
        }

        [Test]
        public void Shop_InventoryFull_ChargesNothing()
        {
            var rig = new Rig();
            rig.Wallet.Add(1000);
            rig.Inv.TryAdd("rock", 10);
            PurchaseResult r = rig.Shop.TryPurchase(rig.Shop.Find("scaffold_1"), 1);
            Assert.AreEqual(PurchaseStatus.InventoryFull, r.Status);
            Assert.AreEqual(1000, rig.Wallet.Coins);
        }

        [Test]
        public void Shop_NotEnoughCoins()
        {
            var rig = new Rig();
            rig.Wallet.Add(5);
            Assert.AreEqual(PurchaseStatus.NotEnoughCoins, rig.Shop.TryPurchase(rig.Shop.Find("scaffold_1"), 1).Status);
            Assert.AreEqual(5, rig.Wallet.Coins);
        }

        [Test]
        public void Shop_SeedLockedUntilZoneReached()
        {
            var rig = new Rig();
            rig.Wallet.Add(1000);
            ShopItem golden = rig.Shop.Find("seed_golden");
            Assert.AreEqual(PurchaseStatus.LockedOrLimit, rig.Shop.TryPurchase(golden, 1).Status);
            Assert.AreEqual(0, rig.Shop.GetMaxPurchasable(golden));
            rig.Zone.NotifyHeight(150f);
            Assert.AreEqual(PurchaseStatus.Success, rig.Shop.TryPurchase(golden, 1).Status);
        }

        [Test]
        public void Shop_InventorySlots_StopAtMax()
        {
            var rig = new Rig();
            rig.Wallet.Add(100000);
            ShopItem slot = rig.Shop.Find(ShopService.SlotItemId);
            PurchaseResult r = rig.Shop.TryPurchase(slot, 99);
            Assert.AreEqual(9, r.Purchased);
            Assert.AreEqual(2190, r.CoinsSpent);
            Assert.AreEqual(10, rig.Inv.SlotCount);
            Assert.AreEqual(PurchaseStatus.LockedOrLimit, rig.Shop.TryPurchase(slot, 1).Status);
        }

        [Test]
        public void Shop_Bots_StopAtMax()
        {
            var rig = new Rig();
            rig.Wallet.Add(100000);
            PurchaseResult r = rig.Shop.TryPurchase(rig.Shop.Find(ShopService.BotItemId), 10);
            Assert.AreEqual(4, r.Purchased);
            Assert.AreEqual(3250, r.CoinsSpent);
            Assert.AreEqual(4, rig.Shop.BotsOwned);
        }

        [Test]
        public void Shop_PlotExpansion_FollowsZoneCap()
        {
            var rig = new Rig();
            rig.Wallet.Add(100000);
            ShopItem plot = rig.Shop.Find(ShopService.PlotItemId);
            Assert.AreEqual(4, rig.Shop.TryPurchase(plot, 99).Purchased);
            Assert.AreEqual(8, rig.Field.UnlockedNormalPlots);
            Assert.AreEqual(PurchaseStatus.LockedOrLimit, rig.Shop.TryPurchase(plot, 1).Status);
            rig.Zone.NotifyHeight(75f);
            Assert.AreEqual(4, rig.Shop.TryPurchase(plot, 99).Purchased);
            Assert.AreEqual(12, rig.Field.UnlockedNormalPlots);
        }

        [Test]
        public void Stepper_FollowsFlowchart()
        {
            var st = new PurchaseQuantityStepper();
            st.SetMaxPurchasable(3);
            st.Plus(); st.Plus(); st.Plus(); st.Plus();
            Assert.AreEqual(3, st.Quantity);
            st.Reset();
            st.SetToMax();
            Assert.AreEqual(3, st.Quantity);
            st.SetMaxPurchasable(1);
            Assert.AreEqual(1, st.Quantity);
            st.SetMaxPurchasable(0);
            st.SetToMax();
            Assert.AreEqual(1, st.Quantity);
            st.Minus();
            Assert.AreEqual(1, st.Quantity);
        }

        // ───────── 농장 ─────────

        [Test]
        public void Farm_PlantGrowHarvest()
        {
            var rig = new Rig();
            rig.Inv.TryAdd("seed_normal", 1);
            Assert.AreEqual(PlantResult.Ok, rig.Field.TryPlantFromInventory(0, "seed_normal"));
            Assert.AreEqual(0, rig.Inv.CountOf("seed_normal"));
            rig.Field.Tick(89f);
            Assert.AreEqual(PlotPhase.Growing, rig.Field.GetPlot(0).Phase);
            Assert.AreEqual(HarvestResult.NotReady, rig.Field.HarvestToStock(0));
            rig.Field.Tick(2f);
            Assert.AreEqual(PlotPhase.Ready, rig.Field.GetPlot(0).Phase);
            Assert.AreEqual(HarvestResult.Ok, rig.Field.HarvestToStock(0));
            Assert.AreEqual(1, rig.Stock.Count("normal"));
            Assert.AreEqual(PlotPhase.Empty, rig.Field.GetPlot(0).Phase);
        }

        [Test]
        public void Farm_NeedsSeedAndFreeUnlockedPlot()
        {
            var rig = new Rig();
            Assert.AreEqual(PlantResult.NoSeed, rig.Field.TryPlantFromInventory(0, "seed_normal"));
            rig.Inv.TryAdd("seed_normal", 3);
            Assert.AreEqual(PlantResult.PlotLocked, rig.Field.TryPlantFromInventory(5, "seed_normal"));
            Assert.AreEqual(PlantResult.Ok, rig.Field.TryPlantFromInventory(0, "seed_normal"));
            Assert.AreEqual(PlantResult.NotEmpty, rig.Field.TryPlantFromInventory(0, "seed_normal"));
            Assert.AreEqual(2, rig.Inv.CountOf("seed_normal"));
        }

        [Test]
        public void Farm_GoldenRules()
        {
            var rig = new Rig();
            int goldenPlot = rig.Field.NormalPlotTotal;
            Assert.AreEqual(PlantResult.PlotLocked, rig.Field.TryPlant(goldenPlot, "golden"));
            rig.Zone.NotifyHeight(150f);
            Assert.AreEqual(PlantResult.WrongPlotKind, rig.Field.TryPlant(0, "golden"));
            Assert.AreEqual(PlantResult.WrongPlotKind, rig.Field.TryPlant(goldenPlot, "normal"));
            Assert.AreEqual(PlantResult.BotNotAllowed, rig.Field.TryPlant(goldenPlot, "golden", true));
            Assert.AreEqual(PlantResult.Ok, rig.Field.TryPlant(goldenPlot, "golden"));
            rig.Field.Tick(1000f);
            string crop;
            int amount;
            Assert.AreEqual(HarvestResult.BotNotAllowed, rig.Field.TryHarvest(goldenPlot, true, out crop, out amount));
            Assert.AreEqual(HarvestResult.Ok, rig.Field.TryHarvest(goldenPlot, false, out crop, out amount));
            Assert.AreEqual("golden", crop);
        }

        [Test]
        public void Farm_FastCropLockedUntil75m()
        {
            var rig = new Rig();
            Assert.AreEqual(PlantResult.CropLocked, rig.Field.TryPlant(0, "fast"));
            rig.Zone.NotifyHeight(75f);
            Assert.AreEqual(PlantResult.Ok, rig.Field.TryPlant(0, "fast"));
        }

        [Test]
        public void Farm_WaterShortensGrowthOnce()
        {
            var rig = new Rig();
            Assert.AreEqual(WaterResult.NotGrowing, rig.Field.TryWater(0));
            rig.Field.TryPlant(0, "normal");
            rig.Field.Tick(10f);
            Assert.AreEqual(WaterResult.Ok, rig.Field.TryWater(0));
            Assert.AreEqual(72f, rig.Field.GetPlot(0).GrowSeconds, 0.001f);
            Assert.AreEqual(WaterResult.AlreadyWatered, rig.Field.TryWater(0));
        }

        [Test]
        public void Sales_SellsUpToOwned()
        {
            var rig = new Rig();
            rig.Stock.Add("golden", 2);
            Assert.AreEqual(80, rig.Sales.Sell("golden", 5));
            Assert.AreEqual(80, rig.Wallet.Coins);
            Assert.AreEqual(0, rig.Stock.Count("golden"));
            rig.Stock.Add("normal", 3);
            rig.Stock.Add("fast", 2);
            Assert.AreEqual(27, rig.Sales.SellAll());
            Assert.AreEqual(0, rig.Stock.Total);
        }

        // ───────── 요리 ─────────

        [Test]
        public void Cooking_CraftThenEatThenConsumeProtection()
        {
            var rig = new Rig();
            rig.Stock.Add("normal", 3);
            CraftResult r = rig.Cooking.TryCraft("carrot_soup");
            Assert.AreEqual(CraftStatus.Success, r.Status);
            Assert.IsTrue(r.FirstTime);
            Assert.IsFalse(r.DroppedOnGround);
            Assert.AreEqual(1, rig.Inv.CountOf("food_carrot_soup"));
            Assert.AreEqual(0, rig.Stock.Count("normal"));
            Assert.AreEqual(CraftStatus.MissingIngredients, rig.Cooking.TryCraft("carrot_soup").Status);

            Assert.AreEqual(EatStatus.NotFood, rig.Cooking.TryEat("rock"));
            Assert.AreEqual(EatStatus.Success, rig.Cooking.TryEat("food_carrot_soup"));
            Assert.AreEqual(1, rig.Cooking.FallProtectionCharges);
            Assert.IsTrue(rig.Cooking.TryConsumeFallProtection());
            Assert.IsFalse(rig.Cooking.TryConsumeFallProtection());
        }

        [Test]
        public void Cooking_DropsWhenInventoryFull()
        {
            var rig = new Rig();
            rig.Inv.TryAdd("rock", 10);
            rig.Stock.Add("normal", 3);
            CraftResult r = rig.Cooking.TryCraft("carrot_soup");
            Assert.AreEqual(CraftStatus.Success, r.Status);
            Assert.IsTrue(r.DroppedOnGround);
            CollectionAssert.Contains(rig.Dropper.Dropped, "food_carrot_soupx1");
        }

        [Test]
        public void Cooking_EatRefusedWhenEffectAtMax()
        {
            var rig = new Rig();
            rig.Cooking.RestoreState(null, rig.Cfg.cooking.maxFallProtectionCharges);
            rig.Inv.TryAdd("food_carrot_soup", 1);
            Assert.AreEqual(EatStatus.EffectAtMax, rig.Cooking.TryEat("food_carrot_soup"));
            Assert.AreEqual(1, rig.Inv.CountOf("food_carrot_soup"));
        }

        // ───────── 저장 ─────────

        [Test]
        public void Save_Roundtrip()
        {
            var a = new Rig();
            a.Wallet.Add(77);
            a.Stock.Add("fast", 2);
            a.Zone.NotifyHeight(80f);
            a.Shop.RestoreBots(2);
            a.Inv.TryAdd("seed_normal", 1);
            a.Field.TryPlantFromInventory(0, "seed_normal");
            a.Field.Tick(30f);
            a.Field.ExpandNormalPlots(2);
            a.Stock.Add("normal", 3);
            a.Cooking.TryCraft("carrot_soup");
            a.Cooking.TryEat("food_carrot_soup");

            JObject json = JObject.Parse(a.Save.Capture().ToString());

            var b = new Rig();
            Assert.IsTrue(b.Save.Restore(json));
            Assert.AreEqual(77, b.Wallet.Coins);
            Assert.AreEqual(2, b.Stock.Count("fast"));
            Assert.AreEqual(1, b.Zone.Stage);
            Assert.AreEqual(2, b.Shop.BotsOwned);
            Assert.AreEqual(6, b.Field.UnlockedNormalPlots);
            Assert.AreEqual(PlotPhase.Growing, b.Field.GetPlot(0).Phase);
            Assert.AreEqual(30f, b.Field.GetPlot(0).Elapsed, 0.001f);
            Assert.IsTrue(b.Cooking.IsDiscovered("carrot_soup"));
            Assert.AreEqual(1, b.Cooking.FallProtectionCharges);
        }

        [Test]
        public void Save_CorruptData_IsRejectedWithoutChangingState()
        {
            var rig = new Rig();
            rig.Wallet.Add(10);
            Assert.IsFalse(rig.Save.Restore(JObject.Parse("{\"coins\":\"abc\"}")));
            Assert.IsFalse(rig.Save.Restore(null));
            Assert.AreEqual(10, rig.Wallet.Coins);
        }
    }
}
