using UnityEngine;

namespace Project.Inventory
{
    public enum ItemCategory
    {
        Platform,   // 발판
        Seed,       // 씨앗
        Ingredient, // 재료
        Food,       // 음식
        Decor       // 꾸미기
    }

    [CreateAssetMenu(fileName = "Item_", menuName = "Project/Item Data")]
    public class ItemData : ScriptableObject
    {
        [Tooltip("세이브에 쓰는 고유 ID. 겹치면 안 되고, 만든 뒤에는 바꾸지 않는다.")]
        public string id;

        public string displayName;
        public Sprite icon;
        public ItemCategory category;

        [Min(1)]
        public int maxStack = 10;

        private void OnValidate()
        {
            // id를 비워 두면 에셋 파일 이름으로 자동 채움
            if (string.IsNullOrEmpty(id))
                id = name;
        }
    }
}