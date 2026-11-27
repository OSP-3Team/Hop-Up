
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// WBS 3.1 플레이어 이동(WASD, 점프)
/// WBS 3.2 플레이어 시점(1인칭, 고정 에임)
///
/// Player 오브젝트에 부착
/// Camera Pivot, Ground Mask를 Inspector에서 연결
/// Player Transform은 발 위치 기준
/// </summary>

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private LayerMask groundMask;

    [Header("이동")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -20f;

    [Range(0f, 1f)]
    [SerializeField] private float airControl = 1f;

    [Header("테스트용 날기 모드")]
    [SerializeField, Tooltip("켜면 중력 없이 날 수 있음. Space 상승 / Left Shift 하강 / WASD 수평 이동. 꺼도 그 높이에서 새 낙하로 계산됨")]
    private bool flyMode = false;   // [날기 모드]
    [SerializeField, Tooltip("날기 모드에서 위·아래 이동 속도")]
    private float flyVerticalSpeed = 6f;   // [날기 모드]

    [Header("접지 판정")]
    [SerializeField] private float groundCheckRadius = 0.1f;
    [SerializeField] private float groundCheckDistance = 0.08f;

    [Header("효과음")]
    [SerializeField, Tooltip("걸을 때 발소리 간격(초)")]
    private float footstepInterval = 0.3f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.6f;

    [Header("시점")]
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float maxLookAngle = 85f;

    // 외부 스크립트에서 접근
    public bool InputEnabled { get; set; } = true;
    public bool IsGrounded { get; private set; }
    public float GroundedTime { get; private set; }
    public float FootHeight => transform.position.y;

    // [3.4] 낙하 중 클릭·Space 같은 행동 입력을 막을 때 FallController가 끄고 켬 (시점·이동은 영향 없음)
    public bool ActionInputEnabled { get; set; } = true;

    // [3.4] 지금 밟고 있는 바닥의 콜라이더 (공중이면 null). 착지한 곳이 설치 발판인지 확인할 때 사용
    public Collider GroundCollider { get; private set; }

    // [날기 모드] 지금 날기 모드인가 (FallController가 낙하 계산을 멈추는 데 사용)
    public bool IsFlying => flyMode;

    private CharacterController cc;

    private float verticalVelocity;
    private float xRotation;

    private bool wasGrounded;
    private float footstepTimer;
    private AudioSource footstepSource;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();

        footstepSource = gameObject.AddComponent<AudioSource>();
        footstepSource.playOnAwake = false;   // 2D(spatialBlend 0): 내 발소리라 위치감 불필요
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        CheckGround();

        if (InputEnabled)
            Look();

        Move();
    }

    // =========================================
    // 효과음 (발소리)
    // =========================================

    private void UpdateFootsteps()
    {
        // 입력이 아닌 실제 이동으로 판단: 투명 벽에 막혀 제자리걸음일 때는 소리 없음
        Vector3 v = cc.velocity;
        bool moving = new Vector2(v.x, v.z).sqrMagnitude > 0.01f;

        if (!IsGrounded || !moving)
        {
            footstepTimer = 0f;   // 멈췄다 다시 걸으면 첫 발소리는 바로
            footstepSource.Stop();   // 클립 끝까지 기다리지 않고 멈추면 바로 끊음
            return;
        }

        footstepTimer -= Time.deltaTime;
        if (footstepTimer > 0f) return;
        footstepTimer = footstepInterval;

        // 설치한 발판 위면 나무 발소리, 그 외 땅은 FootstepGround
        bool onPlatform = GroundCollider != null && GroundCollider.GetComponentInParent<PlacedPlatform>() != null;
        AudioClip clip = Sfx.Pick(onPlatform ? "FootstepWood" : "FootstepGround");
        if (clip == null) return;

        // 소스 하나로 재생: 새 발소리가 이전 발소리를 대신해서 겹치지 않음
        footstepSource.clip = clip;
        footstepSource.volume = sfxVolume;
        footstepSource.Play();
    }

    // =========================================
    // 접지 판정
    // =========================================


    private void CheckGround()
    {
        bool touching = false;
        string groundName = "";
        Collider groundCollider = null;   // [3.4]

        if (verticalVelocity <= 0f)
        {
            Vector3 footPosition = transform.position
                + cc.center
                - Vector3.up * (cc.height / 2f);

            // 발 위치에서 아래 방향으로 검사
            Vector3 origin = footPosition + Vector3.up * 0.1f;

            if (Physics.Raycast(
                origin,
                Vector3.down,
                out RaycastHit hit,
                0.3f,
                groundMask,
                QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(transform))
                {
                    touching = true;
                    groundName = hit.collider.name;
                    groundCollider = hit.collider;   // [3.4]
                }
            }
        }

        IsGrounded = touching;
        GroundCollider = touching ? groundCollider : null;   // [3.4]

        GroundedTime = IsGrounded
            ? GroundedTime + Time.deltaTime
            : 0f;
    }


    // =========================================
    // [8.2 세이브] 불러오기용: 발 위치와 좌우 방향을 지정해서 이동
    // =========================================

    public void TeleportTo(Vector3 footPosition, float yaw)
    {
        if (cc == null) cc = GetComponent<CharacterController>();

        cc.enabled = false;   // CharacterController가 켜져 있으면 위치를 덮어써서 꺼 두고 옮김
        transform.position = footPosition;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        cc.enabled = true;

        verticalVelocity = 0f;
        GroundedTime = 0f;
    }

    // =========================================
    // 마우스 시점
    // =========================================

    private void Look()
    {
        if (Mouse.current == null)
            return;

        Vector2 delta =
            Mouse.current.delta.ReadValue() * mouseSensitivity;

        xRotation = Mathf.Clamp(
            xRotation - delta.y,
            -maxLookAngle,
            maxLookAngle
        );

        if (cameraPivot != null)
        {
            cameraPivot.localRotation =
                Quaternion.Euler(xRotation, 0f, 0f);
        }

        transform.Rotate(Vector3.up * delta.x);
    }

    // =========================================
    // 플레이어 이동 및 점프
    // =========================================

    private void Move()
    {
        Keyboard kb = Keyboard.current;

        Vector2 input = Vector2.zero;
        bool jumpPressed = false;

        if (InputEnabled && kb != null)
        {
            input.x =
                (kb.dKey.isPressed ? 1f : 0f)
                - (kb.aKey.isPressed ? 1f : 0f);

            input.y =
                (kb.wKey.isPressed ? 1f : 0f)
                - (kb.sKey.isPressed ? 1f : 0f);

            jumpPressed = kb.spaceKey.wasPressedThisFrame && ActionInputEnabled;   // [3.4] 낙하 중 Space 무시
        }

        // 대각선 이동 속도 제한
        input = Vector2.ClampMagnitude(input, 1f);

        // [날기 모드] 중력·점프 대신 Space(상승) / Left Shift(하강)로 자유롭게 이동
        if (flyMode)
        {
            float up = 0f;
            if (InputEnabled && kb != null)
                up = (kb.spaceKey.isPressed ? 1f : 0f) - (kb.leftShiftKey.isPressed ? 1f : 0f);

            verticalVelocity = 0f;

            Vector3 flyMove = (transform.right * input.x + transform.forward * input.y) * moveSpeed
                              + Vector3.up * (up * flyVerticalSpeed);
            cc.Move(flyMove * Time.deltaTime);
            return;
        }

        // 지상 및 공중 이동 속도
        float speed = moveSpeed *
            (IsGrounded ? 1f : airControl);

        Vector3 horizontal =
            (transform.right * input.x
            + transform.forward * input.y) * speed;

        // 접지 중에는 바닥에 붙도록 처리
        if (IsGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        // 접지 상태에서만 점프 가능
        if (jumpPressed && IsGrounded)
        {
            verticalVelocity =
                Mathf.Sqrt(jumpHeight * -2f * gravity);

            IsGrounded = false;
            GroundedTime = 0f;
        }

        UpdateFootsteps();

        // 중력 적용
        verticalVelocity += gravity * Time.deltaTime;

        // 최종 이동
        CollisionFlags flags = cc.Move(
            (horizontal + Vector3.up * verticalVelocity)
            * Time.deltaTime
        );

        // 천장에 부딪혔을 때 상승 속도 초기화
        if ((flags & CollisionFlags.Above) != 0
            && verticalVelocity > 0f)
        {
            verticalVelocity = 0f;
        }
    }

    // =========================================
    // 접지 검사 위치 표시
    // =========================================

    private void OnDrawGizmosSelected()
    {
        CharacterController controller =
            GetComponent<CharacterController>();

        if (controller == null)
            return;

        Vector3 footPosition = transform.position
            + controller.center
            - Vector3.up * (controller.height / 2f);

        Vector3 checkPosition =
            footPosition + Vector3.up * 0.02f;

        Gizmos.color = Color.green;

        Gizmos.DrawWireSphere(
            checkPosition + Vector3.down * groundCheckDistance,
            groundCheckRadius
        );
    }
}
