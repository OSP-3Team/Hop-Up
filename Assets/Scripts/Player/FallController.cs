using System;
using UnityEngine;
using HopUp.FarmEconomy;

/// <summary>
/// WBS 3.4 추락 판정 및 페널티 (SYS-01)
///
/// [낙하 거리]  땅을 떠난 뒤 가장 높았던 발 높이(정점) - 착지한 발 높이
///
/// [입력 막기]  공중에서 (정점 - 현재 발 높이)가 Input Gate Distance(2m) 이상이면
///              클릭(발판 설치·파괴)·Space를 막고, 우클릭 선택 모드를 해제합니다.
///              (시점·이동·숫자 키는 막지 않음.) 착지하면 풀립니다.
///
/// [페널티]     착지한 낙하 거리가 Penalty Distance(10m) 이상이고, 착지한 곳이 "설치한 발판"이면
///              - 낙하 방지 효과가 있으면: 발판 유지, 효과 1회 소모 (EconomyHub의 요리 시스템 CookingService 사용)
///              - 없으면: 그 발판 1개 파괴 + 새 낙하를 0m부터 시작 (입력 막기는 유지)
///              땅·정거장 바닥 등 설치 발판이 아닌 곳에 착지하면 아무 일도 없습니다.
///
/// [높이 도달]  접지 0.1초 유지 후
///              - 발 높이 >= Ending Height(300m)이면 EndingReached를 1회
///              - 아니면 EconomyHub의 지대 단계(ZoneProgress)에 발 높이를 알림 (처음 도달한 지대만 해금됨)
///              (농장 바닥이 Y=0이라는 가정)
///
/// [날기 모드]  PlayerController의 Fly Mode가 켜져 있는 동안은 낙하 계산을 멈추고,
///              끄는 순간의 높이부터 새 낙하로 계산합니다.
///
/// Player 오브젝트에 PlayerController와 함께 붙입니다.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class FallController : MonoBehaviour
{
    [Header("낙하 입력 막기")]
    [SerializeField, Tooltip("정점보다 이만큼(m) 이상 내려오면 클릭·Space를 막음. 점프 높이(1.5m)보다 커야 일반 점프에서는 안 걸림")]
    private float inputGateDistance = 2f;

    [Header("낙하 페널티")]
    [SerializeField, Tooltip("낙하 거리가 이 이상이고 설치한 발판에 착지하면 그 발판이 파괴됨")]
    private float penaltyDistance = 10f;

    [Header("높이 도달")]
    [SerializeField, Tooltip("이 높이 이상에서 접지하면 EndingReached 신호")]
    private float endingHeight = 300f;
    [SerializeField, Tooltip("높이 판정 전에 접지를 유지해야 하는 시간(초)")]
    private float groundedHoldTime = 0.1f;
    [SerializeField, Tooltip("EconomyHub가 없는 씬(테스트 씬)에서만 쓰는 지대 높이. EconomyHub가 있으면 그쪽 설정 값을 씁니다")]
    private float[] fallbackZoneHeights = { 75f, 150f, 225f };

    [Header("디버그")]
    [SerializeField, Tooltip("Console에 [낙하] 로그 출력 (확인이 끝나면 끄기)")]
    private bool debugLog = true;

    /// <summary>엔딩 높이에 접지했을 때 (1회)</summary>
    public event Action EndingReached;

    // 높이 바(HeightBar)·세이브(SaveManager)가 읽어 가는 값
    public float EndingHeight => endingHeight;

    /// <summary>낙하 입력 막기가 켜져 있는 동안 true (세이브 보류 판단에 사용)</summary>
    public bool IsFalling => gateOn;

    /// <summary>지대 높이들(75·150·225). EconomyHub가 있으면 그쪽 설정을, 없으면 Inspector 값을 사용</summary>
    public float[] ZoneHeights
    {
        get
        {
            EconomyHub hub = EconomyHub.Instance;
            if (hub != null)
            {
                try
                {
                    float[] heights = hub.Config.zones.stationHeights;
                    if (heights != null && heights.Length > 0) return heights;
                }
                catch (Exception)
                {
                    // 설정을 못 읽으면 아래 기본값 사용
                }
            }
            return fallbackZoneHeights;
        }
    }

    private PlayerController player;
    private PlatformPlacer placer;

    private bool initialized;
    private bool wasGrounded;
    private float lastGroundedFoot;
    private float apexHeight;        // 땅을 떠난 뒤 가장 높았던 발 높이
    private bool gateOn;             // 낙하 입력 막기 중인가

    private bool endingFired;

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        placer = GetComponent<PlatformPlacer>();
    }

    private void OnDisable()
    {
        // 꺼질 때 입력이 막힌 채로 남지 않도록 풀어 줌
        if (gateOn && player != null)
        {
            gateOn = false;
            player.ActionInputEnabled = true;
        }
    }

    /// <summary>세이브를 불러와 위치가 바뀐 뒤에 호출: 낙하 기록을 처음부터 다시 시작</summary>
    public void ResetTracking()
    {
        initialized = false;
        EndGate();
    }

    // PlayerController.Update(접지 판정·이동)가 끝난 뒤에 실행
    private void LateUpdate()
    {
        bool grounded = player.IsGrounded;
        float foot = player.FootHeight;

        if (!initialized)
        {
            initialized = true;
            wasGrounded = grounded;
            lastGroundedFoot = foot;
            apexHeight = foot;
        }

        // 날기 모드: 낙하 계산을 멈추고 매 프레임 지금 높이를 정점으로 삼음
        // → 날기를 끄면 그 높이에서 새 낙하로 계산됨
        if (player.IsFlying)
        {
            EndGate();
            apexHeight = foot;
            lastGroundedFoot = foot;
            wasGrounded = grounded;

            if (grounded)
                CheckHeightReached(foot);
            return;
        }

        if (grounded)
        {
            if (!wasGrounded)
                HandleLanding(foot);

            lastGroundedFoot = foot;
            CheckHeightReached(foot);
        }
        else
        {
            if (wasGrounded)
                apexHeight = Mathf.Max(foot, lastGroundedFoot);   // 방금 땅을 떠남 → 여기서부터 정점 추적
            else if (foot > apexHeight)
                apexHeight = foot;

            if (!gateOn && apexHeight - foot >= inputGateDistance)
                StartGate();
        }

        wasGrounded = grounded;
    }

    // ───────────────────────── 착지 ─────────────────────────

    private void HandleLanding(float foot)
    {
        float fallDistance = apexHeight - foot;

        PlacedPlatform platform = null;
        Collider ground = player.GroundCollider;
        if (ground != null)
            platform = ground.GetComponentInParent<PlacedPlatform>();

        if (fallDistance >= penaltyDistance && platform != null)
        {
            EconomyHub hub = EconomyHub.Instance;
            bool protectedByFood = hub != null && hub.Cooking != null && hub.Cooking.TryConsumeFallProtection();

            if (protectedByFood)
            {
                Log("착지 " + fallDistance.ToString("F1") + "m 낙하 → 낙하 방지 효과 사용, 발판 유지 (남은 횟수 "
                    + hub.Cooking.FallProtectionCharges + ")");
                EndGate();
            }
            else
            {
                platform.Break();
                apexHeight = foot;   // 새 낙하를 0m부터 시작
                Log("착지 " + fallDistance.ToString("F1") + "m 낙하 → 발판 파괴, 새 낙하 시작 (입력 막기 유지)");

                if (!gateOn)
                    StartGate();
            }
        }
        else
        {
            if (fallDistance >= inputGateDistance)
                Log("착지 " + fallDistance.ToString("F1") + "m 낙하 → 페널티 없음"
                    + (fallDistance >= penaltyDistance ? " (설치 발판이 아닌 곳)" : ""));

            EndGate();
        }
    }

    // ───────────────────────── 낙하 입력 막기 ─────────────────────────

    private void StartGate()
    {
        gateOn = true;
        player.ActionInputEnabled = false;

        if (placer != null)
            placer.CancelMode();   // 우클릭 발판 선택 모드 해제

        Log("낙하 시작 → 클릭·Space 막음 (정점 높이 " + apexHeight.ToString("F1") + "m)");
    }

    private void EndGate()
    {
        if (!gateOn) return;

        gateOn = false;
        player.ActionInputEnabled = true;
        Log("입력 막기 해제");
    }

    // ───────────────────────── 높이 도달 ─────────────────────────

    private void CheckHeightReached(float foot)
    {
        if (gateOn || endingFired) return;
        if (player.GroundedTime < groundedHoldTime) return;

        if (foot >= endingHeight)
        {
            endingFired = true;
            Log("엔딩 높이 도달 (" + foot.ToString("F1") + "m) → EndingReached");
            EndingReached?.Invoke();
            return;
        }

        // 지대 단계 알림: 처음 도달한 단계만 해금되고, 한 번 오른 단계는 내려가지 않음
        EconomyHub hub = EconomyHub.Instance;
        if (hub != null && hub.Zone != null && hub.Zone.NotifyHeight(foot))
            Log("지대 해금 → " + hub.Zone.Stage + "단계 (발 높이 " + foot.ToString("F1") + "m)");
    }

    private void Log(string message)
    {
        if (debugLog)
            Debug.Log("[낙하] " + message);
    }
}
