using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Project.Inventory
{
    public class InventorySlotClickable : MonoBehaviour, IPointerClickHandler
    {
        private int index;
        private Action<int> onClick;

        public void Setup(int slotIndex, Action<int> handler)
        {
            index = slotIndex;
            onClick = handler;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // 마우스 왼쪽 클릭만 인정
            if (eventData.button != PointerEventData.InputButton.Left) return;
            onClick?.Invoke(index);
        }
    }
}