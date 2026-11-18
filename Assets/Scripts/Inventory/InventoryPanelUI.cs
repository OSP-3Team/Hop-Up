using System.Collections.Generic;
using UnityEngine;

namespace Project.Inventory
{
    public class InventoryPanelUI : MonoBehaviour
    {
        [SerializeField] private InventoryManager inventory;
        [SerializeField] private Hotbar hotbar;
        [SerializeField] private InventorySlotUI slotPrefab;
        [SerializeField] private Transform slotParent;

        private readonly List<InventorySlotUI> slotUIs = new List<InventorySlotUI>();

        private void OnEnable()
        {
            inventory.OnInventoryChanged += Refresh;
            hotbar.OnSelectionChanged += OnSelectionChanged;
            Refresh();
        }

        private void Start()
        {
            // InventoryManager.Awake 이후에 한 번 더 그려서 시작 칸을 반영
            Refresh();
        }

        private void OnDisable()
        {
            inventory.OnInventoryChanged -= Refresh;
            hotbar.OnSelectionChanged -= OnSelectionChanged;
        }

        private void OnSelectionChanged(int index)
        {
            RefreshSelection();
        }

        private void Refresh()
        {
            // 칸이 늘어났으면 UI 칸도 추가
            while (slotUIs.Count < inventory.SlotCount)
            {
                var ui = Instantiate(slotPrefab, slotParent);
                ui.SetKeyLabel(slotUIs.Count);
                slotUIs.Add(ui);
            }

            for (int i = 0; i < slotUIs.Count; i++)
                slotUIs[i].Refresh(inventory.GetSlot(i));

            RefreshSelection();
        }

        private void RefreshSelection()
        {
            for (int i = 0; i < slotUIs.Count; i++)
                slotUIs[i].SetSelected(i == hotbar.SelectedIndex);
        }
    }
}