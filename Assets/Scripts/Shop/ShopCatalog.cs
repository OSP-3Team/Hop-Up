using System.Collections.Generic;
using UnityEngine;

namespace Project.Shop
{
    [CreateAssetMenu(fileName = "ShopCatalog", menuName = "Project/Shop Catalog")]
    public class ShopCatalog : ScriptableObject
    {
        [SerializeField] private List<ShopEntry> entries = new List<ShopEntry>();

        public IReadOnlyList<ShopEntry> Entries => entries;

        // 카테고리에 맞는 항목만 골라서 돌려줌
        public List<ShopEntry> GetEntries(ShopCategory category)
        {
            var result = new List<ShopEntry>();
            foreach (var entry in entries)
                if (entry.category == category) result.Add(entry);
            return result;
        }
    }
}