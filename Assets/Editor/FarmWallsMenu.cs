using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Hop-Up > Build Farm Walls: MainGame 농장 바닥(Plane) 가장자리 네 면에 투명 벽(BoxCollider만, 렌더러 없음)을 세운다.
/// 땅 아래 5m부터 엔딩 높이(300m)보다 높이까지 막아서, 높은 발판·지대(75·150·225m)에서도 농장 밖으로 못 나감.
/// 발판 설치 검사(PlatformPlacer)가 모든 콜라이더를 보므로 벽에 걸치는 자리엔 발판도 못 놓음.
/// 기존 FarmWalls 루트를 지우고 다시 만든다(몇 번 돌려도 같은 결과).
/// 배치 실행: Unity -batchmode -projectPath . -executeMethod FarmWallsMenu.BuildAndSave -quit
/// </summary>
public static class FarmWallsMenu
{
    const string ScenePath = "Assets/MainGame.unity";
    const string RootName = "FarmWalls";
    const string GroundName = "Plane";
    const float Bottom = -5f;      // 바닥 판 아래까지 내려서 틈이 없게
    const float Top = 330f;        // 엔딩 높이 300m + 여유
    const float Thickness = 1f;

    [MenuItem("Tools/Hop-Up/Build Farm Walls")]
    public static void BuildFromMenu()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
        GameObject root = Build();
        if (root == null) return;
        Undo.RegisterCreatedObjectUndo(root, "Build Farm Walls");
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    public static void BuildAndSave()
    {
        EditorSceneManager.OpenScene(ScenePath);
        if (Build() != null) EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    }

    static GameObject Build()
    {
        GameObject ground = GameObject.Find(GroundName);
        if (ground == null || ground.GetComponent<Renderer>() == null)
        {
            Debug.LogError("[FarmWalls] MainGame에 " + GroundName + " 바닥이 없습니다.");
            return null;
        }
        GameObject old = GameObject.Find(RootName);
        if (old != null && old.transform.parent == null) Object.DestroyImmediate(old);

        Bounds b = ground.GetComponent<Renderer>().bounds;
        var root = new GameObject(RootName).transform;
        root.position = new Vector3(b.center.x, b.max.y, b.center.z);

        float hx = b.extents.x, hz = b.extents.z, h = Top - Bottom, y = Bottom + h / 2f, t = Thickness;
        Wall(root, "North", new Vector3(0f, y, hz + t / 2f), new Vector3(2f * (hx + t), h, t));
        Wall(root, "South", new Vector3(0f, y, -hz - t / 2f), new Vector3(2f * (hx + t), h, t));
        Wall(root, "East", new Vector3(hx + t / 2f, y, 0f), new Vector3(t, h, 2f * hz));
        Wall(root, "West", new Vector3(-hx - t / 2f, y, 0f), new Vector3(t, h, 2f * hz));

        Debug.Log($"[FarmWalls] {2f * hx:F1} x {2f * hz:F1}m 농장 둘레에 투명 벽 4개 (높이 {Bottom}~{Top}m)");
        return root.gameObject;
    }

    static void Wall(Transform root, string side, Vector3 localPos, Vector3 size)
    {
        var go = new GameObject("FarmWall_" + side); // 발판 설치가 막힐 때 로그에 이 이름이 나옴
        go.transform.SetParent(root, false);
        go.transform.localPosition = localPos;
        go.AddComponent<BoxCollider>().size = size;
    }
}
