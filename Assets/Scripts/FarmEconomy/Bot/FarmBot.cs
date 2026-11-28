using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HopUp.FarmEconomy
{
    /// <summary>
    /// 씬에 놓는 농사 봇 한 대(AI-01~03 상태 머신). 데이터는 EconomyHub.Bots.Get(botIndex)가 가진다.
    /// 최대 봇 수(기본 4)만큼 배치하고 botIndex를 0부터 맞춘다. 보유하지 않은 번호의 봇은 visualRoot를 끄고 쉰다.
    /// 상태: 대기 → (예약) → 이동 → 심기/수확 → 대기. 다음 할 일은 대기에서만 고른다.
    /// 플레이어 상호작용 코드는 FindNearest로 봇을 찾고 InsertSeeds/CollectCarrots/Describe만 부르면 된다.
    /// 이동: 출발 위치에 NavMesh가 있으면 NavMeshAgent로 장애물을 돌아가고(Tools/Hop-Up/Bake Bot NavMesh), 없으면 직선 이동.
    /// </summary>
    public sealed class FarmBot : MonoBehaviour
    {
        public enum State { Idle, Moving, Planting, Harvesting }

        static readonly List<FarmBot> Registry = new List<FarmBot>();
        // 칸 index → 예약한 봇. 여러 봇이 같은 칸으로 몰리지 않게 한다
        static readonly Dictionary<int, FarmBot> Reserved = new Dictionary<int, FarmBot>();

        [SerializeField] int botIndex;
        [Tooltip("보유한 봇일 때만 켜지는 모델 루트(선택)")]
        [SerializeField] GameObject visualRoot;
        [Tooltip("머리 위 문구(선택). 씨앗이 없으면 \"씨앗 없음\"")]
        [SerializeField] TextMesh statusLabel;
        [SerializeField] bool showStateInLabel;

        [Header("동작 (프로토타입 임시값)")]
        [SerializeField] float moveSpeed = 2f;
        [SerializeField] float workSeconds = 1f;
        [SerializeField] float thinkInterval = 0.5f;
        [SerializeField] float moveTimeout = 10f;
        [SerializeField] float arriveDistance = 0.2f;

        EconomyHub hub;
        BotModel model;
        NavMeshAgent agent;  // null이면 직선 이동(NavMesh 없는 씬·PlayMode 테스트)
        NavMeshPath pathBuffer;  // 필드 초기화로 만들면 Unity가 막음(생성자 호출 금지) → SetupAgent에서
        int target = -1;
        bool harvestJob;
        float timer;

        public int BotIndex { get { return botIndex; } }
        public State Current { get; private set; }
        public BotModel Model { get { return model; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Registry.Clear();
            Reserved.Clear();
        }

        void OnEnable() { Registry.Add(this); }

        void OnDisable()
        {
            Registry.Remove(this);
            EnterIdle();
        }

        void Start()
        {
            hub = EconomyHub.Instance;
            if (hub == null || hub.Bots == null)
            {
                Debug.LogWarning("[FarmBot] EconomyHub가 없어 봇이 동작하지 않습니다: " + name, this);
                enabled = false;
                return;
            }
            hub.Field.PlotChanged += OnPlotChanged;
            hub.Field.LayoutChanged += Recheck;
            hub.Bots.BotChanged += OnBotChanged;
            SetupAgent();
        }

        // 프리팹·씬마다 붙이지 않도록 런타임에 붙인다. NavMesh 없이 AddComponent하면 에러 로그가 나서 먼저 확인
        void SetupAgent()
        {
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(transform.position, out hit, 2f, NavMesh.AllAreas)) return;
            agent = GetComponent<NavMeshAgent>();
            if (agent == null)
            {
                float pivotHeight = transform.position.y - hit.position.y;  // 붙이는 순간 바닥으로 붙으므로 먼저 잰다
                agent = gameObject.AddComponent<NavMeshAgent>();
                agent.baseOffset = pivotHeight;  // 피벗 높이 유지(캡슐은 중심이 피벗)
            }
            pathBuffer = new NavMeshPath();
            agent.speed = moveSpeed;
            agent.stoppingDistance = arriveDistance * 0.5f;  // 도착 판정(remainingDistance <= arriveDistance)보다 안쪽에서 멈추게
            agent.autoBraking = true;
        }

        bool AgentReady { get { return agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh; } }

        void OnDestroy()
        {
            if (hub == null || hub.Bots == null) return;
            hub.Field.PlotChanged -= OnPlotChanged;
            hub.Field.LayoutChanged -= Recheck;
            hub.Bots.BotChanged -= OnBotChanged;
        }

        void Update()
        {
            model = hub.Bots.Get(botIndex);  // 구매·불러오기로 보유 여부가 바뀔 수 있음
            if (visualRoot != null && visualRoot.activeSelf != (model != null)) visualRoot.SetActive(model != null);
            if (model == null && Current != State.Idle) EnterIdle();  // 에이전트를 끄기 전에 경로를 지운다
            // 보유하지 않은 봇(보이지 않음)이 다른 봇의 회피 대상이 되지 않게 끈다
            if (agent != null && agent.enabled != (model != null)) agent.enabled = model != null;
            if (model == null)
            {
                UpdateLabel();
                return;
            }

            timer += Time.deltaTime;
            switch (Current)
            {
                case State.Idle:
                    if (timer >= thinkInterval) Think();
                    break;
                case State.Moving:
                    MoveStep();
                    break;
                case State.Planting:
                case State.Harvesting:
                    if (timer >= workSeconds) DoWork();
                    break;
            }
            UpdateLabel();
        }

        // ───────── 상태 머신 ─────────

        // T1(예약 성공 → 이동) / T7(할 일 없음 → 대기 유지). 수확을 심기보다 먼저 본다
        void Think()
        {
            timer = 0f;
            if (TryReserve(true)) return;
            if (model.SeedCount > 0) TryReserve(false);
        }

        bool TryReserve(bool harvest)
        {
            int best = -1;
            float bestSqr = float.MaxValue;
            foreach (PlotModel p in hub.Field.AllPlots)
            {
                if (!IsTarget(p, harvest) || Reserved.ContainsKey(p.Index)) continue;
                FarmPlot view = FarmPlot.Find(p.Index);
                if (view == null) continue;
                float sqr = (view.transform.position - transform.position).sqrMagnitude;
                // 길이 막힌 칸(발판에 둘러싸임 등)은 건너뛴다. 안 그러면 0.5초마다 예약→포기를 반복함
                if (sqr < bestSqr && Reachable(view.transform.position)) { bestSqr = sqr; best = p.Index; }
            }
            if (best < 0) return false;

            Reserved[best] = this;
            target = best;
            harvestJob = harvest;
            Current = State.Moving;
            timer = 0f;
            if (AgentReady) agent.SetDestination(NavGoal(FarmPlot.Find(best).transform.position));
            return true;
        }

        bool Reachable(Vector3 plotPos)
        {
            if (!AgentReady) return true;  // 직선 이동은 경로 검사 없음
            return NavMesh.CalculatePath(agent.nextPosition, NavGoal(plotPos), agent.areaMask, pathBuffer)
                && pathBuffer.status == NavMeshPathStatus.PathComplete;
        }

        // 밭 중심이 NavMesh 위에 딱 있지 않을 수 있어(밭 상자 윗면·높이 차) 가장 가까운 NavMesh 점으로 옮긴다
        Vector3 NavGoal(Vector3 plotPos)
        {
            NavMeshHit hit;
            return NavMesh.SamplePosition(plotPos, out hit, 1f, agent.areaMask) ? hit.position : plotPos;
        }

        // 황금 전용 칸·잠긴 칸은 봇 대상이 아니다
        bool IsTarget(PlotModel p, bool harvest)
        {
            return p != null
                && p.Kind == PlotKind.Normal
                && hub.Field.IsUnlocked(p)
                && p.Phase == (harvest ? PlotPhase.Ready : PlotPhase.Empty);
        }

        // T2/T3(도착) / T6(칸이 사라짐·시간 초과)
        void MoveStep()
        {
            FarmPlot view = FarmPlot.Find(target);
            if (view == null || timer > moveTimeout) { EnterIdle(); return; }

            if (AgentReady)
            {
                if (agent.pathPending) return;
                // 가는 중에 발판이 길을 막으면(carving) 경로가 끊긴다 → 포기하고 다음 판단에서 다른 칸을 고름
                if (agent.pathStatus != NavMeshPathStatus.PathComplete) { EnterIdle(); return; }
                if (agent.remainingDistance > arriveDistance) return;
                agent.ResetPath();  // 작업하는 동안 미끄러지지 않게
            }
            else
            {
                Vector3 goal = view.transform.position;
                goal.y = transform.position.y;
                transform.position = Vector3.MoveTowards(transform.position, goal, moveSpeed * Time.deltaTime);
                if ((goal - transform.position).sqrMagnitude > arriveDistance * arriveDistance) return;
            }

            Current = harvestJob ? State.Harvesting : State.Planting;
            timer = 0f;
        }

        // T4/T5. 실패(플레이어가 먼저 건드림 등)하면 아무것도 바꾸지 않고 대기로
        void DoWork()
        {
            if (harvestJob)
            {
                string cropId;
                int amount;
                if (hub.Field.TryHarvest(target, true, out cropId, out amount) == HarvestResult.Ok)
                    hub.Bots.AddHarvest(model, cropId, amount);
            }
            else if (model.SeedCount > 0 && hub.Field.TryPlant(target, model.SeedCropId, true) == PlantResult.Ok)
            {
                hub.Bots.ConsumeSeed(model);
            }
            EnterIdle();
        }

        // 예약 해제는 여기 한 곳. 작업 끝·포기·비활성화가 모두 이곳을 지난다
        void EnterIdle()
        {
            FarmBot owner;
            if (target >= 0 && Reserved.TryGetValue(target, out owner) && owner == this)
                Reserved.Remove(target);
            target = -1;
            Current = State.Idle;
            timer = 0f;
            if (AgentReady) agent.ResetPath();
        }

        void OnPlotChanged(PlotModel changed) { Recheck(); }

        void OnBotChanged(BotModel changed)
        {
            if (changed == model) Recheck();
        }

        // 대기 중이면 다음 프레임에 재판단(이벤트 안에서 바로 예약하지 않음: TryPlant 안에서 불리는 재진입 방지).
        // 이동 중이면 목표 칸이 아직 유효한지 확인(T6). 불러오기는 PlotChanged 없이 LayoutChanged만 오므로 둘 다 듣는다
        void Recheck()
        {
            if (Current == State.Idle) timer = thinkInterval;
            else if (Current == State.Moving && !IsTarget(hub.Field.GetPlot(target), harvestJob)) EnterIdle();
        }

        void UpdateLabel()
        {
            if (statusLabel == null) return;
            string text = model == null ? string.Empty : (model.SeedCount == 0 ? "씨앗 없음" : string.Empty);
            if (showStateInLabel && model != null) text = Current + (text.Length > 0 ? "\n" + text : string.Empty);
            if (statusLabel.text != text) statusLabel.text = text;
        }

        // ───────── 플레이어 상호작용 창구 ─────────

        public SeedInsertResult InsertSeeds(string seedItemId, int heldCount)
        {
            return hub != null ? hub.Bots.InsertSeeds(botIndex, seedItemId, heldCount) : SeedInsertResult.NoSuchBot;
        }

        public int CollectCarrots()
        {
            return hub != null ? hub.Bots.CollectCarrots(botIndex) : 0;
        }

        /// <summary>에임 안내 문구. 예: "씨앗 12/30 당근 · 당근 38".</summary>
        public string Describe()
        {
            return hub != null ? hub.Bots.Describe(botIndex) : string.Empty;
        }

        /// <summary>from에서 maxDistance(기본 1.5m) 안의 가장 가까운 보유 봇. 없으면 null.</summary>
        public static FarmBot FindNearest(Vector3 from, float maxDistance = 1.5f)
        {
            FarmBot best = null;
            float bestSqr = maxDistance * maxDistance;
            for (int i = 0; i < Registry.Count; i++)
            {
                FarmBot b = Registry[i];
                if (b.model == null) continue;
                float sqr = (b.transform.position - from).sqrMagnitude;
                if (sqr <= bestSqr) { bestSqr = sqr; best = b; }
            }
            return best;
        }

        // ───────── 개발용 컨텍스트 메뉴(플레이 중 인스펙터 ⋮ 메뉴). 구매·플레이어 상호작용이 붙기 전 씬 확인용 ─────────

        [ContextMenu("Debug: 이 봇 보유하기")]
        void DebugOwn()
        {
            if (hub != null) hub.Shop.RestoreBots(Mathf.Max(hub.Shop.BotsOwned, botIndex + 1));
        }

        [ContextMenu("Debug: 일반 씨앗 10개 넣기")]
        void DebugInsertNormal() { DebugInsert("seed_normal"); }

        [ContextMenu("Debug: 빠른 씨앗 10개 넣기")]
        void DebugInsertFast() { DebugInsert("seed_fast"); }

        void DebugInsert(string seedItemId)
        {
            if (hub == null) return;
            hub.Inventory.TryAdd(seedItemId, 10);
            Debug.Log("[FarmBot " + botIndex + "] " + InsertSeeds(seedItemId, 10) + " → " + Describe(), this);
        }

        [ContextMenu("Debug: 당근 수령")]
        void DebugCollect()
        {
            if (hub == null) return;
            int n = CollectCarrots();
            Debug.Log("[FarmBot " + botIndex + "] 당근 " + n + "개 수령, 창고 합계 " + hub.Carrots.Total, this);
        }

        [ContextMenu("Debug: 밭 전부 다 자라게")]
        void DebugRipenAll()
        {
            if (hub != null) hub.Field.Tick(100000f);
        }
    }
}
