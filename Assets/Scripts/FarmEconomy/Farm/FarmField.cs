using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace HopUp.FarmEconomy
{
    public enum PlotKind { Normal, Golden }
    public enum PlotPhase { Empty, Growing, Ready }

    public enum PlantResult { Ok, NoSuchPlot, PlotLocked, NotEmpty, UnknownCrop, CropLocked, WrongPlotKind, BotNotAllowed, NoSeed }
    public enum HarvestResult { Ok, NoSuchPlot, PlotLocked, NothingPlanted, NotReady, BotNotAllowed }
    public enum WaterResult { Ok, NoSuchPlot, PlotLocked, NotGrowing, AlreadyWatered }

    /// <summary>밭 한 칸의 상태. 변경은 FarmField를 통해서만 한다.</summary>
    public sealed class PlotModel
    {
        public int Index { get; internal set; }
        public PlotKind Kind { get; internal set; }
        public PlotPhase Phase { get; internal set; }
        public string CropId { get; internal set; }
        public float Elapsed { get; internal set; }
        public float GrowSeconds { get; internal set; }
        public bool Watered { get; internal set; }

        public float Progress01
        {
            get
            {
                if (Phase == PlotPhase.Ready) return 1f;
                if (Phase == PlotPhase.Empty || GrowSeconds <= 0f) return 0f;
                float p = Elapsed / GrowSeconds;
                return p < 0f ? 0f : (p > 1f ? 1f : p);
            }
        }

        public float RemainingSeconds
        {
            get { return Phase == PlotPhase.Growing ? Math.Max(0f, GrowSeconds - Elapsed) : 0f; }
        }

        internal void Clear()
        {
            Phase = PlotPhase.Empty;
            CropId = null;
            Elapsed = 0f;
            GrowSeconds = 0f;
            Watered = false;
        }
    }

    /// <summary>세이브용 밭 상태. 비어 있는 칸은 저장하지 않는다.</summary>
    public sealed class PlotSnapshot
    {
        public int index;
        [JsonConverter(typeof(StringEnumConverter))]
        public PlotPhase phase;
        public string cropId;
        public float elapsed;
        public float growSeconds;
        public bool watered;
    }

    /// <summary>
    /// 농장 모델(SYS-06 심기, SYS-07 수확, SYS-13 밭 확장, SYS-14 당근 종류, SYS-15 물주기 훅).
    /// 칸 배열은 고정이다: 일반 칸 [0, NormalPlotTotal) 뒤에 황금 전용 칸이 붙는다. 씬에는 이 개수만큼 FarmPlot을 두고
    /// plotIndex를 맞춘다. 열려 있는지는 IsUnlocked가 결정한다(일반: 구매한 칸 수, 황금: 지대 단계).
    /// 두 층의 API: TryPlant/TryHarvest는 인벤토리·창고를 건드리지 않는 저수준(농사 봇이 사용),
    /// TryPlantFromInventory/HarvestToStock은 플레이어용 고수준이다.
    /// </summary>
    public sealed class FarmField
    {
        readonly BalanceConfig cfg;
        readonly ZoneProgress zone;
        readonly IInventory inventory;
        readonly CarrotStock stock;
        readonly PlotModel[] plots;
        readonly int normalPlotTotal;
        int unlockedNormal;

        public event Action<PlotModel> PlotChanged;
        /// <summary>열린 칸 수·지대 단계 변화, 불러오기 후 전체 갱신이 필요할 때.</summary>
        public event Action LayoutChanged;

        public FarmField(BalanceConfig cfg, ZoneProgress zone, IInventory inventory, CarrotStock stock)
        {
            this.cfg = cfg;
            this.zone = zone;
            this.inventory = inventory;
            this.stock = stock;

            normalPlotTotal = cfg.farm.plotCapByStage.Max();
            int golden = Math.Max(0, cfg.farm.goldenPlotCount);
            plots = new PlotModel[normalPlotTotal + golden];
            for (int i = 0; i < plots.Length; i++)
            {
                plots[i] = new PlotModel
                {
                    Index = i,
                    Kind = i < normalPlotTotal ? PlotKind.Normal : PlotKind.Golden,
                    Phase = PlotPhase.Empty
                };
            }
            unlockedNormal = Math.Min(Math.Max(0, cfg.farm.startPlotCount), normalPlotTotal);
            zone.StageUnlocked += OnStageUnlocked;
        }

        public int PlotCount { get { return plots.Length; } }
        public int NormalPlotTotal { get { return normalPlotTotal; } }
        public int UnlockedNormalPlots { get { return unlockedNormal; } }

        /// <summary>현재 지대 단계에서 살 수 있는 일반 밭 칸 상한.</summary>
        public int NormalPlotCap
        {
            get
            {
                int[] caps = cfg.farm.plotCapByStage;
                return caps[Math.Min(zone.Stage, caps.Length - 1)];
            }
        }

        public PlotModel GetPlot(int index)
        {
            return index >= 0 && index < plots.Length ? plots[index] : null;
        }

        public IEnumerable<PlotModel> AllPlots { get { return plots; } }

        public bool IsUnlocked(PlotModel plot)
        {
            if (plot == null) return false;
            if (plot.Kind == PlotKind.Normal) return plot.Index < unlockedNormal;
            return zone.Stage >= cfg.farm.goldenPlotUnlockStage;
        }

        // ───────── 심기 ─────────

        /// <summary>저수준 심기. 씨앗 소모는 호출자가 처리한다. byBot이면 botAllowed=false 작물은 거부한다.</summary>
        public PlantResult TryPlant(int index, string cropId, bool byBot = false)
        {
            PlotModel p = GetPlot(index);
            if (p == null) return PlantResult.NoSuchPlot;
            if (!IsUnlocked(p)) return PlantResult.PlotLocked;
            if (p.Phase != PlotPhase.Empty) return PlantResult.NotEmpty;
            CropDef crop = cfg.FindCrop(cropId);
            if (crop == null) return PlantResult.UnknownCrop;
            if (crop.unlockStage > zone.Stage) return PlantResult.CropLocked;
            if (byBot && !crop.botAllowed) return PlantResult.BotNotAllowed;
            if (crop.golden != (p.Kind == PlotKind.Golden)) return PlantResult.WrongPlotKind;

            p.Phase = PlotPhase.Growing;
            p.CropId = crop.id;
            p.Elapsed = 0f;
            p.GrowSeconds = crop.growSeconds;
            p.Watered = false;
            RaisePlot(p);
            return PlantResult.Ok;
        }

        /// <summary>플레이어 심기: 손에 든 씨앗(seedItemId)을 인벤토리에서 1개 소모하고 심는다.</summary>
        public PlantResult TryPlantFromInventory(int index, string seedItemId)
        {
            string cropId = ItemIds.CropIdFromSeed(seedItemId);
            if (cropId == null) return PlantResult.UnknownCrop;
            if (inventory.CountOf(seedItemId) < 1) return PlantResult.NoSeed;
            PlantResult r = TryPlant(index, cropId, false);
            if (r == PlantResult.Ok) inventory.TryRemove(seedItemId, 1);
            return r;
        }

        // ───────── 수확 ─────────

        /// <summary>저수준 수확. 칸을 비우고 수확한 작물과 수량을 돌려준다(당근 보관은 호출자가 처리).</summary>
        public HarvestResult TryHarvest(int index, bool byBot, out string cropId, out int amount)
        {
            cropId = null;
            amount = 0;
            PlotModel p = GetPlot(index);
            if (p == null) return HarvestResult.NoSuchPlot;
            if (!IsUnlocked(p)) return HarvestResult.PlotLocked;
            if (p.Phase == PlotPhase.Empty) return HarvestResult.NothingPlanted;
            if (p.Phase != PlotPhase.Ready) return HarvestResult.NotReady;
            CropDef crop = cfg.FindCrop(p.CropId);
            if (byBot && (crop == null || !crop.botAllowed)) return HarvestResult.BotNotAllowed;

            cropId = p.CropId;
            amount = crop != null ? crop.harvestAmount : 1;
            p.Clear();
            RaisePlot(p);
            return HarvestResult.Ok;
        }

        /// <summary>플레이어 수확: 당근을 CarrotStock에 넣는다.</summary>
        public HarvestResult HarvestToStock(int index)
        {
            string cropId;
            int amount;
            HarvestResult r = TryHarvest(index, false, out cropId, out amount);
            if (r == HarvestResult.Ok) stock.Add(cropId, amount);
            return r;
        }

        // ───────── 물주기(SYS-15, 이성재님 담당): 성장 시간을 줄이는 상태 변경만 제공 ─────────

        public WaterResult TryWater(int index)
        {
            PlotModel p = GetPlot(index);
            if (p == null) return WaterResult.NoSuchPlot;
            if (!IsUnlocked(p)) return WaterResult.PlotLocked;
            if (p.Phase != PlotPhase.Growing) return WaterResult.NotGrowing;
            if (p.Watered) return WaterResult.AlreadyWatered;

            p.Watered = true;
            p.GrowSeconds *= (1f - cfg.farm.waterGrowthReduction);
            if (p.Elapsed >= p.GrowSeconds)
            {
                p.Elapsed = p.GrowSeconds;
                p.Phase = PlotPhase.Ready;
            }
            RaisePlot(p);
            return WaterResult.Ok;
        }

        // ───────── 성장 ─────────

        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;
            for (int i = 0; i < plots.Length; i++)
            {
                PlotModel p = plots[i];
                if (p.Phase != PlotPhase.Growing) continue;
                p.Elapsed += deltaSeconds;
                if (p.Elapsed >= p.GrowSeconds)
                {
                    p.Elapsed = p.GrowSeconds;
                    p.Phase = PlotPhase.Ready;
                    RaisePlot(p);
                }
            }
        }

        /// <summary>이어하기 시 호출. farm.offlineGrowthCapSeconds가 0이면 아무 일도 하지 않는다.</summary>
        public void AdvanceOffline(double secondsSinceSave)
        {
            double cap = cfg.farm.offlineGrowthCapSeconds;
            if (cap <= 0.0 || secondsSinceSave <= 0.0) return;
            Tick((float)Math.Min(secondsSinceSave, cap));
        }

        // ───────── 밭 확장(SYS-13) ─────────

        /// <summary>일반 밭 칸을 최대 n칸 연다(지대 단계 상한까지). 실제로 연 칸 수를 반환.</summary>
        public int ExpandNormalPlots(int n)
        {
            int room = Math.Max(0, NormalPlotCap - unlockedNormal);
            int add = Math.Min(Math.Max(0, n), room);
            if (add <= 0) return 0;
            unlockedNormal += add;
            RaiseLayout();
            return add;
        }

        // ───────── 저장/불러오기 ─────────

        public List<PlotSnapshot> Capture()
        {
            var list = new List<PlotSnapshot>();
            foreach (PlotModel p in plots)
            {
                if (p.Phase == PlotPhase.Empty) continue;
                list.Add(new PlotSnapshot
                {
                    index = p.Index,
                    phase = p.Phase,
                    cropId = p.CropId,
                    elapsed = p.Elapsed,
                    growSeconds = p.GrowSeconds,
                    watered = p.Watered
                });
            }
            return list;
        }

        public int CapturedUnlockedNormal { get { return unlockedNormal; } }

        /// <summary>잘못된 항목(범위 밖 칸, 없는 작물)은 건너뛴다.</summary>
        public void Restore(int unlockedNormalPlots, IList<PlotSnapshot> snapshots)
        {
            foreach (PlotModel p in plots) p.Clear();
            unlockedNormal = Math.Min(Math.Max(0, unlockedNormalPlots), normalPlotTotal);
            if (snapshots != null)
            {
                foreach (PlotSnapshot s in snapshots)
                {
                    if (s == null) continue;
                    PlotModel p = GetPlot(s.index);
                    if (p == null || s.phase == PlotPhase.Empty) continue;
                    CropDef crop = cfg.FindCrop(s.cropId);
                    if (crop == null || s.growSeconds <= 0f) continue;

                    p.CropId = crop.id;
                    p.GrowSeconds = s.growSeconds;
                    p.Elapsed = Math.Max(0f, s.elapsed);
                    p.Watered = s.watered;
                    p.Phase = (s.phase == PlotPhase.Ready || p.Elapsed >= p.GrowSeconds) ? PlotPhase.Ready : PlotPhase.Growing;
                    if (p.Phase == PlotPhase.Ready) p.Elapsed = p.GrowSeconds;
                }
            }
            RaiseLayout();
        }

        void OnStageUnlocked(int stage) { RaiseLayout(); }

        void RaisePlot(PlotModel p)
        {
            var handler = PlotChanged;
            if (handler != null) handler(p);
        }

        void RaiseLayout()
        {
            var handler = LayoutChanged;
            if (handler != null) handler();
        }
    }
}
