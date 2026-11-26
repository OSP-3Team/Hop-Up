using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD 오른쪽 높이 바(NOW)
///
/// - 플레이어의 실제 발 높이(FootHeight)를 0m ~ 엔딩 높이(300m)로 환산해 세로 바를 실시간으로 채움
/// - 현재 높이 숫자(예: 120m)를 표시
/// - 75·150·225m 지대 구분선을 자동으로 그림 (EconomyHub의 지대 높이 설정을 사용, 없으면 FallController의 Fallback Zone Heights)
/// - 맨 위(300m)에 달 아이콘 배치
///
/// 사용법: Canvas 안의 "HeightBar"(바 전체 배경 Image) 오브젝트에 붙입니다.
///   Fill      : HeightBar 안의 채워지는 Image
///   Moon Icon : HeightBar 안의 달 Image (나중에 Sprite만 바꿔 끼우면 됨)
///   Height Text : 현재 높이를 보여줄 TextMeshPro 글자
/// Player, Fall Controller 칸은 비워 두면 씬에서 자동으로 찾습니다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class HeightBar : MonoBehaviour
{
    [Header("참조")]
    [SerializeField, Tooltip("HeightBar 안의 채워지는 Image")]
    private RectTransform fill;
    [SerializeField, Tooltip("맨 위(300m)에 놓일 달 Image. 비워 두면 달 표시 없음")]
    private RectTransform moonIcon;
    [SerializeField, Tooltip("현재 높이를 보여줄 TextMeshPro 글자. 비워 두면 숫자 표시 없음")]
    private TMP_Text heightText;

    [Header("자동 탐색 (비워 두면 씬에서 찾음)")]
    [SerializeField] private PlayerController player;
    [SerializeField] private FallController fallController;

    [Header("지대 구분선")]
    [SerializeField] private Color lineColor = Color.white;
    [SerializeField, Tooltip("구분선 두께(픽셀)")]
    private float lineThickness = 3f;
    [SerializeField, Tooltip("구분선이 바 양옆으로 튀어나오는 길이 합계(픽셀)")]
    private float lineExtraWidth = 10f;

    private bool linesBuilt;
    private float lastRatio = -1f;
    private int lastShownMeters = int.MinValue;
    private float nextFindTime;

    private void Start()
    {
        TryFindReferences();
        PlaceMoonIcon();
        TryBuildLines();
    }

    // 플레이어가 이동한 뒤의 높이를 읽기 위해 LateUpdate 사용
    private void LateUpdate()
    {
        if (player == null || fallController == null)
        {
            // 플레이어가 늦게 생겨도 연결되도록 1초마다 다시 찾음
            if (Time.unscaledTime >= nextFindTime)
            {
                nextFindTime = Time.unscaledTime + 1f;
                TryFindReferences();
                TryBuildLines();
            }

            if (player == null || fallController == null) return;
        }

        float foot = player.FootHeight;
        float maxHeight = Mathf.Max(0.01f, fallController.EndingHeight);

        UpdateFill(Mathf.Clamp01(foot / maxHeight));
        UpdateText(foot);
    }

    private void TryFindReferences()
    {
        if (player == null)
            player = FindFirstObjectByType<PlayerController>();

        if (fallController == null)
            fallController = FindFirstObjectByType<FallController>();
    }

    private void UpdateFill(float ratio)
    {
        if (fill == null || Mathf.Approximately(ratio, lastRatio)) return;
        lastRatio = ratio;

        // 바 아래(0)부터 ratio 높이까지 채움
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(1f, ratio);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;
    }

    private void UpdateText(float foot)
    {
        if (heightText == null) return;

        int meters = Mathf.Max(0, Mathf.FloorToInt(foot));
        if (meters == lastShownMeters) return;
        lastShownMeters = meters;

        heightText.text = meters + "m";
    }

    // 달 아이콘을 바의 맨 위 가운데에 놓음
    private void PlaceMoonIcon()
    {
        if (moonIcon == null) return;

        moonIcon.anchorMin = new Vector2(0.5f, 1f);
        moonIcon.anchorMax = new Vector2(0.5f, 1f);
        moonIcon.anchoredPosition = Vector2.zero;
    }

    // 지대 구분선 만들기 (FallController를 찾은 뒤 딱 한 번)
    private void TryBuildLines()
    {
        if (linesBuilt || fallController == null) return;
        linesBuilt = true;

        float maxHeight = Mathf.Max(0.01f, fallController.EndingHeight);

        foreach (float h in fallController.ZoneHeights)
        {
            float ratio = Mathf.Clamp01(h / maxHeight);

            GameObject go = new GameObject("MilestoneLine_" + Mathf.RoundToInt(h), typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, ratio);
            rt.anchorMax = new Vector2(1f, ratio);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(lineExtraWidth, lineThickness);

            Image img = go.GetComponent<Image>();
            img.color = lineColor;
            img.raycastTarget = false;
        }
    }
}
