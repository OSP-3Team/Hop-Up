using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace HopUp.FarmEconomy
{
    /// <summary>
    /// 농사·상점·경제 시스템의 조립 지점(씬에 하나만 둔다). 밸런스 JSON을 읽고 서비스를 만들어 연결한다.
    /// 다른 시스템은 EconomyHub.Instance.Wallet / Carrots / Zone / Field / Shop / Sales / Cooking / Save로 접근한다.
    /// 실행 순서를 앞당겨(-200) 다른 스크립트의 Awake/OnEnable보다 먼저 서비스가 준비되게 한다.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class EconomyHub : MonoBehaviour
    {
        public static EconomyHub Instance { get; private set; }

        [Header("밸런스 파일 (StreamingAssets 기준 경로)")]
        [SerializeField] string balanceFile = "Balance/farm_economy.json";

        [Header("연결 (선택)")]
        [Tooltip("이동운님 인벤토리 컴포넌트(IInventory 구현). 비우면 임시 인벤토리를 쓴다.")]
        [SerializeField] MonoBehaviour inventoryComponent;
        [Tooltip("이성재님 월드 드롭 컴포넌트(IItemDropper 구현). 비우면 요리 결과가 넘칠 때 아이템이 사라진다.")]
        [SerializeField] MonoBehaviour dropperComponent;

        [Header("개발용")]
        [Tooltip("켜 두면 시작 시 새 게임 기본값(시작 코인·씨앗)을 적용한다. 메뉴의 새로하기/이어하기를 연결하면 끈다.")]
        [SerializeField] bool applyNewGameOnAwake = true;

        public BalanceConfig Config { get; private set; }
        public Wallet Wallet { get; private set; }
        public CarrotStock Carrots { get; private set; }
        public ZoneProgress Zone { get; private set; }
        public IInventory Inventory { get; private set; }
        public FarmField Field { get; private set; }
        public ShopService Shop { get; private set; }
        public SellService Sales { get; private set; }
        public CookingService Cooking { get; private set; }
        public FarmBotService Bots { get; private set; }
        public ISaveSection Save { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            Config = LoadConfig();
            Wallet = new Wallet();
            Carrots = new CarrotStock();
            Zone = new ZoneProgress(Config.zones.stationHeights);

            Inventory = inventoryComponent as IInventory;
            if (Inventory == null)
            {
                if (inventoryComponent != null)
                    Debug.LogWarning("[EconomyHub] inventoryComponent가 IInventory를 구현하지 않습니다. 임시 인벤토리를 사용합니다.", this);
                else
                    Debug.LogWarning("[EconomyHub] 임시 인벤토리를 사용 중입니다. 실제 인벤토리가 준비되면 inventoryComponent에 연결하세요.", this);
                Inventory = new InMemoryInventory(Config.shop.startSlots, Config.shop.maxSlots, Config.shop.inventoryStackLimit);
            }
            IItemDropper dropper = dropperComponent as IItemDropper;

            Field = new FarmField(Config, Zone, Inventory, Carrots);
            Shop = new ShopService(Config, Wallet, Zone, Inventory, Field);
            Sales = new SellService(Config, Carrots, Wallet);
            Cooking = new CookingService(Config, Inventory, Carrots, dropper);
            Bots = new FarmBotService(Config, Inventory, Carrots, Shop);
            Save = new FarmEconomySaveSection(Wallet, Carrots, Zone, Field, Shop, Cooking, Bots);

            if (applyNewGameOnAwake) ApplyNewGame();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (Field != null) Field.Tick(Time.deltaTime);
        }

        /// <summary>메인 메뉴 "새로하기": 농장·경제 상태를 처음 값으로 되돌리고 시작 코인·시작 아이템을 지급한다.</summary>
        public void ApplyNewGame()
        {
            NewGameConfig ng = Config.newGame;
            Wallet.Restore(ng.startingCoins, 0, 0);
            Carrots.Restore(null);
            Zone.Restore(0);
            Shop.RestoreBots(0);
            Bots.Restore(null);
            Field.Restore(Config.farm.startPlotCount, null);
            Cooking.RestoreState(null, 0);
            foreach (ItemStack s in ng.startingItems)
                Inventory.TryAdd(s.itemId, s.count);
        }

        /// <summary>메인 메뉴 "이어하기": SaveManager가 읽은 루트 JSON에서 이 시스템의 조각을 복원한다.</summary>
        public bool RestoreFromSave(JObject root)
        {
            if (root == null) return false;
            JObject section = root[Save.Key] as JObject;
            return section != null && Save.Restore(section);
        }

        BalanceConfig LoadConfig()
        {
            string path = Path.Combine(Application.streamingAssetsPath, balanceFile);
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path, Encoding.UTF8);
                    BalanceConfig cfg;
                    List<string> errors;
                    if (BalanceConfigLoader.TryParse(json, out cfg, out errors)) return cfg;
                    Debug.LogError("[EconomyHub] 밸런스 JSON 오류로 내장 기본값을 사용합니다:\n- " + string.Join("\n- ", errors), this);
                }
                catch (Exception ex)
                {
                    Debug.LogError("[EconomyHub] 밸런스 파일을 읽지 못해 내장 기본값을 사용합니다: " + ex.Message, this);
                }
            }
            else
            {
                Debug.LogWarning("[EconomyHub] 밸런스 파일이 없어 내장 기본값을 사용합니다: " + path, this);
            }
            return BalanceConfigLoader.CreateDefault();
        }

        // ───────── 개발·밸런싱용 컨텍스트 메뉴(인스펙터 ⋮ 메뉴) ─────────

        [ContextMenu("Debug: 코인 +100")]
        void DebugAddCoins() { if (Wallet != null) Wallet.Add(100); }

        [ContextMenu("Debug: 75m 도달")]
        void DebugReach75() { if (Zone != null) Zone.NotifyHeight(75f); }

        [ContextMenu("Debug: 150m 도달")]
        void DebugReach150() { if (Zone != null) Zone.NotifyHeight(150f); }

        [ContextMenu("Debug: 225m 도달")]
        void DebugReach225() { if (Zone != null) Zone.NotifyHeight(225f); }

        [ContextMenu("Debug: 당근 +5(일반)")]
        void DebugAddCarrots() { if (Carrots != null) Carrots.Add("normal", 5); }
    }
}
