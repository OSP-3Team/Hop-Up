using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Hop-Up > Build Map Decor: MainGame을 '떠 있는 하늘섬'으로 꾸민다.
/// MapDecor 루트를 지우고 다시 만든다(고정 시드라 몇 번 돌려도 같은 결과).
/// 건물·큰 바위만 콜라이더, 나무·덤불·풀·작은 바위는 통과. 밭·봇·스폰 주변은 비워 둔다. 장식을 바꾸면 Tools/Hop-Up/Bake Bot NavMesh를 다시 실행(봇은 콜라이더를 돌아감).
/// 배치 실행: Unity -batchmode -projectPath . -executeMethod MapDecorMenu.BuildAndSave -quit
/// </summary>
public static class MapDecorMenu
{
    const string ScenePath = "Assets/MainGame.unity";
    const string RootName = "MapDecor";
    const string Kay = "Assets/ThirdParty/KayKit_Forest/Models/";
    const string Farm = "Assets/ThirdParty/Quaternius_FarmBuildings/Models/";
    const string GroundMatPath = "Assets/Materials/IslandGround.mat";
    const int Seed = 20261126;

    static readonly string[] Trees = { "Tree_1_A", "Tree_2_A", "Tree_2_B", "Tree_3_A", "Tree_3_B", "Tree_4_A", "Tree_4_B" };
    static readonly string[] Bushes = { "Bush_1_C", "Bush_2_B", "Bush_2_C", "Bush_3_B", "Bush_4_B" };
    static readonly string[] Grass = { "Grass_1_B", "Grass_1_C", "Grass_2_B", "Grass_2_C" };
    static readonly string[] SmallRocks = { "Rock_1_A", "Rock_1_B", "Rock_1_C", "Rock_2_B" };
    static readonly string[] BigUnderRocks = { "Rock_3_M", "Rock_3_N", "Rock_3_O", "Rock_3_P", "Rock_3_Q", "Rock_3_R" };

    static System.Random rng;
    static Transform root;
    static float half;               // 지상 판 반폭
    static Rect farmZone;            // 비워 둘 밭+봇 구역 (x,z)
    static Vector2 spawn;
    static readonly List<Vector3> occupied = new List<Vector3>();  // x,z = 중심, y = 반경

    [MenuItem("Tools/Hop-Up/Build Map Decor")]
    public static void BuildFromMenu()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
        Build();
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Build Map Decor");
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    public static void BuildAndSave()
    {
        EditorSceneManager.OpenScene(ScenePath);
        Build();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    }

    static void Build()
    {
        GameObject ground = Find("Plane");
        GameObject old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);

        rng = new System.Random(Seed);
        occupied.Clear();
        root = new GameObject(RootName).transform;
        half = ground.GetComponent<Renderer>().bounds.extents.x;
        spawn = XZ(Find("Player").transform.position);

        // 밭 전체 + 봇 위치를 감싸고 1m 여유
        Bounds farm = WorldBounds(Find("farms"));
        GameObject bot = GameObject.Find("Bot");
        if (bot != null) farm.Encapsulate(bot.transform.position);
        farmZone = Rect.MinMaxRect(farm.min.x - 1f, farm.min.z - 1f, farm.max.x + 1f, farm.max.z + 1f);

        ground.GetComponent<Renderer>().sharedMaterial = GroundMaterial();

        BuildBuildings(Group("Buildings"));
        BuildNature(Group("Nature"));
        BuildUnderside(Group("Underside"), ground.transform.position, half, 1f);
        foreach (string name in new[] { "Plane2", "Plane3", "Plane4" })
            BuildCheckpoint(Group("Checkpoint_" + name), Find(name));

        Debug.Log($"[MapDecor] {root.GetComponentsInChildren<Renderer>().Length}개 렌더러 배치 완료");
    }

    static void BuildBuildings(Transform g)
    {
        // 밭(북동쪽)을 피해 서쪽·남쪽에 배치. 콜라이더 있음
        Place(g, Farm + "SmallBarn", -5.5f, 6.2f, 90f, 0.75f, true, 3f);
        Place(g, Farm + "Silo", -8.4f, 8.4f, 0f, 0.6f, true, 1.2f);
        Place(g, Farm + "TowerWindmill", -6.5f, -6.5f, 45f, 0.55f, true, 2.5f);
        Place(g, Farm + "WaterTower", 7.8f, -7.8f, 0f, 0.6f, true, 1f);
        Place(g, Farm + "ChickenCoop", 4.2f, -6.5f, 200f, 0.8f, true, 1.3f);
        Place(g, Farm + "Well", -2.2f, -3.2f, 30f, 0.8f, true, 0.8f);
        Place(g, Kay + "Rock_1_J_Color1", -8.3f, 1.2f, 70f, 0.7f, true, 1.6f);
        Place(g, Kay + "Rock_2_E_Color1", 8.2f, -1.8f, 160f, 0.55f, true, 1.2f);

        // 밭 쪽 섬 가장자리(북·동)에 울타리. 통과 가능
        float edge = half - 0.5f;
        Place(g, Farm + "Fence", 4.0f, edge, 0f, 1f, false, 0f);
        Place(g, Farm + "Fence2", 7.05f, edge, 0f, 1f, false, 0f);
        Place(g, Farm + "Fence", edge, 4.0f, 90f, 1f, false, 0f);
        Place(g, Farm + "Fence2", edge, 7.05f, 90f, 1f, false, 0f);
    }

    static void BuildNature(Transform g)
    {
        // 가장자리 나무 띠, 그 안쪽 덤불 띠
        // inset은 반경 + 지터(0.4)만큼 안쪽이어야 Free()의 경계 검사를 통과한다
        // 반경은 줄기 기준이라 수관끼리는 겹쳐도 된다(건물 사이 틈을 채움)
        Ring(g, half - 1.5f, 1.8f, Trees, 0.6f, 0.85f, 0.8f, 1f);
        Ring(g, half - 3.0f, 1.6f, Bushes, 0.7f, 1f, 0.6f, 0.6f);

        for (int i = 0; i < 60; i++) Scatter(g, Grass, 0.8f, 1.2f, 0.3f);
        for (int i = 0; i < 10; i++) Scatter(g, SmallRocks, 0.8f, 1.2f, 0.4f);
    }

    static void Ring(Transform g, float inset, float step, string[] pool, float minS, float maxS, float radius, float chance)
    {
        for (float t = -inset; t <= inset; t += step)
            foreach (var p in new[] { new Vector2(t, -inset), new Vector2(t, inset), new Vector2(-inset, t), new Vector2(inset, t) })
            {
                Vector2 q = p + new Vector2(Range(-0.4f, 0.4f), Range(-0.4f, 0.4f));
                if (rng.NextDouble() > chance || !Free(q, radius)) continue;
                Place(g, Kay + Pick(pool) + "_Color1", q.x, q.y, Range(0f, 360f), Range(minS, maxS), false, radius);
            }
    }

    static void Scatter(Transform g, string[] pool, float minS, float maxS, float radius)
    {
        var p = new Vector2(Range(-half, half), Range(-half, half));
        if (Free(p, radius)) Place(g, Kay + Pick(pool) + "_Color1", p.x, p.y, Range(0f, 360f), Range(minS, maxS), false, radius);
    }

    // 판 아래에 바위 층을 쌓아 공중에 뜬 섬처럼 보이게 한다. 콜라이더 없음(낙하 동작 그대로)
    static void BuildUnderside(Transform g, Vector3 center, float h, float s)
    {
        float[] l1 = { -0.65f * h, 0f, 0.65f * h };
        foreach (float x in l1)
            foreach (float z in l1)
                Under(g, Pick(BigUnderRocks), center + new Vector3(x, 0f, z), 2.0f * s, 0.05f);
        foreach (float x in new[] { -0.35f * h, 0.35f * h })
            foreach (float z in new[] { -0.35f * h, 0.35f * h })
                Under(g, Pick(BigUnderRocks), center + new Vector3(x, 0f, z), 1.8f * s, 3.5f * s);
        Under(g, "Rock_3_R", center, 1.6f * s, 6.5f * s);
        Core(g, center, 2f * h * 0.95f, 9f * s);
    }

    // 바위 사이 틈을 막는 심: 가로로 늘린 큰 바위 하나. rng를 안 써서 나머지 배치는 그대로
    static void Core(Transform g, Vector3 top, float width, float height)
    {
        GameObject go = PlaceAt(g, Kay + "Rock_3_R_Color1", top, 0f, 1f, false);
        go.name = "CoreRock";
        Bounds b = WorldBounds(go);
        go.transform.localScale = Vector3.Scale(go.transform.localScale, new Vector3(width / b.size.x, height / b.size.y, width / b.size.z));
        b = WorldBounds(go);
        go.transform.position += new Vector3(top.x - b.center.x, top.y - 0.1f - b.max.y, top.z - b.center.z);
    }

    static void BuildCheckpoint(Transform g, GameObject plane)
    {
        Vector3 c = plane.transform.position;
        float e = plane.GetComponent<Renderer>().bounds.extents.x - 0.6f;  // 가운데 착지 공간은 비움
        Under(g, "Rock_3_Q", c, 1.8f, 0.05f);
        Under(g, "Rock_3_G", c, 1.2f, 3f);
        Core(g, c, 2f * (e + 0.6f) * 0.95f, 4f);
        PlaceAt(g, Kay + "Bush_2_B_Color1", c + new Vector3(-e, 0, e), 40f, 0.8f, false);
        PlaceAt(g, Kay + "Rock_1_B_Color1", c + new Vector3(e, 0, -e), 120f, 0.9f, false);
        PlaceAt(g, Kay + "Grass_2_C_Color1", c + new Vector3(e, 0, e), 200f, 1f, false);
        PlaceAt(g, Kay + "Grass_1_C_Color1", c + new Vector3(-e, 0, -e), 300f, 1f, false);
    }

    // 윗면이 topY - depth 에 오도록 내려 놓는다
    static void Under(Transform g, string rock, Vector3 top, float scale, float depth)
    {
        GameObject go = PlaceAt(g, Kay + rock + "_Color1", top, Range(0f, 360f), scale, false);
        float dy = top.y - depth - WorldBounds(go).max.y;
        go.transform.position += Vector3.up * dy;
    }

    static GameObject Place(Transform g, string path, float x, float z, float yaw, float scale, bool collider, float radius)
    {
        occupied.Add(new Vector3(x, radius, z));
        return PlaceAt(g, path, new Vector3(x, 0f, z), yaw, scale, collider);
    }

    static GameObject PlaceAt(Transform g, string path, Vector3 pos, float yaw, float scale, bool collider)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path + ".fbx");
        if (prefab == null) throw new FileNotFoundException("모델 없음: " + path + ".fbx");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, g);
        // FBX 루트의 축 변환 회전·스케일(Blender Z-up 등)은 유지하고 그 위에 yaw·배율만 얹는다
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f) * prefab.transform.rotation);
        go.transform.localScale = prefab.transform.localScale * scale;
        foreach (var t in go.GetComponentsInChildren<Transform>())
        {
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            var mf = t.GetComponent<MeshFilter>();
            if (collider && mf != null) t.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        }
        return go;
    }

    static bool Free(Vector2 p, float r)
    {
        if (Mathf.Abs(p.x) > half - r || Mathf.Abs(p.y) > half - r) return false;
        if (p.x > farmZone.xMin - r && p.x < farmZone.xMax + r && p.y > farmZone.yMin - r && p.y < farmZone.yMax + r) return false;
        if (Vector2.Distance(p, spawn) < 3f + r) return false;
        foreach (var o in occupied)
            if (Vector2.Distance(p, new Vector2(o.x, o.z)) < o.y + r) return false;
        return true;
    }

    static Material GroundMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(GroundMatPath);
        if (mat != null) return mat;
        Directory.CreateDirectory(Path.GetDirectoryName(GroundMatPath));
        mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", new Color(0.45f, 0.68f, 0.32f));  // KayKit 풀색에 맞춤
        mat.SetFloat("_Smoothness", 0f);
        AssetDatabase.CreateAsset(mat, GroundMatPath);
        return mat;
    }

    static GameObject Find(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go == null) throw new MissingReferenceException("MainGame에 " + name + " 오브젝트가 없습니다");
        return go;
    }

    static Bounds WorldBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static Transform Group(string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(root, false);
        return t;
    }

    static Vector2 XZ(Vector3 v) => new Vector2(v.x, v.z);
    static float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);
    static string Pick(string[] pool) => pool[rng.Next(pool.Length)];
}
