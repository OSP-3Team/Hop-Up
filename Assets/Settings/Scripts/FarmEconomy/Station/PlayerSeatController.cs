using System;
using UnityEngine;

namespace HopUp.FarmEconomy
{
    /// <summary>
    /// 플레이어에 붙이는 앉기 상태. 흐름도: 앉아 있는 동안(IsSeated)에는 클릭·Q·숫자키 입력을 무시한다 →
    /// 플레이어 입력 코드(이성재님)가 IsSeated를 확인해야 한다. 이동·점프 입력이 들어오면 입력 코드가 StandUp()을 호출해 일어난다.
    /// 저장할 때는 SaveSafePosition을 쓴다(앉은 상태는 선 상태로 저장).
    /// </summary>
    public sealed class PlayerSeatController : MonoBehaviour
    {
        [Tooltip("플레이어가 CharacterController를 쓰면 연결(앉는 동안 비활성화)")]
        [SerializeField] CharacterController characterController;
        [Tooltip("플레이어가 Rigidbody를 쓰면 연결(앉는 동안 kinematic)")]
        [SerializeField] Rigidbody body;

        StationBench bench;

        public bool IsSeated { get; private set; }
        public StationBench CurrentBench { get { return bench; } }

        public event Action<StationBench> Sat;
        public event Action Stood;

        /// <summary>세이브에 넣을 위치: 앉아 있으면 일어났을 때의 위치.</summary>
        public Vector3 SaveSafePosition
        {
            get { return IsSeated && bench != null ? bench.StandPosition : transform.position; }
        }

        internal bool Sit(StationBench target)
        {
            if (IsSeated || target == null) return false;
            bench = target;
            if (characterController != null) characterController.enabled = false;
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.isKinematic = true;
            }
            transform.SetPositionAndRotation(target.SeatPosition, target.SeatRotation);
            IsSeated = true;
            var handler = Sat;
            if (handler != null) handler(target);
            return true;
        }

        public void StandUp()
        {
            if (!IsSeated) return;
            StationBench was = bench;
            transform.position = was != null ? was.StandPosition : transform.position;
            if (body != null) body.isKinematic = false;
            if (characterController != null) characterController.enabled = true;
            IsSeated = false;
            bench = null;
            if (was != null) was.Release(this);
            var handler = Stood;
            if (handler != null) handler();
        }
    }
}
