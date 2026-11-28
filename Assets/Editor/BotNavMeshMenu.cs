using System.Collections.Generic;
using System.IO;
using HopUp.FarmEconomy;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Tools > Hop-Up > Bake Bot NavMesh: MainGame 농장 바닥(Plane) 위에 농사 봇이 다닐 NavMesh를 굽는다.
/// 바닥 위 -1~+5m만 굽고(위쪽 지대는 제외), 콜라이더만 장애물로 본다(MapDecor: 건물·큰 바위만 콜라이더, 나무·풀은 통과).
/// 장식·벽·밭 배치를 바꾸면 다시 실행. 설치된 발판은 PlacedPlatform의 NavMeshObstacle이 실시간으로 구멍을 낸다.
/// 배치 실행: Unity -batchmode -projectPath . -executeMethod BotNavMeshMenu.BuildAndSave -quit
/// </summary>
public static class BotNavMeshMenu
{
    const string ScenePath = "Assets/MainGame.unity";
    const string GroundName = "Plane";        // FarmWallsMenu와 같은 농장 바닥
    const string RootName = "BotNavMesh";
    const string AssetPath = "Assets/MainGame/NavMesh-BotNavMesh.asset";  // Unity 기본 규칙: 씬 이름 폴더
    const float Below = 1f, Above = 5f;

    [MenuItem("Tools/Hop-Up/Bake Bot NavMesh")]
    public static void BuildFromMenu()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
        if (Bake(GameObject.Find(GroundName), AssetPath) != null)
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    public static void BuildAndSave()
    {
        EditorSceneManager.OpenScene(ScenePath);
        if (Bake(GameObject.Find(GroundName), AssetPath) != null)
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    }

    /// <summary>
    /// 손으로 고친 BotTest 씬을 다시 만들지 않고 장애물(없을 때만)을 넣고 NavMesh를 굽는다: -executeMethod BotNavMeshMenu.BuildBotTestAndSave
    /// 장애물: 봇 줄(z=4)과 밭 줄(z=0) 사이 z=2에 벽 두 개, 가운데(x 2~4)만 틈 → 양쪽 봇이 가운데로 돌아 들어가야 함
    /// </summary>
    public static void BuildBotTestAndSave()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/BotTest/BotTest.unity");
        var soil = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scenes/BotTest/Soil.mat");
        Obstacle("Obstacle_L", new Vector3(0f, 0.5f, 2f), new Vector3(4f, 1f, 0.4f), soil);
        Obstacle("Obstacle_R", new Vector3(6f, 0.5f, 2f), new Vector3(4f, 1f, 0.4f), soil);
        if (Bake(GameObject.Find("Ground"), "Assets/Scenes/BotTest/NavMesh.asset") == null) return;
        LogBotPaths();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    }

    /// <summary>MainGame 봇이 구운 NavMesh 위에서 출발하는지 로그로 확인(저장 안 함): -executeMethod BotNavMeshMenu.CheckMainGame</summary>
    public static void CheckMainGame()
    {
        EditorSceneManager.OpenScene(ScenePath);
        LogBotPaths();
    }

    static void Obstacle(string name, Vector3 pos, Vector3 size, Material mat)
    {
        if (GameObject.Find(name) != null) return;  // 이미 있으면(손으로 옮겼을 수도) 건드리지 않음
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = size;
        if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    // 굽기 확인용: 봇마다 가장 가까운 밭까지 경로 상태·길이를 로그로 남긴다(직선거리보다 길면 돌아가는 것)
    static void LogBotPaths()
    {
        var path = new NavMeshPath();
        FarmPlot[] plots = Object.FindObjectsByType<FarmPlot>();
        foreach (FarmBot bot in Object.FindObjectsByType<FarmBot>(FindObjectsInactive.Include))
        {
            NavMeshHit from;
            // FarmBot.SetupAgent와 같은 반경(2m). 실패하면 그 봇은 플레이 때 직선 이동
            if (!NavMesh.SamplePosition(bot.transform.position, out from, 2f, NavMesh.AllAreas)) { Debug.LogWarning($"[BotNavMesh] {bot.name} {bot.transform.position}: NavMesh 밖 → 직선 이동"); continue; }
            FarmPlot best = null;
            foreach (FarmPlot p in plots)
                if (best == null || (p.transform.position - from.position).sqrMagnitude < (best.transform.position - from.position).sqrMagnitude) best = p;
            if (best == null) return;
            NavMeshHit to;
            NavMesh.SamplePosition(best.transform.position, out to, 1f, NavMesh.AllAreas);
            NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path);
            float len = 0f;
            for (int i = 1; i < path.corners.Length; i++) len += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            Debug.Log($"[BotNavMesh] {bot.name} {bot.transform.position} (NavMesh까지 {from.distance:F2}m) → {best.name}: {path.status}, 꺾임 {path.corners.Length}점, 경로 {len:F1}m (직선 {Vector3.Distance(from.position, to.position):F1}m)");
        }
    }

    /// <summary>ground 렌더러 범위(XZ) × 바닥 위 -1~+5m를 굽고 assetPath에 저장. 기존 BotNavMesh 루트는 지우고 다시 만든다.</summary>
    public static NavMeshSurface Bake(GameObject ground, string assetPath)
    {
        if (ground == null || ground.GetComponent<Renderer>() == null)
        {
            Debug.LogError("[BotNavMesh] 바닥(렌더러가 있는 지면 오브젝트)이 없습니다.");
            return null;
        }
        GameObject old = GameObject.Find(RootName);
        if (old != null && old.transform.parent == null) Object.DestroyImmediate(old);

        // Plane은 크기·회전이 들어가 있어서 따로 둔다(NavMeshSurface 볼륨은 스케일을 무시함)
        Bounds b = ground.GetComponent<Renderer>().bounds;
        var go = new GameObject(RootName);
        go.transform.position = new Vector3(b.center.x, b.max.y, b.center.z);

        NavMeshSurface surface = go.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Volume;
        surface.center = new Vector3(0f, (Above - Below) / 2f, 0f);
        surface.size = new Vector3(b.size.x, Above + Below, b.size.z);
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

        // 움직이는 몸(봇 캡슐·플레이어)이 NavMesh 구멍으로 구워지지 않게 굽는 동안만 끈다
        var movers = new List<Collider>();
        foreach (FarmBot bot in Object.FindObjectsByType<FarmBot>(FindObjectsInactive.Include))
            movers.AddRange(bot.GetComponentsInChildren<Collider>());
        movers.AddRange(Object.FindObjectsByType<CharacterController>());
        movers.RemoveAll(c => !c.enabled);
        foreach (Collider c in movers) c.enabled = false;
        try { surface.BuildNavMesh(); }
        finally { foreach (Collider c in movers) c.enabled = true; }

        if (surface.navMeshData == null)
        {
            Debug.LogError("[BotNavMesh] 굽기 실패");
            Object.DestroyImmediate(go);
            return null;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
        AssetDatabase.DeleteAsset(assetPath);
        AssetDatabase.CreateAsset(surface.navMeshData, assetPath);
        EditorUtility.SetDirty(surface);
        Debug.Log($"[BotNavMesh] {b.size.x:F1} x {b.size.z:F1}m 굽기 완료 → {assetPath}");
        return surface;
    }
}
