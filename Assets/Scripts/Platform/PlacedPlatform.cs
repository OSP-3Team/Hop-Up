using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

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

    // [종류별 발판·세이브] 어떤 아이템(발판 종류)으로 설치했는지. 예: scaffold_1
    public string KindId { get; private set; } = "default";

    // [세이브] 씬에 있는 설치된 발판 전체 목록
    private static readonly List<PlacedPlatform> all = new List<PlacedPlatform>();
    public static IReadOnlyList<PlacedPlatform> All => all;

    public void SetKind(string kindId)
    {
        if (!string.IsNullOrEmpty(kindId)) KindId = kindId;
    }

    private void OnEnable()
    {
        all.Add(this);
    }

    private void OnDisable()
    {
        all.Remove(this);
    }

    private void Awake()
    {
        BodyCollider = GetComponentInChildren<Collider>();
        AddNavObstacle();

        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            originalColors[i] = renderers[i].material.color;
    }

    // 농사 봇(NavMeshAgent)이 바닥에 설치된 발판을 돌아가게 NavMesh에 구멍을 낸다(carving). NavMesh가 없는 곳에선 아무 일도 안 함
    private void AddNavObstacle()
    {
        if (BodyCollider == null || GetComponent<NavMeshObstacle>() != null) return;
        NavMeshObstacle obstacle = gameObject.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.carving = true;
        if (BodyCollider is BoxCollider box && box.transform == transform)
        {
            obstacle.center = box.center;
            obstacle.size = box.size;
        }
        else
        {
            // shortcut: 자식 콜라이더는 월드 bounds로 근사(회전하면 실제보다 큼), 모양이 복잡한 발판이 생기면 콜라이더별로
            Bounds b = BodyCollider.bounds;
            Vector3 s = transform.lossyScale;
            obstacle.center = transform.InverseTransformPoint(b.center);
            obstacle.size = new Vector3(b.size.x / s.x, b.size.y / s.y, b.size.z / s.z);
        }
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
        // TODO: 파괴 이펙트 추가 (SYS 완성 단계에서)
        Sfx.Play("PlatformBreak", transform.position);
        Destroy(gameObject);
    }
}
