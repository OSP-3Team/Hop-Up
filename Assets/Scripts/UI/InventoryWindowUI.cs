using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.UI
{
    public class InventoryWindowUI : MonoBehaviour, IUIWindow
    {
        [SerializeField, Tooltip("열고 닫을 패널. 이 스크립트가 붙은 오브젝트와 달라야 한다")]
        private GameObject windowRoot;

        public bool IsOpen => windowRoot.activeSelf;

        // 창이 열리고 닫힐 때 발생 (열렸으면 true)
        public event Action<bool> OnOpenChanged;

        private void Awake()
        {
            windowRoot.SetActive(false);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.iKey.wasPressedThisFrame)
                SetOpen(!IsOpen);
        }

        public void SetOpen(bool open)
        {
            if (windowRoot.activeSelf == open) return;

            windowRoot.SetActive(open);
            OnOpenChanged?.Invoke(open);
        }
    }
}