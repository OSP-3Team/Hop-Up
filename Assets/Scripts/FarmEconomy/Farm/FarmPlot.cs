using System.Collections.Generic;
using UnityEngine;

namespace HopUp.FarmEconomy
{
    /// <summary>
    /// 씬에 놓는 밭 한 칸(뷰 + 상호작용 창구). 모델(PlotModel)은 EconomyHub.Field가 가진다.
    /// 일반 밭 칸 수(기본 16) + 황금 전용 칸 수(기본 2)만큼 배치하고 plotIndex를 0부터 차례로 맞춘다
    /// (일반 칸 0~15, 황금 칸 16~17). 잠긴 칸은 lockedVisual이 켜진다.
    /// 플레이어 상호작용 코드(이성재님)는 FindNearest로 대상을 찾고 TryPlant/TryHarvest/TryWater만 호출하면 된다.
    /// </summary>
    public sealed class FarmPlot : MonoBehaviour
    {
        static readonly List<FarmPlot> Registry = new List<FarmPlot>();

        [SerializeField] int plotIndex;
        [Header("상태별 비주얼(없으면 비워도 됨)")]
        [SerializeField] GameObject lockedVisual;
        [SerializeField] GameObject emptyVisual;
        [SerializeField] GameObject growingVisual;
        [SerializeField] GameObject readyVisual;
        [Tooltip("자라는 동안 0.2~1로 스케일이 커질 오브젝트(선택)")]
        [SerializeField] Transform growScaleTarget;

        EconomyHub hub;
        bool bound;

        public int PlotIndex { get { return plotIndex; } }
        public PlotModel Model { get; private set; }
        public bool IsUnlocked { get { return bound && hub.Field.IsUnlocked(Model); } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Registry.Clear(); }

        void OnEnable()
        {
            Registry.Add(this);
            TryBind();
        }

        void Start() { TryBind(); }

        void OnDisable()
        {
            Registry.Remove(this);
            Unbind();
        }

        void TryBind()
        {
            if (bound) return;
            hub = EconomyHub.Instance;
            if (hub == null || hub.Field == null) return;
            Model = hub.Field.GetPlot(plotIndex);
            if (Model == null)
            {
                Debug.LogWarning("[FarmPlot] plotIndex " + plotIndex + "에 해당하는 칸이 없습니다(총 " + hub.Field.PlotCount + "칸): " + name, this);
                return;
            }
            hub.Field.PlotChanged += OnPlotChanged;
            hub.Field.LayoutChanged += Refresh;
            bound = true;
            Refresh();
        }

        void Unbind()
        {
            if (!bound) return;
            hub.Field.PlotChanged -= OnPlotChanged;
            hub.Field.LayoutChanged -= Refresh;
            bound = false;
        }

        void OnPlotChanged(PlotModel changed)
        {
            if (changed == Model) Refresh();
        }

        void Refresh()
        {
            if (!bound) return;
            bool unlocked = hub.Field.IsUnlocked(Model);
            SetActive(lockedVisual, !unlocked);
            SetActive(emptyVisual, unlocked && Model.Phase == PlotPhase.Empty);
            SetActive(growingVisual, unlocked && Model.Phase == PlotPhase.Growing);
            SetActive(readyVisual, unlocked && Model.Phase == PlotPhase.Ready);
            if (growScaleTarget != null && Model.Phase != PlotPhase.Growing)
                growScaleTarget.localScale = Vector3.one * (Model.Phase == PlotPhase.Ready ? 1f : 0.2f);
        }

        void Update()
        {
            if (bound && growScaleTarget != null && Model.Phase == PlotPhase.Growing)
                growScaleTarget.localScale = Vector3.one * Mathf.Lerp(0.2f, 1f, Model.Progress01);
        }

        static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active) go.SetActive(active);
        }

        // ───────── 플레이어 상호작용 창구 ─────────

        public PlantResult TryPlant(string seedItemId)
        {
            if (!bound) return PlantResult.NoSuchPlot;
            return hub.Field.TryPlantFromInventory(plotIndex, seedItemId);
        }

        public HarvestResult TryHarvest()
        {
            if (!bound) return HarvestResult.NoSuchPlot;
            return hub.Field.HarvestToStock(plotIndex);
        }

        /// <summary>물주기(SYS-15, 이성재님 담당)가 사용할 창구.</summary>
        public WaterResult TryWater()
        {
            if (!bound) return WaterResult.NoSuchPlot;
            return hub.Field.TryWater(plotIndex);
        }

        /// <summary>plotIndex로 씬의 밭 칸을 찾는다(농사 봇이 이동 목표를 잡을 때 사용). 없으면 null.</summary>
        public static FarmPlot Find(int index)
        {
            return Registry.Find(p => p.plotIndex == index);
        }

        /// <summary>from에서 maxDistance(기본 1.5m, 흐름도 기준) 안의 가장 가까운 열린 밭 칸. 없으면 null.</summary>
        public static FarmPlot FindNearest(Vector3 from, float maxDistance = 1.5f)
        {
            FarmPlot best = null;
            float bestSqr = maxDistance * maxDistance;
            for (int i = 0; i < Registry.Count; i++)
            {
                FarmPlot p = Registry[i];
                if (!p.IsUnlocked) continue;
                float sqr = (p.transform.position - from).sqrMagnitude;
                if (sqr <= bestSqr) { bestSqr = sqr; best = p; }
            }
            return best;
        }
    }
}
