using UnityEngine;

/// <summary>
/// 플레이어의 현재 높이에 따라 Procedural 스카이박스 색을 바꾼다.
/// 떨어지면 낮은 하늘로 돌아간다 (지대 단계 ZoneProgress와는 별개).
///
/// 씬의 빈 오브젝트(Environment)에 부착. 스카이박스는 Skybox/Procedural 셰이더여야 함.
/// 그라디언트·커브 키는 0 / 0.25 / 0.5 / 0.75 / 1 = 0 / 75 / 150 / 225 / 300m (maxHeight 300 기준)
/// </summary>
public class HeightSkyController : MonoBehaviour
{
    private static readonly int SkyTintId = Shader.PropertyToID("_SkyTint");
    private static readonly int GroundColorId = Shader.PropertyToID("_GroundColor");
    private static readonly int AtmosphereId = Shader.PropertyToID("_AtmosphereThickness");
    private static readonly int ExposureId = Shader.PropertyToID("_Exposure");

    [SerializeField] private float maxHeight = 300f;

    // 지상(기본 하늘) → 맑은 하늘 → 높은 하늘 → 성층권 → 우주
    [SerializeField] private Gradient skyTint = Grad(
        new Color(0.5f, 0.5f, 0.5f),
        new Color(0.45f, 0.6f, 0.85f),
        new Color(0.3f, 0.4f, 0.8f),
        new Color(0.3f, 0.22f, 0.55f),
        new Color(0.1f, 0.1f, 0.2f));

    // 아래쪽(지평선 밑) 색. 올라갈수록 구름 바다처럼 밝게
    [SerializeField] private Gradient groundColor = Grad(
        new Color(0.369f, 0.349f, 0.341f),
        new Color(0.6f, 0.65f, 0.7f),
        new Color(0.85f, 0.88f, 0.92f),
        new Color(0.7f, 0.72f, 0.8f),
        new Color(0.3f, 0.3f, 0.4f));

    [SerializeField] private AnimationCurve atmosphere = Curve(1f, 1f, 0.8f, 0.45f, 0.15f);
    [SerializeField] private AnimationCurve exposure = Curve(1.3f, 1.3f, 1.2f, 0.9f, 0.5f);

    private PlayerController player;
    private Material sky;
    private int lastGiStep = -1;

    private void Start()
    {
        player = FindAnyObjectByType<PlayerController>();
        if (player == null || RenderSettings.skybox == null)
        {
            Debug.LogWarning("[HeightSky] PlayerController 또는 스카이박스가 없어 비활성화");
            enabled = false;
            return;
        }

        // 원본 에셋을 직접 바꾸면 에디터에서 플레이 종료 후에도 변경이 남으므로 복사본 사용
        sky = new Material(RenderSettings.skybox);
        RenderSettings.skybox = sky;
    }

    private void Update()
    {
        float t = Mathf.Clamp01(player.FootHeight / maxHeight);

        sky.SetColor(SkyTintId, skyTint.Evaluate(t));
        sky.SetColor(GroundColorId, groundColor.Evaluate(t));
        sky.SetFloat(AtmosphereId, atmosphere.Evaluate(t));
        sky.SetFloat(ExposureId, exposure.Evaluate(t));

        // 환경광 갱신은 비싸서 매 프레임 대신 높이 5% 구간(15m)마다 한 번
        int step = Mathf.RoundToInt(t * 20f);
        if (step != lastGiStep)
        {
            lastGiStep = step;
            DynamicGI.UpdateEnvironment();
        }
    }

    private void OnDestroy()
    {
        if (sky != null) Destroy(sky);
    }

    // 색을 0~1 구간에 같은 간격으로 배치
    private static Gradient Grad(params Color[] colors)
    {
        var keys = new GradientColorKey[colors.Length];
        for (int i = 0; i < colors.Length; i++)
            keys[i] = new GradientColorKey(colors[i], i / (float)(colors.Length - 1));

        var g = new Gradient();
        g.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }

    // 값을 0~1 구간에 같은 간격으로 배치
    private static AnimationCurve Curve(params float[] values)
    {
        var keys = new Keyframe[values.Length];
        for (int i = 0; i < values.Length; i++)
            keys[i] = new Keyframe(i / (float)(values.Length - 1), values[i]);
        return new AnimationCurve(keys);
    }
}
