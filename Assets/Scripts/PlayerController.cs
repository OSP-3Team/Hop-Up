
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

    [Header("접지 판정")]
    [SerializeField] private float groundCheckRadius = 0.1f;
    [SerializeField] private float groundCheckDistance = 0.08f;

    [SerializeField] private bool logGroundState = true;

    [Header("시점")]
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float maxLookAngle = 85f;

    // 외부 스크립트에서 접근
    public bool InputEnabled { get; set; } = true;
    public bool IsGrounded { get; private set; }
    public float GroundedTime { get; private set; }
    public float FootHeight => transform.position.y;

    private CharacterController cc;

    private float verticalVelocity;
    private float xRotation;

    private bool wasGrounded;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
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
    // 접지 판정
    // =========================================


    private void CheckGround()
    {
        bool touching = false;
        string groundName = "";

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
                }
            }
        }

        IsGrounded = touching;

        GroundedTime = IsGrounded
            ? GroundedTime + Time.deltaTime
            : 0f;
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

            jumpPressed = kb.spaceKey.wasPressedThisFrame;
        }

        // 대각선 이동 속도 제한
        input = Vector2.ClampMagnitude(input, 1f);

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
