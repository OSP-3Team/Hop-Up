using UnityEngine;

namespace Project.UI
{
    /// <summary>창(상점·인벤토리)이 열려 있으면 조작 안내를 숨긴다.</summary>
    public class HudControlHintUI : MonoBehaviour
    {
        [SerializeField] private WindowManager windowManager;
        [SerializeField] private GameObject hintRoot;

        private void Start()
        {
            if (windowManager == null) windowManager = FindAnyObjectByType<WindowManager>();
            if (windowManager == null) return;

            windowManager.OnAnyWindowOpenChanged += OnWindowOpenChanged;
            OnWindowOpenChanged(windowManager.IsAnyWindowOpen);
        }

        private void OnDestroy()
        {
            if (windowManager != null) windowManager.OnAnyWindowOpenChanged -= OnWindowOpenChanged;
        }

        private void OnWindowOpenChanged(bool anyOpen)
        {
            if (hintRoot != null) hintRoot.SetActive(!anyOpen);
        }
    }
}