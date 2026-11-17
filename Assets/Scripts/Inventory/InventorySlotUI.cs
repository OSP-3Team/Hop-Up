using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Inventory
{
    public class InventorySlotUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private TMP_Text keyLabelText;
        [SerializeField] private Outline selectionOutline;

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

        // 칸 번호 표시: 인덱스 0~8 → 1~9, 인덱스 9 → 0
        public void SetKeyLabel(int index)
        {
            if (keyLabelText == null) return;
            keyLabelText.text = ((index + 1) % 10).ToString();
        }

        // 선택된 칸이면 테두리를 켬
        public void SetSelected(bool selected)
        {
            if (selectionOutline == null) return;
            selectionOutline.enabled = selected;
        }
    }
}