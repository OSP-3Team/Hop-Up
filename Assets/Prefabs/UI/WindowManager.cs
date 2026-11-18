using System;
using System.Collections.Generic;
using UnityEngine;
using Project.Inventory;

namespace Project.UI
{
    public class WindowManager : MonoBehaviour
    {
        [SerializeField, Tooltip("관리할 창들 (IUIWindow를 구현한 컴포넌트)")]
        private MonoBehaviour[] windowComponents;

        [SerializeField] private Hotbar hotbar;

        [SerializeField] private bool logChanges = true;

        private readonly List<IUIWindow> windows = new List<IUIWindow>();
        private bool anyOpen;
        private CursorLockMode savedLockState;
        private bool savedCursorVisible;

        public bool IsAnyWindowOpen => anyOpen;

        // 열린 창이 하나도 없다가 생기거나, 모두 닫힐 때 발생 (열린 창이 있으면 true)
        public event Action<bool> OnAnyWindowOpenChanged;

        private void Awake()
        {
            foreach (var component in windowComponents)
            {
                if (component is IUIWindow window)
                    windows.Add(window);
                else
                    Debug.LogWarning($"{component.name}은 IUIWindow가 아니라서 무시합니다", component);
            }
        }

        private void OnEnable()
        {
            foreach (var window in windows)
                window.OnOpenChanged += OnWindowOpenChanged;
        }

        private void OnDisable()
        {
            foreach (var window in windows)
                window.OnOpenChanged -= OnWindowOpenChanged;
        }

        private void OnWindowOpenChanged(bool open)
        {
            bool nowOpen = false;
            foreach (var window in windows)
            {
                if (window.IsOpen)
                {
                    nowOpen = true;
                    break;
                }
            }

            if (nowOpen == anyOpen) return; // 열린 창이 있다/없다 상태가 그대로면 할 일 없음
            anyOpen = nowOpen;

            if (hotbar != null) hotbar.InputEnabled = !anyOpen;

            if (anyOpen)
            {
                // 커서 상태를 기억해 두고 풀어 줌
                savedLockState = Cursor.lockState;
                savedCursorVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                // 원래 상태로 되돌림
                Cursor.lockState = savedLockState;
                Cursor.visible = savedCursorVisible;
            }

            if (logChanges)
                Debug.Log($"[WindowManager] 창 열림={anyOpen}, 커서={Cursor.lockState}");

            OnAnyWindowOpenChanged?.Invoke(anyOpen);
        }

        // 테스트용: 점 세 개(⋮) 메뉴에서 실행하면 1인칭처럼 커서가 잠김
        [ContextMenu("Lock Cursor (test)")]
        private void LockCursorForTest()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}