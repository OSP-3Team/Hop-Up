using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HopUp.FarmEconomy;
using Newtonsoft.Json.Linq;
using Project.Core;
using Project.Inventory;
using UnityEngine;

/// <summary>
/// WBS 8.2 세이브·자동 로드 (SYS-02)
///
/// - 5분마다 자동 저장 (Auto Save Interval). 낙하 중이거나 공중이면 저장하지 않고, 착지하면 바로 저장
/// - 일시정지 메뉴의 [메인으로]는 SaveManager.Instance.SaveNow()를 부르면 됨 (착지 상태에서만 저장, 낙하 중이면 false)
/// - 게임 씬이 시작될 때 메인 메뉴에서 "이어하기"를 골랐으면(GameStartRequest.Mode) save.json을 불러옴
/// - 저장 내용: 이준형 님의 농사·경제 조각(코인·당근·해금 지대·밭·봇·요리) + 플레이어 위치 + 인벤토리 + 설치한 발판
///
/// 사용법: 씬의 빈 오브젝트(GameSystems)에 붙입니다. 참조 칸은 비워 두면 씬에서 자동으로 찾습니다.
/// 테스트: 이 컴포넌트 우클릭 메뉴(⋮)의 [Save Now] / [Load Now] / [Log Save Path]
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    [Header("참조 (비워 두면 씬에서 자동으로 찾음)")]
    [SerializeField] private PlayerController player;
    [SerializeField] private FallController fall;
    [SerializeField] private PlatformPlacer placer;
    [SerializeField] private InventoryManager inventory;

    [Header("저장 설정")]
    [SerializeField, Tooltip("MainMenuUI의 Save File Name과 같아야 함")]
    private string saveFileName = "save.json";
    [SerializeField, Tooltip("자동 저장 간격(초). 흐름도 기준 5분 = 300초")]
    private float autoSaveInterval = 300f;
    [SerializeField, Tooltip("Console에 [세이브] 로그 출력")]
    private bool debugLog = true;

    // 불러올 때 이 순서대로 복원: 인벤토리 → 발판 → 플레이어
    // (발판이 먼저 있어야 발판 위에서 저장했을 때 불러오자마자 떨어지지 않음)
    private readonly List<ISaveSection> sections = new List<ISaveSection>();

    private float timer;
    private bool pendingLogged;

    private string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (fall == null) fall = FindFirstObjectByType<FallController>();
        if (placer == null) placer = FindFirstObjectByType<PlatformPlacer>();
        if (inventory == null) inventory = FindFirstObjectByType<InventoryManager>();

        BuildSections();

        if (GameStartRequest.ShouldLoadSave)
            LoadGame();
    }

    private void BuildSections()
    {
        sections.Clear();

        if (inventory != null) sections.Add(new InventorySaveSection(inventory));
        else Debug.LogWarning("[세이브] 씬에 InventoryManager가 없어서 인벤토리는 저장하지 않습니다.");

        if (placer != null) sections.Add(new PlatformsSaveSection(placer));
        else Debug.LogWarning("[세이브] 씬에 PlatformPlacer가 없어서 설치한 발판은 저장하지 않습니다.");

        if (player != null) sections.Add(new PlayerSaveSection(player));
        else Debug.LogWarning("[세이브] 씬에 PlayerController가 없어서 플레이어 위치는 저장하지 않습니다.");
    }

    private void Update()
    {
        timer += Time.unscaledDeltaTime;
        if (timer < autoSaveInterval) return;

        // 낙하 중·공중이면 저장하지 않고 다음 프레임에 다시 시도 (착지하면 바로 저장됨)
        if (TrySave())
            timer = 0f;
    }

    // ───────────────────────── 저장 ─────────────────────────

    /// <summary>지금 저장한다. 낙하 중이거나 공중이면 저장하지 않고 false. (일시정지 메뉴의 [메인으로]에서 호출)</summary>
    public bool SaveNow()
    {
        return TrySave();
    }

    private bool CanSaveNow()
    {
        if (player == null) return true;   // 플레이어가 없는 씬은 위치 조건 없이 저장
        if (!player.IsGrounded) return false;
        if (fall != null && fall.IsFalling) return false;
        return true;
    }

    private bool TrySave()
    {
        if (!CanSaveNow())
        {
            if (!pendingLogged)
            {
                Log("공중이거나 낙하 중이라 저장을 보류합니다 (마지막 저장 유지, 착지하면 저장)");
                pendingLogged = true;
            }
            return false;
        }
        pendingLogged = false;

        var root = new JObject();

        EconomyHub hub = EconomyHub.Instance;
        if (hub != null && hub.Save != null)
            root[hub.Save.Key] = hub.Save.Capture();

        foreach (ISaveSection section in sections)
            root[section.Key] = section.Capture();

        string path = SavePath;
        string tempPath = path + ".tmp";

        try
        {
            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

            // 임시 파일에 먼저 쓰고 바꿔치기 → 저장 도중 꺼져도 이전 저장이 망가지지 않음
            File.WriteAllText(tempPath, root.ToString(Newtonsoft.Json.Formatting.Indented), new UTF8Encoding(false));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tempPath, path);

            Log("저장 완료: " + path);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError("[세이브] 저장 실패: " + ex.Message);
            return false;
        }
    }

    // ───────────────────────── 불러오기 ─────────────────────────

    /// <summary>save.json을 읽어 이어하기 상태를 복원한다. 파일이 없거나 깨졌으면 false</summary>
    public bool LoadGame()
    {
        string path = SavePath;
        if (!File.Exists(path))
        {
            Log("저장 파일이 없어서 불러오지 않습니다: " + path);
            return false;
        }

        JObject root;
        try
        {
            root = JObject.Parse(File.ReadAllText(path, Encoding.UTF8));
        }
        catch (Exception ex)
        {
            Debug.LogError("[세이브] 저장 파일을 읽지 못했습니다: " + ex.Message);
            return false;
        }

        EconomyHub hub = EconomyHub.Instance;
        if (hub != null)
        {
            bool ok = hub.RestoreFromSave(root);
            Log("농사·경제 복원 " + (ok ? "성공" : "실패 또는 데이터 없음"));
        }

        foreach (ISaveSection section in sections)
        {
            JObject data = root[section.Key] as JObject;
            if (data == null)
            {
                Log("[" + section.Key + "] 저장 데이터가 없어서 그대로 둡니다");
                continue;
            }

            bool ok = section.Restore(data);
            Log("[" + section.Key + "] 복원 " + (ok ? "성공" : "실패"));
        }

        Physics.SyncTransforms();           // 새로 만든 발판과 옮긴 위치를 물리에 바로 반영
        if (fall != null) fall.ResetTracking();
        timer = 0f;
        return true;
    }

    private void Log(string message)
    {
        if (debugLog)
            Debug.Log("[세이브] " + message);
    }

    // 테스트용: 점 세 개(⋮) 메뉴
    [ContextMenu("Save Now")]
    private void ContextSaveNow()
    {
        if (!Application.isPlaying) return;
        SaveNow();
    }

    [ContextMenu("Load Now")]
    private void ContextLoadNow()
    {
        if (!Application.isPlaying) return;
        LoadGame();
    }

    [ContextMenu("Log Save Path")]
    private void LogSavePath()
    {
        Debug.Log("[세이브] " + SavePath);
    }
}
