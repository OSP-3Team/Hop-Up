using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Project.Inventory
{
    public class InventoryWindowGridUI : MonoBehaviour
    {
        [SerializeField] private InventoryManager inventory;
        [SerializeField] private InventorySlotUI slotPrefab;

        [SerializeField, Tooltip("칸들이 들어갈 부모 (Grid Layout Group이 붙은 오브젝트)")]
        private Transform slotParent;

        [SerializeField] private TMP_Text detailText;

        private readonly List<InventorySlotUI> slotUIs = new List<InventorySlotUI>();
        private int selectedIndex = -1;

        public int SelectedIndex => selectedIndex;

        public InventorySlot SelectedSlot =>
            selectedIndex < 0 ? null : inventory.GetSlot(selectedIndex);

        // 칸을 클릭해서 선택했을 때 발생 (칸 번호)
        public event Action<int> OnSlotSelected;

        private void OnEnable()
        {
            inventory.OnInventoryChanged += Refresh;
            Refresh(); // 창이 닫혀 있는 동안 바뀐 내용을 열 때 따라잡음
        }

        private void OnDisable()
        {
            inventory.OnInventoryChanged -= Refresh;
        }

        private void Refresh()
        {
            // 칸이 늘어났으면 UI 칸도 추가
            while (slotUIs.Count < inventory.SlotCount)
            {
                int index = slotUIs.Count;
                InventorySlotUI ui = Instantiate(slotPrefab, slotParent);
                ui.SetKeyLabel(index);

                var clickable = ui.GetComponent<InventorySlotClickable>();
                if (clickable != null)
                    clickable.Setup(index, Select);
                else
                    Debug.LogWarning("슬롯 프리팹에 InventorySlotClickable이 없어서 클릭 선택이 동작하지 않습니다.", slotPrefab);

                slotUIs.Add(ui);
            }

            for (int i = 0; i < slotUIs.Count; i++)
            {
                slotUIs[i].Refresh(inventory.GetSlot(i));
                slotUIs[i].SetSelected(i == selectedIndex);
            }

            RefreshDetail();
        }

        private void Select(int index)
        {
            selectedIndex = index;

            for (int i = 0; i < slotUIs.Count; i++)
                slotUIs[i].SetSelected(i == selectedIndex);

            RefreshDetail();
            OnSlotSelected?.Invoke(index);
        }

        private void RefreshDetail()
        {
            if (detailText == null) return;

            InventorySlot slot = SelectedSlot;
            if (selectedIndex < 0)
                detailText.text = "칸을 클릭해서 아이템을 선택하세요";
            else if (slot == null || slot.IsEmpty)
                detailText.text = "빈 칸";
            else
                detailText.text = $"{slot.item.displayName} ×{slot.count}";
        }
    }
}