using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// WBS 3.3 발판 설치·파괴 (SYS-05)
///
/// [설치]  좌클릭: 에임 앞쪽에 미리보기(초록 = 가능, 빨강 = 불가)가 뜨고, 초록일 때 좌클릭하면 설치
/// [파괴]  우클릭: 1.5m 이내에서 조준한 발판을 선택 (발판 선택 모드 진입, 주황색 표시)
///         좌클릭: 선택 모드에서 누르면 발판 파괴 후 모드 해제
///         우클릭: 선택 모드에서 다시 누르면 선택 취소
///
/// [인벤토리 연동] Use Inventory를 켜면
///   - 손에 발판을 들고 있을 때만 설치 기능(미리보기 포함)이 켜지고
///   - 설치에 성공했을 때 발판 1개를 소모합니다. (설치가 안 되면 소모하지 않음)
///   인벤토리와는 IPlatformInventory 인터페이스로만 대화합니다.
///
/// 아직 없는 것(나중에 추가): 낙하 중 입력 막기, 플레이 구역 검사
/// Player 오브젝트에 PlayerController와 함께 붙입니다.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlatformPlacer : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private GameObject platformPrefab;  // Prefabs 폴더의 Platform
    [SerializeField] private Transform cameraPivot;      // Player 아래의 CameraPivot

    [Header("인벤토리 연동")]
    [SerializeField] private bool useInventory = false;  // 켜면 인벤토리 기준으로 설치·소모 (꺼두면 무한 설치)

    [Header("설치 위치")]
    [SerializeField] private float placeDistance = 2.5f;   // 카메라 앞 거리 (3m 이내로 유지)
    [SerializeField] private float verticalOffset = -0.8f; // 위아래 보정 (음수 = 눈높이보다 아래)

    [Header("파괴(선택) 설정")]
    [SerializeField] private float selectDistance = 1.5f;  // 조준 대상 최대 거리 (카메라 기준)

    [Header("미리보기 색")]
    [SerializeField] private Color validColor = new Color(0.2f, 0.9f, 0.2f);
    [SerializeField] private Color invalidColor = new Color(0.9f, 0.2f, 0.2f);

    private PlayerController player;
    private GameObject preview;
    private Renderer[] previewRenderers;
    private Vector3 halfExtents;

    private PlacedPlatform selected;   // null이 아니면 "발판 선택 모드"

    private void Awake()
    {
        player = GetComponent<PlayerController>();
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
        if (mouse == null) return;

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
        if (useInventory)
        {
            IPlatformInventory inv = PlatformInventoryLocator.Current;
            if (inv == null || !inv.IsHoldingPlatform)
            {
                preview.SetActive(false);
                return;
            }
        }

        Vector3 eye = cameraPivot.position;
        Vector3 pos = eye
                      + cameraPivot.forward * placeDistance
                      + Vector3.up * verticalOffset;
        Quaternion rot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);  // 항상 수평

        preview.SetActive(true);
        preview.transform.SetPositionAndRotation(pos, rot);

        // 1) 다른 물체와 겹치는가? (크기를 5% 줄여서 "딱 붙어 있는" 경우는 허용)
        bool overlapping = Physics.CheckBox(
            pos, halfExtents * 0.95f, rot, ~0, QueryTriggerInteraction.Ignore);

        // 2) 카메라에서 발판 중심까지 가는 길이 막혀 있는가? (바닥·벽 너머 설치 방지)
        bool blocked = Physics.Linecast(
            eye, pos, ~0, QueryTriggerInteraction.Ignore);

        bool canPlace = !overlapping && !blocked;
        SetPreviewColor(canPlace ? validColor : invalidColor);

        if (canPlace && mouse.leftButton.wasPressedThisFrame)
        {
            // 설치할 수 있는 상태일 때만 1개 소모. 소모에 실패하면(수량 0 등) 설치하지 않음
            if (useInventory && !PlatformInventoryLocator.Current.TryConsumeHeldPlatform())
                return;

            GameObject placed = Instantiate(platformPrefab, pos, rot);
            placed.AddComponent<PlacedPlatform>();   // "설치된 발판" 표식 (파괴·선택에 사용)
        }
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
        PlacedPlatform target = FindAimedPlatform();
        if (target != null)
        {
            selected = target;
            selected.SetSelected(true);
        }
        // 대상이 없거나 발판이 아니면 무반응
    }

    private void HandleLeftClickInSelectMode()
    {
        PlacedPlatform target = selected;

        // 모드 조건 재검증: 대상이 아직 1.5m 이내에 있는가?
        bool inRange = false;
        if (target != null && target.BodyCollider != null)
        {
            Vector3 closest = target.BodyCollider.ClosestPoint(cameraPivot.position);
            inRange = Vector3.Distance(cameraPivot.position, closest) <= selectDistance;
        }

        CancelMode();   // 모드 해제 (색 원래대로)

        if (inRange)
            target.Break();   // 발판 파괴
        // 범위를 벗어났으면 파괴 없이 모드만 해제
    }

    /// <summary>선택 모드 해제. 낙하 시작 시(3.4)에도 이 함수를 호출하면 됩니다.</summary>
    public void CancelMode()
    {
        if (selected != null)
            selected.SetSelected(false);
        selected = null;
    }

    private PlacedPlatform FindAimedPlatform()
    {
        Ray ray = new Ray(cameraPivot.position, cameraPivot.forward);

        // 에임 방향으로 쏴서 맨 처음 맞은 물체가 설치된 발판인지 확인 (바닥·벽이 먼저면 null)
        if (Physics.Raycast(ray, out RaycastHit hit, selectDistance, ~0, QueryTriggerInteraction.Ignore))
            return hit.collider.GetComponentInParent<PlacedPlatform>();

        return null;
    }

    private void SetPreviewColor(Color color)
    {
        foreach (Renderer r in previewRenderers)
            r.material.color = color;
    }
}
