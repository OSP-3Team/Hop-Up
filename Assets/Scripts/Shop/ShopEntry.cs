using System;
using UnityEngine;
using Project.Inventory;

namespace Project.Shop
{
    [Serializable]
    public class ShopEntry
    {
        [Tooltip("구매 처리에서 항목을 구분하는 고유 이름 (예: platform_10)")]
        public string id;

        public ShopCategory category;
        public string displayName;
        public Sprite icon;

        [Min(0)] public int price;

        [Tooltip("아이템을 파는 항목이면 연결 (확장·판매 항목은 비워 둠)")]
        public ItemData item;

        [Tooltip("판매 탭 항목이면 당근 종류")]
        public CarrotType carrotType;
    }
}