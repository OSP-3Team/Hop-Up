using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Project.Core
{
    /// <summary>
    /// 오프닝 씬 전용 카메라 연출. 메인 메뉴에서 새로하기를 누르면 이 씬이 먼저 열린다.
    /// 카메라가 Start Point에서 End Point로 이동한 뒤 Next Scene(게임 씬)을 불러온다.
    /// Space/Esc로 건너뛸 수 있다.
    ///
    /// 오프닝 씬의 빈 오브젝트에 부착. 씬에는 MainCamera 태그가 붙은 카메라와
    /// 시작·끝 위치를 나타낼 빈 오브젝트 두 개(Start Point, End Point)가 있어야 함.
    /// </summary>
    public class OpeningSequence : MonoBehaviour
    {
        [SerializeField, Tooltip("카메라가 출발하는 위치·방향")]
        private Transform startPoint;

        [SerializeField, Tooltip("카메라가 도착하는 위치·방향")]
        private Transform endPoint;

        [SerializeField, Tooltip("이동하는 데 걸리는 시간(초)")]
        private float duration = 6f;

        [SerializeField, Tooltip("오프닝이 끝나면 불러올 게임 씬 이름. Build Profiles의 Scene List에 있어야 함")]
        private string nextSceneName = "MainGame";

        private async void Start()
        {
            // 게임 씬에 잘못 붙어 있으면 자기 씬을 계속 다시 불러오게 되므로 막음
            if (SceneManager.GetActiveScene().name == nextSceneName)
            {
                Debug.LogWarning($"[Opening] {nextSceneName} 씬 안에 붙어 있어 동작하지 않습니다. 오프닝 씬으로 옮기세요.", this);
                return;
            }

            Camera cam = Camera.main;
            if (cam == null || startPoint == null || endPoint == null)
            {
                Debug.LogWarning("[Opening] 카메라 또는 Start/End Point가 없어 연출 없이 게임 씬으로 넘어갑니다.", this);
            }
            else
            {
                Transform camTr = cam.transform;
                try
                {
                    for (float t = 0f; t < duration && !SkipPressed(); t += Time.deltaTime)
                    {
                        float k = Mathf.SmoothStep(0f, 1f, t / duration);
                        camTr.SetPositionAndRotation(
                            Vector3.Lerp(startPoint.position, endPoint.position, k),
                            Quaternion.Slerp(startPoint.rotation, endPoint.rotation, k));
                        await Awaitable.NextFrameAsync(destroyCancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    return; // 연출 중 플레이 종료 등으로 파괴됨
                }
            }

            SceneManager.LoadScene(nextSceneName);
        }

        private static bool SkipPressed()
        {
            Keyboard kb = Keyboard.current;
            return kb != null && (kb.spaceKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame);
        }
    }
}
