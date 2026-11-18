using System.Collections.Generic;
using UnityEngine;

namespace HopUp.FarmEconomy
{
    /// <summary>
    /// 정거장 벤치(SYS-16). 좌클릭 판정에서 대상이 "앉기"(벤치)면 TrySit을 호출한다.
    /// seatAnchor: 앉은 위치·방향, standAnchor: 일어났을 때 서는 위치(없으면 벤치 앞쪽 1m).
    /// </summary>
    public sealed class StationBench : MonoBehaviour
    {
        static readonly List<StationBench> Registry = new List<StationBench>();

        [SerializeField] Transform seatAnchor;
        [SerializeField] Transform standAnchor;

        PlayerSeatController occupant;

        public bool IsOccupied { get { return occupant != null; } }

        public Vector3 SeatPosition { get { return (seatAnchor != null ? seatAnchor : transform).position; } }
        public Quaternion SeatRotation { get { return (seatAnchor != null ? seatAnchor : transform).rotation; } }

        public Vector3 StandPosition
        {
            get { return standAnchor != null ? standAnchor.position : transform.position + transform.forward; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Registry.Clear(); }

        void OnEnable() { Registry.Add(this); }

        void OnDisable()
        {
            Registry.Remove(this);
            if (occupant != null) occupant.StandUp();
        }

        public bool TrySit(PlayerSeatController player)
        {
            if (player == null || IsOccupied || player.IsSeated) return false;
            if (!player.Sit(this)) return false;
            occupant = player;
            return true;
        }

        /// <summary>PlayerSeatController가 일어날 때 호출한다.</summary>
        internal void Release(PlayerSeatController player)
        {
            if (occupant == player) occupant = null;
        }

        /// <summary>from에서 maxDistance(기본 1.5m) 안의 비어 있는 가장 가까운 벤치. 없으면 null.</summary>
        public static StationBench FindNearest(Vector3 from, float maxDistance = 1.5f)
        {
            StationBench best = null;
            float bestSqr = maxDistance * maxDistance;
            for (int i = 0; i < Registry.Count; i++)
            {
                StationBench b = Registry[i];
                if (b.IsOccupied) continue;
                float sqr = (b.transform.position - from).sqrMagnitude;
                if (sqr <= bestSqr) { bestSqr = sqr; best = b; }
            }
            return best;
        }
    }
}
