using System;
using System.Collections.Generic;

namespace HopUp.FarmEconomy
{
    /// <summary>수확한 당근 창고(작물 ID별 개수). 인벤토리와는 별개로 센다.</summary>
    public sealed class CarrotStock
    {
        readonly Dictionary<string, int> counts = new Dictionary<string, int>();

        public event Action Changed;

        public int Total
        {
            get
            {
                int sum = 0;
                foreach (int n in counts.Values) sum += n;
                return sum;
            }
        }

        public int Count(string cropId)
        {
            int n;
            return cropId != null && counts.TryGetValue(cropId, out n) ? n : 0;
        }

        public void Add(string cropId, int amount)
        {
            if (string.IsNullOrEmpty(cropId) || amount <= 0) return;
            counts[cropId] = Count(cropId) + amount;
            Raise();
        }

        /// <summary>count개가 모두 있을 때만 빼고 true. 모자라면 아무것도 빼지 않고 false.</summary>
        public bool TryRemove(string cropId, int count)
        {
            if (count < 0 || Count(cropId) < count) return false;
            if (count == 0) return true;
            int left = Count(cropId) - count;
            if (left == 0) counts.Remove(cropId);
            else counts[cropId] = left;
            Raise();
            return true;
        }

        /// <summary>저장용 복사본(0개인 항목은 빼고).</summary>
        public Dictionary<string, int> Snapshot()
        {
            var copy = new Dictionary<string, int>();
            foreach (KeyValuePair<string, int> kv in counts)
                if (kv.Value > 0) copy[kv.Key] = kv.Value;
            return copy;
        }

        public void Restore(IDictionary<string, int> data)
        {
            counts.Clear();
            if (data != null)
                foreach (KeyValuePair<string, int> kv in data)
                    if (!string.IsNullOrEmpty(kv.Key) && kv.Value > 0) counts[kv.Key] = kv.Value;
            Raise();
        }

        void Raise()
        {
            var handler = Changed;
            if (handler != null) handler();
        }
    }
}
