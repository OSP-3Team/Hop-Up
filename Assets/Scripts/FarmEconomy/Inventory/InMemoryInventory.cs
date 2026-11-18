using System;
using System.Collections.Generic;

namespace HopUp.FarmEconomy
{
    /// <summary>
    /// 임시 인벤토리(순수 C#). 칸 하나에 같은 아이템을 stackLimit개까지 담는다.
    /// 칸 배치는 기억하지 않고 아이템별 개수만 센다(칸은 항상 빈틈없이 채운다고 본다).
    /// </summary>
    public sealed class InMemoryInventory : IInventory
    {
        readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        readonly int maxSlots;
        readonly int stackLimit;

        public event Action Changed;

        public InMemoryInventory(int startSlots, int maxSlots, int stackLimit)
        {
            this.stackLimit = Math.Max(1, stackLimit);
            this.maxSlots = Math.Max(1, maxSlots);
            SlotCount = Math.Min(Math.Max(1, startSlots), this.maxSlots);
        }

        public int SlotCount { get; private set; }
        public int MaxSlots { get { return maxSlots; } }

        public int CountOf(string itemId)
        {
            int n;
            return itemId != null && counts.TryGetValue(itemId, out n) ? n : 0;
        }

        int SlotsUsedByOthers(string itemId)
        {
            int used = 0;
            foreach (KeyValuePair<string, int> kv in counts)
            {
                if (kv.Key == itemId) continue;
                used += (kv.Value + stackLimit - 1) / stackLimit;
            }
            return used;
        }

        public int FreeCapacityFor(string itemId)
        {
            if (itemId == null) return 0;
            long free = (long)(SlotCount - SlotsUsedByOthers(itemId)) * stackLimit - CountOf(itemId);
            return free < 0 ? 0 : (int)Math.Min(free, int.MaxValue);
        }

        public int TryAdd(string itemId, int count)
        {
            if (itemId == null || count <= 0) return 0;
            int add = Math.Min(count, FreeCapacityFor(itemId));
            if (add <= 0) return 0;
            counts[itemId] = CountOf(itemId) + add;
            RaiseChanged();
            return add;
        }

        public int TryRemove(string itemId, int count)
        {
            if (itemId == null || count <= 0) return 0;
            int have = CountOf(itemId);
            int removed = Math.Min(count, have);
            if (removed <= 0) return 0;
            if (have - removed == 0) counts.Remove(itemId);
            else counts[itemId] = have - removed;
            RaiseChanged();
            return removed;
        }

        public bool TryExpandSlots(int count)
        {
            if (count <= 0 || SlotCount + count > maxSlots) return false;
            SlotCount += count;
            RaiseChanged();
            return true;
        }

        void RaiseChanged()
        {
            var handler = Changed;
            if (handler != null) handler();
        }
    }
}
