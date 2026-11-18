using System;

namespace Project.UI
{
    // 열고 닫을 수 있는 UI 창 (상점, 인벤토리·조합 창 등)
    public interface IUIWindow
    {
        bool IsOpen { get; }
        event Action<bool> OnOpenChanged;
        void SetOpen(bool open);
    }
}