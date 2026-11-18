using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Inventory
{
    public class Hotbar : MonoBehaviour
    {
        private static readonly Key[] NumberKeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
            Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0
        };

        [SerializeField] private InventoryManager inventory;

        [SerializeField, Tooltip("이미 선택한 번호를 다시 누르면 빈손으로 돌아간다")]
        private bool deselectOnSamePress = true;

        // -1 = 빈손
        [SerializeField] private int selectedIndex = -1;

        // I·E 창이 열려 있을 때 등에 외부에서 끌 수 있음
        public bool InputEnabled { get; set; } = true;

        // 선택이 바뀔 때 발생 (새 칸 번호, 빈손이면 -1)
        public event Action<int> OnSelectionChanged;

        public int SelectedIndex => selectedIndex;

        public InventorySlot SelectedSlot =>
            selectedIndex < 0 ? null : inventory.GetSlot(selectedIndex);

        // 지금 손에 든 아이템 (빈손이거나 빈 칸이면 null)
        public ItemData HeldItem
        {
            get
            {
                var slot = SelectedSlot;
                return (slot == null || slot.IsEmpty) ? null : slot.item;
            }
        }

        private void Update()
        {
            if (!InputEnabled) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            for (int i = 0; i < NumberKeys.Length; i++)
            {
                if (keyboard[NumberKeys[i]].wasPressedThisFrame)
                {
                    Select(i);
                    break;
                }
            }
        }

        public void Select(int index)
        {
            if (index < 0 || index >= inventory.SlotCount) return; // 없는 칸은 무시

            bool same = index == selectedIndex;
            SetSelected(deselectOnSamePress && same ? -1 : index);
        }

        public void Deselect()
        {
            SetSelected(-1);
        }

        private void SetSelected(int index)
        {
            if (index == selectedIndex) return;
            selectedIndex = index;
            OnSelectionChanged?.Invoke(selectedIndex);
        }
    }
}