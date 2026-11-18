using UnityEngine;

namespace Project.Inventory
{
    public class HeldItemView : MonoBehaviour
    {
        [SerializeField] private InventoryManager inventory;
        [SerializeField] private Hotbar hotbar;

        [SerializeField, Tooltip("손 위치. 플레이어 카메라의 자식 오브젝트로 둔다")]
        private Transform holdPoint;

        [SerializeField] private float placeholderSize = 0.2f;

        private ItemData shownItem;
        private GameObject shownObject;

        private void OnEnable()
        {
            hotbar.OnSelectionChanged += OnSelectionChanged;
            inventory.OnInventoryChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            hotbar.OnSelectionChanged -= OnSelectionChanged;
            inventory.OnInventoryChanged -= Refresh;
            Clear();
        }

        private void OnSelectionChanged(int index)
        {
            Refresh();
        }

        // 손에 든 아이템이 바뀐 경우에만 다시 만듦
        private void Refresh()
        {
            ItemData held = hotbar.HeldItem;
            if (held == shownItem) return;

            Clear();
            shownItem = held;
            if (held == null) return;

            if (held.heldPrefab != null)
            {
                shownObject = Instantiate(held.heldPrefab, holdPoint);
                shownObject.transform.localPosition = Vector3.zero;
                shownObject.transform.localRotation = Quaternion.identity;
            }
            else
            {
                shownObject = CreatePlaceholder(held);
            }
        }

        private void Clear()
        {
            if (shownObject != null) Destroy(shownObject);
            shownObject = null;
            shownItem = null;
        }

        private GameObject CreatePlaceholder(ItemData item)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = "HeldPlaceholder";
            obj.transform.SetParent(holdPoint, false);
            obj.transform.localScale = Vector3.one * placeholderSize;

            // 손에 든 물체가 충돌에 영향을 주지 않게 콜라이더 제거
            Destroy(obj.GetComponent<Collider>());

            obj.GetComponent<Renderer>().material.color = GetPlaceholderColor(item.category);
            return obj;
        }

        private static Color GetPlaceholderColor(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.Platform:   return new Color(0.6f, 0.6f, 0.65f); // 회색
                case ItemCategory.Seed:       return new Color(0.3f, 0.8f, 0.3f);  // 초록
                case ItemCategory.Ingredient: return new Color(0.95f, 0.65f, 0.2f); // 주황
                case ItemCategory.Food:       return new Color(0.9f, 0.3f, 0.3f);  // 빨강
                case ItemCategory.Decor:      return new Color(0.5f, 0.4f, 0.9f);  // 보라
                default:                      return Color.white;
            }
        }
    }
}