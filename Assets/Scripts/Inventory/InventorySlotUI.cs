using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Inventory
{
    public class InventorySlotUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text countText;

        // 슬롯 내용에 맞게 아이콘과 개수를 그림
        public void Refresh(InventorySlot slot)
        {
            if (slot == null || slot.IsEmpty)
            {
                iconImage.enabled = false;
                countText.text = string.Empty;
                return;
            }

            iconImage.enabled = true;
            iconImage.sprite = slot.item.icon;
            countText.text = slot.count > 1 ? slot.count.ToString() : string.Empty;
        }
    }
}