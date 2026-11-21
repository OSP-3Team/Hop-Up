using UnityEngine;

namespace Project.Inventory
{
    /// <summary>
    /// 인벤토리(InventoryManager + Hotbar)와 발판 설치(PlatformPlacer)를 이어주는 다리.
    /// 기존 인벤토리 스크립트는 건드리지 않고, 이 스크립트가 IPlatformInventory 약속만 대신 구현합니다.
    ///
    /// 사용법: Game 씬의 빈 오브젝트에 붙이고 Inventory, Hotbar 칸을 연결합니다.
    /// </summary>
    public class PlatformInventoryBridge : MonoBehaviour, IPlatformInventory
    {
        [SerializeField] private InventoryManager inventory;
        [SerializeField] private Hotbar hotbar;

        private void OnEnable()
        {
            PlatformInventoryLocator.Register(this);
        }

        private void OnDisable()
        {
            PlatformInventoryLocator.Unregister(this);
        }

        // 지금 손에 든 아이템이 "발판" 종류인가? (빈손이거나 빈 칸이면 false)
        public bool IsHoldingPlatform
        {
            get
            {
                if (hotbar == null) return false;

                ItemData held = hotbar.HeldItem;
                return held != null && held.category == ItemCategory.Platform;
            }
        }

        // 손에 든 칸에서 발판 1개 소모. 0개가 되면 InventoryManager가 칸을 비워서 빈손이 됨
        public bool TryConsumeHeldPlatform()
        {
            if (inventory == null || hotbar == null) return false;
            if (!IsHoldingPlatform) return false;

            return inventory.RemoveFromSlot(hotbar.SelectedIndex, 1);
        }
    }
}
