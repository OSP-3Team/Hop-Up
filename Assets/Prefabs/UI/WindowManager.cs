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

        [SerializeField, Tooltip("켜면 창 하나를 열 때 열려 있던 다른 창을 닫는다")]
        private bool onlyOneWindowOpen = true;

        [SerializeField] private bool logChanges = true;

        private readonly List<IUIWindow> windows = new List<IUIWindow>();
        private readonly Dictionary<IUIWindow, Action<bool>> handlers =
            new Dictionary<IUIWindow, Action<bool>>();

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
            {
                IUIWindow captured = window; // 어느 창의 이벤트인지 기억하기 위한 복사
                Action<bool> handler = open => OnWindowOpenChanged(captured, open);
                handlers[window] = handler;
                window.OnOpenChanged += handler;
            }
        }

        private void OnDisable()
        {
            foreach (var pair in handlers)
                pair.Key.OnOpenChanged -= pair.Value;
            handlers.Clear();
        }

        private void OnWindowOpenChanged(IUIWindow changed, bool open)
        {
            // 한 번에 창 하나만: 새로 열렸으면 나머지는 닫음
            if (open && onlyOneWindowOpen)
            {
                foreach (var other in windows)
                {
                    if (other != changed && other.IsOpen)
                        other.SetOpen(false);
                }
            }

            RefreshState();
        }

        private void RefreshState()
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