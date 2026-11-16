using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// WBS 3.1 플레이어 이동(WASD·점프, IN-01) + 3.2 플레이어 시점(1인칭·고정 에임, UI-07)
///
/// 사용법: Player 오브젝트에 붙이고 Inspector에서 참조 3개를 연결합니다.
/// 주의: Player의 피벗(transform.position)은 "발 위치"입니다. (높이 판정·접지 판정이 발 기준이라서)
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Transform cameraPivot;   // 카메라가 들어있는 눈 높이 오브젝트
    [SerializeField] private Transform groundCheck;   // 발 위치(Player 바닥)에 둔 빈 오브젝트
    [SerializeField] private LayerMask groundMask;    // Ground 레이어 (바닥·발판 전부 포함)

    [Header("이동")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -20f;
    [Range(0f, 1f)]
    [SerializeField] private float airControl = 1f;   // 공중 이동 배율 (낙하 중에도 WASD 허용)

    [Header("접지 판정")]
    [SerializeField] private float groundCheckRadius = 0.2f;

    [Header("시점")]
    [SerializeField] private float mouseSensitivity = 0.1f;  // 나중에 설정 화면(UI-08)과 연결
    [SerializeField] private float maxLookAngle = 85f;

    // 다른 스크립트(낙하 판정, 높이 도달 판정, 메뉴 등)에서 읽거나 제어할 값들
    public bool InputEnabled { get; set; } = true;      // 메뉴·I/E 창이 열리면 false
    public bool IsGrounded { get; private set; }
    public float GroundedTime { get; private set; }     // 접지 유지 시간 (높이 도달 판정의 0.1초 조건용)
    public float FootHeight => transform.position.y;    // 발 위치 높이

    private CharacterController cc;
    private float verticalVelocity;
    private float xRotation;

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
        if (InputEnabled) Look();
        Move();
    }

    private void CheckGround()
    {
        IsGrounded = Physics.CheckSphere(
            groundCheck.position, groundCheckRadius, groundMask, QueryTriggerInteraction.Ignore);

        GroundedTime = IsGrounded ? GroundedTime + Time.deltaTime : 0f;
    }

    private void Look()
    {
        if (Mouse.current == null) return;

        // 마우스 delta는 이미 프레임당 이동량이라 Time.deltaTime을 곱하지 않습니다.
        Vector2 delta = Mouse.current.delta.ReadValue() * mouseSensitivity;

        xRotation = Mathf.Clamp(xRotation - delta.y, -maxLookAngle, maxLookAngle);
        cameraPivot.localRotation = Quaternion.Euler(xRotation, 0f, 0f);  // 상하: 카메라만
        transform.Rotate(Vector3.up * delta.x);                           // 좌우: 플레이어 전체
    }

    private void Move()
    {
        Keyboard kb = Keyboard.current;
        Vector2 input = Vector2.zero;
        bool jumpPressed = false;

        if (InputEnabled && kb != null)
        {
            input.x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            input.y = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
            jumpPressed = kb.spaceKey.wasPressedThisFrame;
        }

        input = Vector2.ClampMagnitude(input, 1f);  // 대각선 이동이 빨라지지 않게

        float speed = moveSpeed * (IsGrounded ? 1f : airControl);
        Vector3 horizontal = (transform.right * input.x + transform.forward * input.y) * speed;

        // 중력 · 점프 (점프는 접지 중에만)
        if (IsGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;  // 바닥에 살짝 붙여두기

        if (jumpPressed && IsGrounded)
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        verticalVelocity += gravity * Time.deltaTime;

        cc.Move((horizontal + Vector3.up * verticalVelocity) * Time.deltaTime);
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}