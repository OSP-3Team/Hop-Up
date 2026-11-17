using System;
using UnityEngine;

namespace Project.Inventory
{
    public class InventoryTester : MonoBehaviour
    {
        [SerializeField] private InventoryManager inventory;
        [SerializeField] private CarrotStorage carrots;
        [SerializeField] private ItemData platform;
        [SerializeField] private ItemData seed;

        private int failCount;

        [ContextMenu("Run Inventory Tests")]
        private void RunTests()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Play 모드에서 실행해 주세요.");
                return;
            }
            if (inventory == null || carrots == null || platform == null || seed == null)
            {
                Debug.LogError("Inspector에서 4개 항목을 모두 연결해 주세요.");
                return;
            }

            failCount = 0;

            // --- 스택 규칙 ---
            int left = inventory.Add(platform, 10);
            Check(left == 0 && inventory.GetCount(platform) == 10, "발판 10개가 한 칸에 들어간다");
            Check(inventory.GetSlot(0).count == 10, "0번 칸에 10개가 쌓였다");

            left = inventory.Add(platform, 1);
            Check(left == 1 && inventory.GetCount(platform) == 10, "칸이 가득 차면 못 넣은 1개를 돌려준다");

            left = inventory.Add(seed, 1);
            Check(left == 1 && inventory.GetCount(seed) == 0, "다른 종류는 가득 찬 칸에 섞이지 않는다");

            // --- 칸 확장 ---
            Check(inventory.TryExpandSlots(1) && inventory.SlotCount == 2, "칸이 1개 늘어난다");

            left = inventory.Add(seed, 3);
            Check(left == 0 && inventory.GetSlot(1).item == seed && inventory.GetSlot(1).count == 3,
                "씨앗 3개가 새 칸에 들어간다");
            Check(inventory.GetSlot(0).item == platform && inventory.GetSlot(0).count == 10,
                "발판 칸은 그대로다");

            // --- 제거 ---
            Check(!inventory.Remove(seed, 5) && inventory.GetCount(seed) == 3,
                "개수가 모자라면 제거에 실패하고 그대로다");
            Check(inventory.Remove(seed, 3) && inventory.GetSlot(1).IsEmpty,
                "다 빼면 칸이 비워진다");
            Check(inventory.Remove(platform, 10) && inventory.GetSlot(0).IsEmpty && inventory.GetCount(platform) == 0,
                "발판을 모두 빼면 인벤토리가 비워진다");

            // --- 이벤트 ---
            int fired = 0;
            Action handler = () => fired++;
            inventory.OnInventoryChanged += handler;

            inventory.Add(platform, 1);
            Check(fired == 1, "추가하면 이벤트가 1번 발생한다");
            inventory.Remove(platform, 99);
            Check(fired == 1, "실패한 제거는 이벤트를 보내지 않는다");
            inventory.Remove(platform, 1);
            Check(fired == 2, "제거하면 이벤트가 발생한다");

            inventory.OnInventoryChanged -= handler;

            // --- 당근 ---
            carrots.Add(CarrotType.Normal, 5);
            Check(carrots.GetCount(CarrotType.Normal) == 5, "일반 당근 5개가 보관된다");

            bool allEmpty = true;
            for (int i = 0; i < inventory.SlotCount; i++)
                if (!inventory.GetSlot(i).IsEmpty) allEmpty = false;
            Check(allEmpty, "당근을 얻어도 인벤토리 칸을 차지하지 않는다");

            Check(!carrots.Remove(CarrotType.Normal, 9) && carrots.GetCount(CarrotType.Normal) == 5,
                "당근이 모자라면 제거에 실패하고 그대로다");

            int carrotFired = 0;
            Action carrotHandler = () => carrotFired++;
            carrots.OnCarrotsChanged += carrotHandler;
            bool removed = carrots.Remove(CarrotType.Normal, 5);
            carrots.OnCarrotsChanged -= carrotHandler;
            Check(removed && carrots.GetCount(CarrotType.Normal) == 0 && carrotFired == 1,
                "당근을 빼면 0이 되고 이벤트가 발생한다");

            // --- 칸 상한 ---
            Check(inventory.TryExpandSlots(8) && inventory.SlotCount == 10, "최대 10칸까지 늘어난다");
            Check(!inventory.TryExpandSlots(1), "10칸을 넘길 수 없다");

            if (failCount == 0) Debug.Log("모든 테스트 통과");
            else Debug.LogError($"{failCount}개 테스트 실패");
        }

        [ContextMenu("Add Sample Items")]
        private void AddSampleItems()
        {
            if (!Application.isPlaying) return;

            inventory.TryExpandSlots(2);
            inventory.Add(platform, 13);
            inventory.Add(seed, 4);
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