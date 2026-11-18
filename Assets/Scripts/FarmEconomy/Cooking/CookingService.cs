using System;
using System.Collections.Generic;

namespace HopUp.FarmEconomy
{
    public static class EffectTypes
    {
        public const string FallProtection = "fall_protection";
    }

    public enum CraftStatus { Success, UnknownRecipe, MissingIngredients }
    public enum EatStatus { Success, NotFood, NotInInventory, EffectAtMax }

    public struct CraftResult
    {
        public CraftStatus Status;
        /// <summary>이 레시피를 처음 만들었는가(처음이면 이름·효과를 공개한다).</summary>
        public bool FirstTime;
        /// <summary>인벤토리가 가득 차 바닥에 떨어뜨렸는가(안내 문구는 띄우지 않는다).</summary>
        public bool DroppedOnGround;
        public RecipeDef Recipe;
    }

    /// <summary>
    /// 요리(SYS-08). 흐름도 "음식 제작": 재료 충분? → 재료 소모, 음식 생성, 처음이면 이름·효과 공개 →
    /// 인벤토리에 자리 있으면 추가, 없으면 플레이어 앞 바닥에 드롭(안내 문구 없음).
    /// 음식 효과는 현재 "추락 방지"만 있다: 낙하 판정에서 TryConsumeFallProtection()이 true면 발판 파괴를 건너뛴다.
    /// </summary>
    public sealed class CookingService
    {
        readonly BalanceConfig cfg;
        readonly IInventory inventory;
        readonly CarrotStock stock;
        readonly IItemDropper dropper;
        readonly HashSet<string> discovered = new HashSet<string>();

        public int FallProtectionCharges { get; private set; }

        public event Action<RecipeDef> Crafted;
        public event Action<RecipeDef> RecipeDiscovered;
        public event Action<int> FallProtectionChanged;

        public CookingService(BalanceConfig cfg, IInventory inventory, CarrotStock stock, IItemDropper dropper)
        {
            this.cfg = cfg;
            this.inventory = inventory;
            this.stock = stock;
            this.dropper = dropper;
        }

        public IList<RecipeDef> Recipes { get { return cfg.cooking.recipes; } }
        public IReadOnlyCollection<string> DiscoveredRecipeIds { get { return discovered; } }
        public bool IsDiscovered(string recipeId) { return discovered.Contains(recipeId); }

        /// <summary>재료 하나의 보유량. 당근 재료는 CarrotStock, 그 외는 인벤토리에서 센다.</summary>
        public int CountAvailable(ItemStack ingredient)
        {
            string cropId = ItemIds.CropIdFromCarrot(ingredient.itemId);
            return cropId != null ? stock.Count(cropId) : inventory.CountOf(ingredient.itemId);
        }

        public bool CanCraft(RecipeDef recipe)
        {
            if (recipe == null) return false;
            foreach (ItemStack ing in recipe.ingredients)
                if (CountAvailable(ing) < ing.count) return false;
            return true;
        }

        public CraftResult TryCraft(string recipeId)
        {
            var result = new CraftResult();
            RecipeDef recipe = cfg.FindRecipe(recipeId);
            result.Recipe = recipe;
            if (recipe == null) { result.Status = CraftStatus.UnknownRecipe; return result; }
            if (!CanCraft(recipe)) { result.Status = CraftStatus.MissingIngredients; return result; }

            foreach (ItemStack ing in recipe.ingredients)
            {
                string cropId = ItemIds.CropIdFromCarrot(ing.itemId);
                if (cropId != null) stock.TryRemove(cropId, ing.count);
                else inventory.TryRemove(ing.itemId, ing.count);
            }

            result.FirstTime = discovered.Add(recipe.id);
            if (inventory.TryAdd(recipe.resultItemId, 1) < 1)
            {
                result.DroppedOnGround = true;
                if (dropper != null) dropper.DropInFrontOfPlayer(recipe.resultItemId, 1);
            }
            result.Status = CraftStatus.Success;

            var crafted = Crafted;
            if (crafted != null) crafted(recipe);
            if (result.FirstTime)
            {
                var found = RecipeDiscovered;
                if (found != null) found(recipe);
            }
            return result;
        }

        /// <summary>손에 든 음식을 먹는다(좌클릭). 효과가 이미 최대치면 음식을 소모하지 않는다.</summary>
        public EatStatus TryEat(string foodItemId)
        {
            RecipeDef recipe = FindByResult(foodItemId);
            if (recipe == null) return EatStatus.NotFood;
            if (inventory.CountOf(foodItemId) < 1) return EatStatus.NotInInventory;

            EffectDef eff = recipe.effect;
            if (eff != null && eff.type == EffectTypes.FallProtection)
            {
                int max = cfg.cooking.maxFallProtectionCharges;
                if (FallProtectionCharges >= max) return EatStatus.EffectAtMax;
                inventory.TryRemove(foodItemId, 1);
                SetCharges(Math.Min(max, FallProtectionCharges + Math.Max(1, eff.charges)));
                return EatStatus.Success;
            }

            inventory.TryRemove(foodItemId, 1);
            return EatStatus.Success;
        }

        /// <summary>낙하 판정(이성재님)이 호출: 방지 효과가 있으면 1회 소진하고 true.</summary>
        public bool TryConsumeFallProtection()
        {
            if (FallProtectionCharges <= 0) return false;
            SetCharges(FallProtectionCharges - 1);
            return true;
        }

        RecipeDef FindByResult(string itemId)
        {
            if (itemId == null) return null;
            foreach (RecipeDef r in cfg.cooking.recipes)
                if (r.resultItemId == itemId) return r;
            return null;
        }

        void SetCharges(int value)
        {
            FallProtectionCharges = value;
            var handler = FallProtectionChanged;
            if (handler != null) handler(value);
        }

        public void RestoreState(IEnumerable<string> discoveredIds, int charges)
        {
            discovered.Clear();
            if (discoveredIds != null)
                foreach (string id in discoveredIds)
                    if (cfg.FindRecipe(id) != null) discovered.Add(id);
            FallProtectionCharges = Math.Max(0, Math.Min(charges, cfg.cooking.maxFallProtectionCharges));
            var handler = FallProtectionChanged;
            if (handler != null) handler(FallProtectionCharges);
        }
    }
}
