using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Core
{
    /// <summary>
    /// 새로하기로 게임 씬에 들어왔을 때 맵을 보여 주는 오프닝 카메라 연출.
    /// 상공(Start Point, 비우면 플레이어 위 Start Height)에서 출발한 카메라가 원래 1인칭 시점까지 내려온 뒤 조작을 돌려준다.
    /// Space/Esc로 건너뛸 수 있다. 이어하기면 아무것도 하지 않는다.
    ///
    /// MainGame 씬의 빈 오브젝트에 부착하고, 오프닝 동안 끌 컴포넌트를 배열에 넣는다:
    /// PlayerController, PlatformPlacer, Hotbar, InventoryWindowUI, ShopWindowUI
    /// </summary>
    public class OpeningSequence : MonoBehaviour
    {
        [SerializeField, Tooltip("오프닝 동안 끌 컴포넌트 (PlayerController, PlatformPlacer, Hotbar, InventoryWindowUI, ShopWindowUI)")]
        private Behaviour[] disableDuringOpening;

        [SerializeField, Tooltip("카메라 출발 위치·방향 (빈 오브젝트). 비우면 플레이어 시점 바로 위 Start Height에서 아래를 내려다봄")]
        private Transform startPoint;

        [SerializeField, Tooltip("Start Point가 비어 있을 때 출발 높이 (플레이어 시점 기준 위로 몇 m)")]
        private float startHeight = 300f;

        [SerializeField, Tooltip("내려오는 데 걸리는 시간(초)")]
        private float duration = 6f;

        private async void Start()
        {
            if (!GameStartRequest.ShouldPlayOpening) return;

            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[Opening] MainCamera 태그가 붙은 카메라가 없어 오프닝을 생략합니다.", this);
                return;
            }

            // 카메라를 부모에서 떼지 않음: PlayerController가 꺼져 있는 동안 부모가 움직이지 않으므로 월드 자세를 되돌리면 원래 자세가 됨
            Transform camTr = cam.transform;
            Vector3 endPos = camTr.position;
            Quaternion endRot = camTr.rotation;
            Vector3 startPos = startPoint != null ? startPoint.position : endPos + Vector3.up * startHeight;
            Quaternion startRot = startPoint != null ? startPoint.rotation : Quaternion.LookRotation(Vector3.down, camTr.forward);

            SetControls(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            try
            {
                for (float t = 0f; t < duration && !SkipPressed(); t += Time.deltaTime)
                {
                    float k = Mathf.SmoothStep(0f, 1f, t / duration);
                    camTr.SetPositionAndRotation(Vector3.Lerp(startPos, endPos, k), Quaternion.Slerp(startRot, endRot, k));
                    await Awaitable.NextFrameAsync(destroyCancellationToken);
                }

                camTr.SetPositionAndRotation(endPos, endRot);
                // 한 프레임 뒤에 켬: 스킵에 쓴 Space가 같은 프레임에 PlayerController에서 점프로 읽히지 않게
                await Awaitable.NextFrameAsync(destroyCancellationToken);
            }
            catch (OperationCanceledException)
            {
                return; // 연출 중 씬이 바뀌어 파괴됨
            }

            SetControls(true);
        }

        private static bool SkipPressed()
        {
            Keyboard kb = Keyboard.current;
            return kb != null && (kb.spaceKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame);
        }

        private void SetControls(bool on)
        {
            if (disableDuringOpening == null) return;
            foreach (Behaviour b in disableDuringOpening)
                if (b != null) b.enabled = on;
        }
    }
}
