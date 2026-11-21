using UnityEngine;

/// <summary>
/// 토끼의 오른팔(RightArm 뼈)을 카메라에 붙여서,
/// 시점을 위아래로 돌려도 손과 손에 든 아이템이 항상 같은 화면 위치에 보이게 합니다.
///
/// 사용법: Player 오브젝트에 붙이기만 하면 됩니다. (카메라와 RightArm 뼈는 자동으로 찾음)
/// 손에 든 아이템(HoldPoint)은 RightHand 뼈 아래에 있으니 팔을 따라 같이 움직입니다.
/// 위치·각도는 Inspector에서 ▶ 실행 중에 바꿔 보면서 맞추세요.
/// </summary>
public class FirstPersonArm : MonoBehaviour
{
    [Header("참조 (비워 두면 자동으로 찾음)")]
    [SerializeField] private Transform cameraPivot;   // 카메라(CameraPivot)
    [SerializeField] private Transform arm;           // 오른쪽 윗팔 뼈(RightArm)

    [Header("화면에서의 팔 위치")]
    [SerializeField, Tooltip("카메라 기준 윗팔 뿌리 위치 (오른쪽, 위, 앞). 팔은 여기서 정면으로 뻗어 나갑니다")]
    private Vector3 positionInView = new Vector3(0.08f, -0.08f, 0.08f);

    [SerializeField, Tooltip("팔 방향 보정(도). 0,0,0이면 정면을 똑바로 가리킴. Y를 음수로 하면 화면 가운데 쪽으로 기울어짐")]
    private Vector3 rotationOffset = Vector3.zero;

    private Quaternion restRotationInPlayer;
    private bool ready;

    private void Awake()
    {
        if (cameraPivot == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraPivot = cam.transform;
        }

        if (arm == null)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "RightArm")
                {
                    arm = t;
                    break;
                }
            }
        }

        if (cameraPivot == null || arm == null)
        {
            Debug.LogError("[FirstPersonArm] 카메라 또는 RightArm 뼈를 찾지 못했습니다. Inspector에서 직접 연결해 주세요.");
            return;
        }

        // 지금(원래 자세)의 팔 회전을 Player 기준으로 기억해 둠
        restRotationInPlayer = Quaternion.Inverse(transform.rotation) * arm.rotation;
        ready = true;
    }

    // 모든 Update(이동·시점 회전)가 끝난 뒤에 팔을 카메라에 맞춤
    private void LateUpdate()
    {
        if (!ready) return;

        arm.SetPositionAndRotation(
            cameraPivot.TransformPoint(positionInView),
            cameraPivot.rotation * Quaternion.Euler(rotationOffset) * restRotationInPlayer);
    }
}
