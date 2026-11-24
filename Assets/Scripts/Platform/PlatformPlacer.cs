using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// WBS 3.3 발판 설치·파괴 (SYS-05)
///
/// [설치]  에임 위치에 미리보기(초록 = 가능, 빨강 = 불가)가 뜨고, 초록일 때 좌클릭하면 설치
/// [파괴]  우클릭: 1.5m 이내에서 조준한 발판을 선택 (발판 선택 모드 진입, 주황색 표시)
///         좌클릭: 선택 모드에서 누르면 발판 파괴 후 모드 해제
///         우클릭: 선택 모드에서 다시 누르면 선택 취소
///
/// [인벤토리 연동] Use Inventory를 켜거나, 씬에 인벤토리(PlatformInventoryBridge)가 있으면
///   - 손에 발판을 들고 있을 때만 설치 기능(미리보기 포함)이 켜지고
///   - 설치에 성공했을 때 발판 1개를 소모합니다. (설치가 안 되면 소모하지 않음)
///
/// [카메라] Camera Pivot 칸을 비워 두면 Player 안의 카메라를 자동으로 사용합니다.
/// [디버그] Debug Placement를 켜면 설치 불가(빨강) 이유를 Console에 알려줍니다.
///
/// Player 오브젝트에 PlayerController와 함께 붙입니다.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlatformPlacer : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private GameObject platformPrefab;  // Prefabs 폴더의 Platform
    [SerializeField, Tooltip("눈 위치·시선 기준 오브젝트. 비워 두면 Player 안의 카메라를 자동으로 사용")]
    private Transform cameraPivot;

    [Header("인벤토리 연동")]
    [SerializeField, Tooltip("켜면 항상 인벤토리 기준. 꺼져 있어도 씬에 인벤토리(PlatformInventoryBridge)가 있으면 자동으로 인벤토리 기준")]
    private bool useInventory = false;

    [Header("설치 위치")]
    [SerializeField] private float placeDistance = 2.5f;   // 에임 방향으로 이만큼 앞에 설치 (3m 이내로 유지)

    [Header("파괴(선택) 설정")]
    [SerializeField] private float selectDistance = 1.5f;  // 조준 대상 최대 거리 (카메라 기준)
    [SerializeField, Tooltip("조준선이 닿는 범위를 조금 넓혀 줍니다 (작은 발판도 쉽게 선택)")]
    private float selectAimRadius = 0.15f;

    [Header("미리보기 색")]
    [SerializeField] private Color validColor = new Color(0.2f, 0.9f, 0.2f);
    [SerializeField] private Color invalidColor = new Color(0.9f, 0.2f, 0.2f);

    [Header("디버그")]
    [SerializeField, Tooltip("설치 불가 이유가 바뀔 때마다 Console에 출력 (확인이 끝나면 끄기)")]
    private bool debugPlacement = true;

    private PlayerController player;
    private GameObject preview;
    private Renderer[] previewRenderers;
    private Vector3 halfExtents;

    private PlacedPlatform selected;   // null이 아니면 "발판 선택 모드"

    private readonly Collider[] overlapBuffer = new Collider[16];
    private readonly RaycastHit[] rayBuffer = new RaycastHit[16];
    private string lastReason = "";

    // 인벤토리를 쓸지 여부: 체크했거나, 씬에 인벤토리가 있으면 자동으로 사용
    private bool UseInventoryNow => useInventory || PlatformInventoryLocator.Current != null;
    private bool warnedNoInventory;

    private void Awake()
    {
        player = GetComponent<PlayerController>();

        // Camera Pivot이 비었거나 Player 본체가 들어가 있으면 Player 안의 카메라를 자동으로 사용
        if (cameraPivot == null || cameraPivot == transform)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
                cameraPivot = cam.transform;
            else
                Debug.LogError("[PlatformPlacer] Player 안에서 카메라를 찾지 못했습니다. Camera Pivot 칸에 Main Camera를 연결해 주세요.");
        }

        CreatePreview();
    }

    private void CreatePreview()
    {
        preview = Instantiate(platformPrefab);
        preview.name = "PlatformPreview";

        // 발판 크기(절반)를 BoxCollider에서 계산 — 겹침 검사에 사용
        BoxCollider box = preview.GetComponent<BoxCollider>();
        halfExtents = Vector3.Scale(box.size, preview.transform.lossyScale) * 0.5f;

        // 미리보기는 밟히거나 부딪히면 안 되니 Collider를 모두 끔
        foreach (Collider c in preview.GetComponentsInChildren<Collider>())
            c.enabled = false;

        previewRenderers = preview.GetComponentsInChildren<Renderer>();
    }

    private void Update()
    {
        // 메뉴·창이 열려 있으면(InputEnabled == false) 입력 무시, 미리보기 숨김
        if (!player.InputEnabled)
        {
            preview.SetActive(false);
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null || cameraPivot == null) return;

        // 우클릭: 선택 모드 진입 / 해제 (손에 든 아이템과 상관없이 동작)
        if (mouse.rightButton.wasPressedThisFrame)
            HandleRightClick();

        // ── 선택 모드: 좌클릭 = 파괴 (설치는 하지 않음) ──
        if (selected != null)
        {
            preview.SetActive(false);
            if (mouse.leftButton.wasPressedThisFrame)
                HandleLeftClickInSelectMode();
            return;
        }

        // ── 일반 모드: 미리보기 + 좌클릭 = 설치 ──
        HandlePlacement(mouse);
    }

    // ───────────────────────── 설치 ─────────────────────────

    private void HandlePlacement(Mouse mouse)
    {
        // 인벤토리 연동: 손에 발판이 없으면 설치 기능 자체를 끔 (미리보기도 숨김)
        if (UseInventoryNow)
        {
            IPlatformInventory inv = PlatformInventoryLocator.Current;

            if (inv == null && !warnedNoInventory)
            {
                Debug.LogWarning("[PlatformPlacer] Use Inventory가 켜져 있는데 씬에 PlatformInventoryBridge가 없어서 발판을 설치할 수 없습니다.");
                warnedNoInventory = true;
            }

            if (inv == null || !inv.IsHoldingPlatform)
            {
                preview.SetActive(false);
                return;
            }
        }

        // 에임 위치: 카메라 정면(조준선) 방향으로 placeDistance만큼 앞
        Vector3 eye = cameraPivot.position;
        Vector3 pos = eye + cameraPivot.forward * placeDistance;
        Quaternion rot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);  // 항상 수평

        preview.SetActive(true);
        preview.transform.SetPositionAndRotation(pos, rot);

        // 1) 다른 물체와 겹치는가? (내 몸은 제외)
        bool overlapping = FindOverlap(pos, rot, out string overlapName);

        // 2) 카메라에서 발판 중심까지 가는 길이 막혀 있는가? (바닥·벽 너머 설치 방지, 내 몸은 제외)
        bool blocked = FindBlocker(eye, pos, out string blockName);

        bool canPlace = !overlapping && !blocked;
        SetPreviewColor(canPlace ? validColor : invalidColor);

        if (debugPlacement)
        {
            string reason = canPlace ? "설치 가능"
                          : overlapping ? "겹침: " + overlapName
                          : "가로막힘: " + blockName;

            if (reason != lastReason)
            {
                Debug.Log("[발판] " + reason
                          + " | 카메라 높이 " + eye.y.ToString("F2")
                          + " | 발판 높이 " + pos.y.ToString("F2"));
                lastReason = reason;
            }
        }

        if (canPlace && mouse.leftButton.wasPressedThisFrame)
        {
            // 설치할 수 있는 상태일 때만 1개 소모. 소모에 실패하면(수량 0 등) 설치하지 않음
            if (UseInventoryNow && !PlatformInventoryLocator.Current.TryConsumeHeldPlatform())
                return;

            GameObject placed = Instantiate(platformPrefab, pos, rot);
            placed.AddComponent<PlacedPlatform>();   // "설치된 발판" 표식 (파괴·선택에 사용)
        }
    }

    // Player 안의 콜라이더(내 몸)는 검사에서 제외
    private bool IsMine(Collider c)
    {
        return c.transform.IsChildOf(transform);
    }

    private bool FindOverlap(Vector3 pos, Quaternion rot, out string culprit)
    {
        // 크기를 5% 줄여서 "딱 붙어 있는" 경우는 허용
        int count = Physics.OverlapBoxNonAlloc(
            pos, halfExtents * 0.95f, overlapBuffer, rot, ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            if (IsMine(overlapBuffer[i])) continue;
            culprit = overlapBuffer[i].name;
            return true;
        }

        culprit = "";
        return false;
    }

    private bool FindBlocker(Vector3 from, Vector3 to, out string culprit)
    {
        Vector3 dir = to - from;
        float dist = dir.magnitude;

        if (dist > 0.001f)
        {
            int count = Physics.RaycastNonAlloc(
                from, dir / dist, rayBuffer, dist, ~0, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                if (IsMine(rayBuffer[i].collider)) continue;
                culprit = rayBuffer[i].collider.name;
                return true;
            }
        }

        culprit = "";
        return false;
    }

    // ───────────────────────── 파괴 (선택 모드) ─────────────────────────

    private void HandleRightClick()
    {
        // 이미 선택 모드면 → 선택 취소
        if (selected != null)
        {
            CancelMode();
            return;
        }

        // 조준 대상(1.5m 이내, 가장 가까운 1개)이 설치된 발판이면 → 선택 모드 진입
        PlacedPlatform target = FindAimedPlatform(out string info);
        if (target != null)
        {
            selected = target;
            selected.SetSelected(true);
        }
        // 대상이 없거나 발판이 아니면 무반응

        if (debugPlacement)
            Debug.Log("[파괴] 우클릭: " + info);
    }

    private void HandleLeftClickInSelectMode()
    {
        PlacedPlatform target = selected;

        // 모드 조건 재검증: 대상이 아직 1.5m 이내에 있는가?
        bool inRange = false;
        float distance = -1f;
        if (target != null && target.BodyCollider != null)
        {
            Vector3 closest = target.BodyCollider.ClosestPoint(cameraPivot.position);
            distance = Vector3.Distance(cameraPivot.position, closest);
            inRange = distance <= selectDistance;
        }

        CancelMode();   // 모드 해제 (색 원래대로)

        if (inRange)
        {
            target.Break();   // 발판 파괴
            if (debugPlacement)
                Debug.Log("[파괴] 발판 파괴됨");
        }
        else if (debugPlacement)
        {
            // 범위를 벗어났으면 파괴 없이 모드만 해제
            Debug.Log(target == null
                ? "[파괴] 선택한 발판이 이미 사라져서 선택만 해제"
                : "[파괴] 선택한 발판이 범위 밖이라 파괴하지 않고 선택만 해제 (거리 "
                  + distance.ToString("F2") + "m > " + selectDistance.ToString("F1") + "m)");
        }
    }

    /// <summary>선택 모드 해제. 낙하 시작 시(3.4)에도 이 함수를 호출하면 됩니다.</summary>
    public void CancelMode()
    {
        if (selected != null)
            selected.SetSelected(false);
        selected = null;
    }

    private PlacedPlatform FindAimedPlatform(out string info)
    {
        // 에임 방향으로 (조금 굵은 선으로) 쏴서, 내 몸을 빼고 맨 처음 맞은 물체가 설치된 발판인지 확인
        int count = Physics.SphereCastNonAlloc(
            cameraPivot.position, selectAimRadius, cameraPivot.forward, rayBuffer,
            selectDistance, ~0, QueryTriggerInteraction.Ignore);

        float nearest = float.MaxValue;
        RaycastHit best = default;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            if (IsMine(rayBuffer[i].collider)) continue;
            if (rayBuffer[i].distance < nearest)
            {
                nearest = rayBuffer[i].distance;
                best = rayBuffer[i];
                found = true;
            }
        }

        if (!found)
        {
            info = "조준한 방향 " + selectDistance.ToString("F1")
                   + "m 안에 아무것도 없음 → 발판에 더 가까이 가서 조준선(+)을 발판에 맞춰 보세요";
            return null;
        }

        PlacedPlatform platform = best.collider.GetComponentInParent<PlacedPlatform>();
        if (platform == null)
        {
            // 바닥·벽이 발판보다 먼저 맞은 경우
            info = "발판이 아닌 물체가 먼저 맞음: " + best.collider.name
                   + " (거리 " + best.distance.ToString("F2") + "m)";
            return null;
        }

        info = "발판 선택됨 (거리 " + best.distance.ToString("F2") + "m) → 좌클릭하면 파괴";
        return platform;
    }

    private void SetPreviewColor(Color color)
    {
        foreach (Renderer r in previewRenderers)
            r.material.color = color;
    }
}
