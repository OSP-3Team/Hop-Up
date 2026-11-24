using System.Collections.Generic;
using UnityEngine;

namespace Project.Inventory
{
    [CreateAssetMenu(fileName = "ItemDatabase", menuName = "Project/Item Database")]
    public class ItemDatabase : ScriptableObject
    {
        [SerializeField] private List<ItemData> items = new List<ItemData>();

        private Dictionary<string, ItemData> lookup;

        public IReadOnlyList<ItemData> Items => items;

        // id로 ItemData를 찾음. 없으면 null
        public ItemData Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (lookup == null) BuildLookup();

            lookup.TryGetValue(id, out ItemData item);
            return item;
        }

        private void OnEnable()
        {
            lookup = null; // 에디터에서 목록을 고쳤을 때 다시 만들도록
        }

        private void OnValidate()
        {
            lookup = null;
        }

#if UNITY_EDITOR
        // 프로젝트에 있는 모든 ItemData를 목록에 자동으로 채움 (에디터 전용)
        [ContextMenu("모든 ItemData 자동으로 채우기")]
        private void CollectAllItems()
        {
            items.Clear();
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:ItemData"))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (item != null) items.Add(item);
            }
            lookup = null;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        private void BuildLookup()
        {
            lookup = new Dictionary<string, ItemData>();
            foreach (var item in items)
            {
                if (item == null || string.IsNullOrEmpty(item.id)) continue;

                if (lookup.ContainsKey(item.id))
                    Debug.LogWarning($"[ItemDatabase] id가 겹칩니다: {item.id}", this);
                else
                    lookup.Add(item.id, item);
            }
        }
    }
}