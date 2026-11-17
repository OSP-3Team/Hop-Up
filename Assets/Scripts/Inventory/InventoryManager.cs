using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Inventory
{
    public class InventoryManager : MonoBehaviour
    {
        public const int MaxSlotCount = 10;

        // 인벤토리 내용이나 칸 수가 바뀔 때마다 발생
        public event Action OnInventoryChanged;

        [SerializeField, Range(1, MaxSlotCount)]
        private int startSlotCount = 1;

        [SerializeField]
        private List<InventorySlot> slots = new List<InventorySlot>();

        public int SlotCount => slots.Count;

        private void Awake()
        {
            while (slots.Count < startSlotCount)
                slots.Add(new InventorySlot());
        }

        // 해당 칸 가져오기 (없는 번호면 null). 6.2 단축키에서 사용
        public InventorySlot GetSlot(int index)
        {
            if (index < 0 || index >= slots.Count) return null;
            return slots[index];
        }

        // 해당 아이템의 총 보유 개수
        public int GetCount(ItemData item)
        {
            if (item == null) return 0;

            int total = 0;
            foreach (var slot in slots)
                if (slot.item == item) total += slot.count;
            return total;
        }

        // 아이템 추가. 못 넣은 수량을 돌려줌 (0이면 전부 넣은 것)
        public int Add(ItemData item, int amount)
        {
            if (item == null || amount <= 0) return Mathf.Max(amount, 0);

            int requested = amount;

            // 1) 같은 종류가 있는 칸부터 채우기
            foreach (var slot in slots)
            {
                if (amount <= 0) break;
                if (slot.item != item) continue;

                int moved = Mathf.Min(item.maxStack - slot.count, amount);
                if (moved <= 0) continue;

                slot.count += moved;
                amount -= moved;
            }

            // 2) 남으면 빈 칸 사용
            foreach (var slot in slots)
            {
                if (amount <= 0) break;
                if (!slot.IsEmpty) continue;

                int moved = Mathf.Min(item.maxStack, amount);
                slot.item = item;
                slot.count = moved;
                amount -= moved;
            }

            // 하나라도 들어갔을 때만 알림
            if (amount < requested) OnInventoryChanged?.Invoke();

            return amount;
        }

        // 아이템 제거. 개수가 모자라면 아무것도 지우지 않고 false
        public bool Remove(ItemData item, int amount)
        {
            if (item == null || amount <= 0) return false;
            if (GetCount(item) < amount) return false;

            for (int i = slots.Count - 1; i >= 0 && amount > 0; i--)
            {
                var slot = slots[i];
                if (slot.item != item) continue;

                int taken = Mathf.Min(slot.count, amount);
                slot.count -= taken;
                amount -= taken;
                if (slot.count <= 0) slot.Clear();
            }

            OnInventoryChanged?.Invoke();
            return true;
        }

        // 칸 늘리기 (최대 10칸). 상한을 넘으면 false
        public bool TryExpandSlots(int amount = 1)
        {
            if (amount <= 0 || slots.Count + amount > MaxSlotCount) return false;

            for (int i = 0; i < amount; i++)
                slots.Add(new InventorySlot());

            OnInventoryChanged?.Invoke();
            return true;
        }
    }
}