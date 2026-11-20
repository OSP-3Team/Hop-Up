using UnityEngine;

/// <summary>
/// 플레이어가 설치한 발판에 PlatformPlacer가 자동으로 붙여주는 표식 스크립트.
/// - "이 오브젝트는 설치된 발판이다"를 알려주고
/// - 선택 모드일 때 색을 바꾸고
/// - 파괴 처리를 담당합니다.
///
/// 직접 붙일 필요 없음 (PlatformPlacer가 설치할 때 코드로 붙임).
/// 나중에 3.4 낙하 판정에서 "착지한 발판 1개 파괴"를 할 때도 Break()를 호출하면 됩니다.
/// </summary>
public class PlacedPlatform : MonoBehaviour
{
    private static readonly Color SelectedColor = new Color(1f, 0.75f, 0.1f);  // 선택 모드: 주황/노랑

    private Renderer[] renderers;
    private Color[] originalColors;

    public Collider BodyCollider { get; private set; }

    private void Awake()
    {
        BodyCollider = GetComponentInChildren<Collider>();

        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            originalColors[i] = renderers[i].material.color;
    }

    /// <summary>선택 모드 표시 켜기/끄기 (색만 바뀜)</summary>
    public void SetSelected(bool selected)
    {
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].material.color = selected ? SelectedColor : originalColors[i];
    }

    /// <summary>발판 파괴</summary>
    public void Break()
    {
        // TODO: 파괴 이펙트·사운드 추가 (SYS 완성 단계에서)
        Destroy(gameObject);
    }
}
