using System;
using System.Collections.Generic;
using UnityEngine;
using HopUp.FarmEconomy;

namespace Project.Inventory
{
    [DefaultExecutionOrder(-300)]
    public class InventoryManager : MonoBehaviour, IInventory
    {
        public const int MaxSlotCount = 10;

        // 인벤토리 내용이나 칸 수가 바뀔 때마다 발생
        public event Action OnInventoryChanged;

        [SerializeField, Range(1, MaxSlotCount)]
        private int startSlotCount = 1;

        [SerializeField]
        private List<InventorySlot> slots = new List<InventorySlot>();

        [SerializeField, Tooltip("아이템 id(문자열)로 ItemData를 찾는 목록")]
        private ItemDatabase itemDatabase;

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

        // 지정한 칸에서 amount개 제거 (손에 든 칸에서 쓸 때 사용: 발판 설치, Q 버리기 등)
        // 칸이 비었거나 개수가 모자라면 아무것도 지우지 않고 false
        public bool RemoveFromSlot(int index, int amount = 1)
        {
            var slot = GetSlot(index);
            if (slot == null || slot.IsEmpty || amount <= 0 || slot.count < amount) return false;

            slot.count -= amount;
            if (slot.count <= 0) slot.Clear();

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

        // ---- IInventory 구현: FarmEconomy가 문자열 아이템 id로 접근하는 통로 ----

        public int MaxSlots => MaxSlotCount;

        public int CountOf(string itemId)
        {
            return GetCount(FindItem(itemId));
        }

        // 실제로 넣은 개수를 반환 (자리가 모자라면 넣을 수 있는 만큼만)
        public int TryAdd(string itemId, int count)
        {
            ItemData item = FindItem(itemId);
            if (item == null || count <= 0) return 0;

            int leftover = Add(item, count);
            return count - leftover;
        }

        // 실제로 뺀 개수를 반환 (모자라면 있는 만큼만)
        public int TryRemove(string itemId, int count)
        {
            ItemData item = FindItem(itemId);
            if (item == null || count <= 0) return 0;

            int removable = Mathf.Min(count, GetCount(item));
            if (removable <= 0) return 0;

            Remove(item, removable);
            return removable;
        }

        // 이 아이템을 지금 더 넣을 수 있는 개수
        public int FreeCapacityFor(string itemId)
        {
            ItemData item = FindItem(itemId);
            if (item == null) return 0;

            int space = 0;
            foreach (var slot in slots)
            {
                if (slot.IsEmpty) space += item.maxStack;
                else if (slot.item == item) space += item.maxStack - slot.count;
            }
            return space;
        }

        // 아이템 id로 ItemData를 찾음 (화면 표시용). 없으면 null
        public ItemData GetItemData(string itemId)
        {
            return itemDatabase != null ? itemDatabase.Find(itemId) : null;
        }

        private ItemData FindItem(string itemId)
        {
            if (itemDatabase == null)
            {
                Debug.LogError("[InventoryManager] Item Database가 연결되지 않았습니다.", this);
                return null;
            }

            ItemData item = itemDatabase.Find(itemId);
            if (item == null)
                Debug.LogWarning($"[InventoryManager] 알 수 없는 아이템 id: {itemId}", this);
            return item;
        }
        
    }
}