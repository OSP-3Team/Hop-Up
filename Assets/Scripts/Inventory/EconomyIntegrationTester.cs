using UnityEngine;
using HopUp.FarmEconomy;

namespace Project.Inventory
{
    public class EconomyIntegrationTester : MonoBehaviour
    {
        private int failCount;

        [ContextMenu("Run Economy Integration Tests")]
        private void RunTests()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Play 모드에서 실행해 주세요.");
                return;
            }

            EconomyHub hub = EconomyHub.Instance;
            if (hub == null)
            {
                Debug.LogError("씬에 EconomyHub가 없습니다.");
                return;
            }

            failCount = 0;
            const string recipeId = "carrot_soup";
            const string soupId = "food_carrot_soup";
            string seedId = ItemIds.Seed("normal");

            // --- 연결과 새 게임 시작 아이템 ---
            Check(hub.Inventory is InventoryManager, "EconomyHub가 우리 InventoryManager를 쓰고 있다");
            Check(hub.Inventory.CountOf(seedId) == 3,
                "시작 씨앗 3개가 인벤토리에 들어 있다 (Apply New Game On Awake가 켜져 있어야 함)");

            // --- 요리 ---
            CookingService cooking = hub.Cooking;
            RecipeDef recipe = hub.Config.FindRecipe(recipeId);
            Check(recipe != null, "레시피 carrot_soup가 설정에 있다");
            if (recipe == null)
            {
                Finish();
                return;
            }

            Check(!cooking.IsDiscovered(recipeId), "처음에는 레시피가 공개되지 않았다");
            Check(!cooking.CanCraft(recipe), "당근이 없으면 만들 수 없다");

            hub.Carrots.Add("normal", 3);
            Check(cooking.CanCraft(recipe), "일반 당근 3개가 있으면 만들 수 있다");

            // 시작 칸은 씨앗이 차지하고 있으니 결과가 들어갈 칸을 하나 늘림
            Check(hub.Inventory.TryExpandSlots(1), "칸을 1개 늘린다");

            CraftResult result = cooking.TryCraft(recipeId);
            Check(result.Status == CraftStatus.Success && result.FirstTime,
                "처음 만들면 성공하고 FirstTime이다");
            Check(hub.Carrots.Count("normal") == 0, "당근 3개가 소모된다");
            Check(hub.Inventory.CountOf(soupId) == 1 && !result.DroppedOnGround,
                "당근 수프가 인벤토리에 들어온다");
            Check(cooking.IsDiscovered(recipeId), "만들고 나면 레시피가 공개된다");
            Check(cooking.TryCraft(recipeId).Status == CraftStatus.MissingIngredients,
                "당근이 없으면 다시 만들 수 없다");

            // --- 먹기 ---
            Check(cooking.TryEat(soupId) == EatStatus.Success, "당근 수프를 먹을 수 있다");
            Check(hub.Inventory.CountOf(soupId) == 0, "먹으면 수프가 사라진다");
            Check(cooking.FallProtectionCharges == 1, "추락 방지 효과가 1회 생긴다");
            Check(cooking.TryConsumeFallProtection() && cooking.FallProtectionCharges == 0,
                "추락 시 효과가 1회 소진된다");

            Finish();
        }

        private void Finish()
        {
            if (failCount == 0) Debug.Log("모든 연동 테스트 통과");
            else Debug.LogError($"{failCount}개 테스트 실패");
        }

        private void Check(bool ok, string label)
        {
            if (ok)
            {
                Debug.Log($"PASS: {label}");
            }
            else
            {
                failCount++;
                Debug.LogError($"FAIL: {label}");
            }
        }
    }
}