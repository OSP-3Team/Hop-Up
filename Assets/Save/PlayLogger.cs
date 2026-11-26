using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// WBS 8.1 플레이 로그 (SYS-04)
///
/// 이벤트가 일어날 때마다 CSV 파일(playlog.csv)에 한 줄씩 바로 추가합니다.
///   - 발판설치 : 발판을 설치할 때마다 (발판 종류, 그때 발 높이)
///   - 종료     : 게임을 끄거나 메인으로 나갈 때 (종료 위치 = 발판 / 땅 / 공중, 발 높이)
/// 발판 사용량·설치 간격은 이 줄들을 엑셀로 집계해서 구합니다. 파일은 지우지 않고 계속 이어서 기록합니다.
///
/// 열: 시각, 경과초(게임 시작 후), 이벤트, 발판종류, 발높이(m), 종료위치
///
/// 사용법: 씬의 빈 오브젝트(GameSystems)에 붙입니다. 참조 칸은 비워 두면 씬에서 자동으로 찾습니다.
/// </summary>
public class PlayLogger : MonoBehaviour
{
    [Header("참조 (비워 두면 씬에서 자동으로 찾음)")]
    [SerializeField] private PlayerController player;
    [SerializeField] private PlatformPlacer placer;

    [Header("설정")]
    [SerializeField] private string logFileName = "playlog.csv";
    [SerializeField, Tooltip("Console에 [로그] 메시지 출력")]
    private bool debugLog = true;

    private const string Header = "시각,경과초,이벤트,발판종류,발높이(m),종료위치";

    private float sessionStart;
    private bool endLogged;
    private float lastHeight;
    private string lastPosition = "알수없음";
    private bool warnedWriteFail;

    private string LogPath => Path.Combine(Application.persistentDataPath, logFileName);

    private void Awake()
    {
        sessionStart = Time.realtimeSinceStartup;
    }

    private void Start()
    {
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (placer == null) placer = FindFirstObjectByType<PlatformPlacer>();

        if (placer != null)
            placer.PlatformPlaced += OnPlatformPlaced;
        else
            Debug.LogWarning("[로그] 씬에 PlatformPlacer가 없어서 발판 설치는 기록하지 않습니다.");

        if (player == null)
            Debug.LogWarning("[로그] 씬에 PlayerController가 없어서 높이와 종료 위치는 기록하지 못합니다.");

        if (debugLog)
            Debug.Log("[로그] 플레이 로그 파일: " + LogPath);
    }

    // 종료할 때 플레이어가 이미 사라졌을 수 있어서, 마지막 상태를 매 프레임 기억해 둠
    private void LateUpdate()
    {
        if (player == null) return;

        lastHeight = player.FootHeight;
        lastPosition = DescribePosition();
    }

    private void OnApplicationQuit()
    {
        LogEnd();
    }

    private void OnDestroy()
    {
        if (placer != null)
            placer.PlatformPlaced -= OnPlatformPlaced;

        LogEnd();   // 메인으로 나가서 씬이 사라질 때
    }

    private void OnPlatformPlaced(string kindId, Vector3 position)
    {
        float height = player != null ? player.FootHeight : position.y;
        AppendRow("발판설치", kindId, height, "");
    }

    private void LogEnd()
    {
        if (endLogged) return;
        endLogged = true;

        AppendRow("종료", "", lastHeight, lastPosition);
    }

    // 종료 위치: 공중이면 "공중", 설치한 발판 위면 "발판", 그 외 바닥이면 "땅"
    private string DescribePosition()
    {
        if (!player.IsGrounded) return "공중";

        Collider ground = player.GroundCollider;
        if (ground != null && ground.GetComponentInParent<PlacedPlatform>() != null)
            return "발판";

        return "땅";
    }

    private void AppendRow(string eventName, string kindId, float height, string endPosition)
    {
        CultureInfo inv = CultureInfo.InvariantCulture;

        string line = string.Join(",",
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", inv),
            (Time.realtimeSinceStartup - sessionStart).ToString("F1", inv),
            eventName,
            kindId,
            height.ToString("F2", inv),
            endPosition);

        try
        {
            string path = LogPath;

            // 새 파일이면 첫 줄에 열 이름 (엑셀에서 한글이 깨지지 않게 BOM 포함)
            if (!File.Exists(path))
                File.WriteAllText(path, Header + Environment.NewLine, new UTF8Encoding(true));

            File.AppendAllText(path, line + Environment.NewLine, new UTF8Encoding(false));

            if (debugLog)
                Debug.Log("[로그] " + line);
        }
        catch (Exception ex)
        {
            if (!warnedWriteFail)
            {
                Debug.LogWarning("[로그] 플레이 로그를 쓰지 못했습니다: " + ex.Message);
                warnedWriteFail = true;
            }
        }
    }
}
