using System;
using UnityEngine;

namespace Project.Inventory
{
    public class CarrotStorage : MonoBehaviour
    {
        private static readonly int TypeCount = Enum.GetValues(typeof(CarrotType)).Length;

        // 당근 보유량이 바뀔 때마다 발생
        public event Action OnCarrotsChanged;

        // 순서: 0 = 일반, 1 = 빠른, 2 = 황금
        [SerializeField]
        private int[] counts = new int[TypeCount];

        private void Awake()
        {
            if (counts == null || counts.Length != TypeCount)
                Array.Resize(ref counts, TypeCount);
        }

        public int GetCount(CarrotType type)
        {
            return counts[(int)type];
        }

        public void Add(CarrotType type, int amount)
        {
            if (amount <= 0) return;
            counts[(int)type] += amount;
            OnCarrotsChanged?.Invoke();
        }

        // 개수가 모자라면 아무것도 빼지 않고 false
        public bool Remove(CarrotType type, int amount)
        {
            if (amount <= 0 || counts[(int)type] < amount) return false;
            counts[(int)type] -= amount;
            OnCarrotsChanged?.Invoke();
            return true;
        }
    }
}