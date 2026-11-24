using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using HopUp.FarmEconomy;
using Project.Inventory;

namespace Project.UI
{
    public class RecipeListUI : MonoBehaviour
    {
        [SerializeField] private InventoryManager inventory;
        [SerializeField] private RecipeEntryUI entryPrefab;
        [SerializeField] private Transform entryParent;
        [SerializeField] private TMP_Text messageText;

        private readonly List<RecipeEntryUI> entries = new List<RecipeEntryUI>();
        private EconomyHub hub;

        private void OnEnable()
        {
            hub = EconomyHub.Instance;
            if (hub == null)
            {
                Debug.LogError("[RecipeListUI] 씬에 EconomyHub가 없습니다.", this);
                return;
            }

            BuildEntries();

            inventory.OnInventoryChanged += Refresh;
            hub.Carrots.Changed += Refresh;
            hub.Cooking.Crafted += OnCrafted;

            if (messageText != null) messageText.text = string.Empty;
            Refresh();
        }

        private void OnDisable()
        {
            inventory.OnInventoryChanged -= Refresh;

            if (hub == null) return;
            hub.Carrots.Changed -= Refresh;
            hub.Cooking.Crafted -= OnCrafted;
        }

        private void BuildEntries()
        {
            if (entries.Count > 0) return;

            foreach (RecipeDef recipe in hub.Cooking.Recipes)
            {
                RecipeEntryUI entry = Instantiate(entryPrefab, entryParent);
                entry.Setup(recipe, OnCraftClicked);
                entries.Add(entry);
            }
        }

        private void OnCraftClicked(RecipeDef recipe)
        {
            CraftResult result = hub.Cooking.TryCraft(recipe.id);

            // 인벤토리가 가득 차 바닥에 떨어진 경우는 안내 문구를 띄우지 않음 (CookingService 규칙)
            switch (result.Status)
            {
                case CraftStatus.Success:
                {
                    string message = $"{recipe.name} 제작 완료!";
                    if (result.FirstTime) message += " (새 레시피 공개!)";
                    SetMessage(message);
                    break;
                }
                case CraftStatus.MissingIngredients:
                    SetMessage("재료가 부족해요");
                    break;
                default:
                    SetMessage("만들 수 없는 레시피예요");
                    break;
            }
        }

        private void OnCrafted(RecipeDef recipe)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (hub == null) return;

            foreach (RecipeEntryUI entry in entries)
            {
                RecipeDef recipe = entry.Recipe;
                bool discovered = hub.Cooking.IsDiscovered(recipe.id);

                // 아직 만들어 본 적 없는 레시피는 재료만 보이고 이름·효과는 ?로 가림
                entry.Refresh(
                    discovered ? recipe.name : "?",
                    discovered ? BuildEffectText(recipe) : "효과: ?",
                    BuildIngredientsText(recipe),
                    hub.Cooking.CanCraft(recipe));
            }
        }

        private string BuildIngredientsText(RecipeDef recipe)
        {
            var sb = new StringBuilder("재료: ");
            for (int i = 0; i < recipe.ingredients.Count; i++)
            {
                ItemStack ingredient = recipe.ingredients[i];
                int have = hub.Cooking.CountAvailable(ingredient);
                string text = $"{GetIngredientName(ingredient.itemId)} ×{ingredient.count} (보유 {have})";

                if (i > 0) sb.Append(", ");

                // 모자란 재료는 빨간색
                sb.Append(have >= ingredient.count ? text : $"<color=#FF6666>{text}</color>");
            }
            return sb.ToString();
        }

        private string GetIngredientName(string itemId)
        {
            // 당근 재료는 작물 이름을 씀
            string cropId = ItemIds.CropIdFromCarrot(itemId);
            if (cropId != null)
            {
                CropDef crop = hub.Config.FindCrop(cropId);
                return crop != null ? crop.name : itemId;
            }

            ItemData item = inventory.GetItemData(itemId);
            return item != null ? item.displayName : itemId;
        }

        private static string BuildEffectText(RecipeDef recipe)
        {
            EffectDef effect = recipe.effect;
            if (effect == null) return "효과: 없음";
            if (effect.type == EffectTypes.FallProtection) return $"효과: 추락 방지 ×{effect.charges}";
            return $"효과: {effect.type}";
        }

        private void SetMessage(string message)
        {
            if (messageText != null) messageText.text = message;
        }
    }
}