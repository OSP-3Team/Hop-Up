using System;
using UnityEngine;

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
///              - 낙하 방지 효과가 있으면: 발판 유지, 효과 1회 소모
///              - 없으면: 그 발판 1개 파괴 + 새 낙하를 0m부터 시작 (입력 막기는 유지)
///              땅·정거장 바닥 등 설치 발판이 아닌 곳에 착지하면 아무 일도 없습니다.
///
/// [높이 도달 신호]  접지 0.1초 유지 후
///              - 발 높이 >= Ending Height(300m)이면 EndingReached를 1회
///              - 아니면 Milestone Heights(75·150·225m) 중 아직 안 보낸 것을 낮은 것부터 전부 MilestoneReached(높이)로 1회씩
///              해금·엔딩 화면은 담당자가 이 신호를 받아서 연결합니다. (농장 바닥이 Y=0이라는 가정)
///
/// Player 오브젝트에 PlayerController, PlayerBuffs와 함께 붙입니다.
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

    [Header("높이 도달 신호")]
    [SerializeField, Tooltip("처음 도달하면 MilestoneReached 신호를 보낼 높이들 (낮은 것부터 순서대로 적기)")]
    private float[] milestoneHeights = { 75f, 150f, 225f };
    [SerializeField, Tooltip("이 높이 이상에서 접지하면 EndingReached 신호")]
    private float endingHeight = 300f;
    [SerializeField, Tooltip("높이 판정 전에 접지를 유지해야 하는 시간(초)")]
    private float groundedHoldTime = 0.1f;

    [Header("디버그")]
    [SerializeField, Tooltip("Console에 [낙하] 로그 출력 (확인이 끝나면 끄기)")]
    private bool debugLog = true;

    /// <summary>75·150·225m 같은 지대 높이에 처음 접지했을 때 (인자: 그 지대의 높이)</summary>
    public event Action<float> MilestoneReached;

    /// <summary>엔딩 높이에 접지했을 때 (1회)</summary>
    public event Action EndingReached;

    // 높이 바(HeightBar)가 읽어 가는 값
    public float EndingHeight => endingHeight;
    public float[] MilestoneHeights => milestoneHeights;

    private PlayerController player;
    private PlatformPlacer placer;
    private PlayerBuffs buffs;

    private bool initialized;
    private bool wasGrounded;
    private float lastGroundedFoot;
    private float apexHeight;        // 땅을 떠난 뒤 가장 높았던 발 높이
    private bool gateOn;             // 낙하 입력 막기 중인가

    private bool endingFired;
    private bool[] milestoneFired;

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        placer = GetComponent<PlatformPlacer>();
        buffs = GetComponent<PlayerBuffs>();

        if (buffs == null)
            Debug.LogWarning("[FallController] Player에 PlayerBuffs가 없어서 낙하 방지 효과는 사용되지 않습니다.");

        milestoneFired = new bool[milestoneHeights.Length];
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
            if (buffs != null && buffs.TryConsumeFallProtection())
            {
                Log("착지 " + fallDistance.ToString("F1") + "m 낙하 → 낙하 방지 효과 사용, 발판 유지 (남은 횟수 "
                    + buffs.FallProtectionCharges + ")");
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

    // ───────────────────────── 높이 도달 신호 ─────────────────────────

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

        for (int i = 0; i < milestoneHeights.Length; i++)
        {
            if (milestoneFired[i] || foot < milestoneHeights[i]) continue;

            milestoneFired[i] = true;
            Log("지대 도달 " + milestoneHeights[i].ToString("F0") + "m → MilestoneReached");
            MilestoneReached?.Invoke(milestoneHeights[i]);
        }
    }

    private void Log(string message)
    {
        if (debugLog)
            Debug.Log("[낙하] " + message);
    }
}
