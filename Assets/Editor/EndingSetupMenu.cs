using System.Linq;
using Project.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Tools > Hop-Up > Setup Ending: 엔딩 씬(Assets/Scenes/Ending.unity)을 새로 만들고 Build Profiles의 Scene List에 넣는다.
/// 몇 번 돌려도 같은 결과(씬을 통째로 다시 만듦). 만든 뒤 Unity에서 배치·색을 손봐도 되지만, 다시 돌리면 그 손본 것은 사라진다.
/// 구도: 왼쪽 위에 지구, 둥근 달 지평선, 왼쪽에 우리 토끼, 오른쪽에 달 토끼 둘. 연출은 EndingCutscene이 한다.
/// </summary>
public static class EndingSetupMenu
{
    const string ScenePath = "Assets/Scenes/Ending.unity";
    const string BunnyPrefabPath = "Assets/Stylized cartoon bunny/Prefab/Stylized bunny.prefab";
    const string BunnyMaterialPath = "Assets/Stylized cartoon bunny/Material/bunny.mat";
    const string MoonMaterialPath = "Assets/Materials/Moon.mat";
    const string EarthTexturePath = "Assets/Textures/EarthSurface.png";
    const string EarthMaterialPath = "Assets/Materials/Earth.mat";
    const string StarsTexturePath = "Assets/Textures/Stars.png";
    const string StarsMaterialPath = "Assets/Materials/EndingStars.mat";

    const float MoonGroundDiameter = 400f;                   // 꼭대기가 y=0. 반지름이 커서 지평선이 완만하게 휨
    static readonly Vector3 EarthPosition = new Vector3(-60f, 55f, 140f);
    const float EarthDiameter = 60f;
    static readonly Vector3 PlayerBunnyPosition = new Vector3(-1.5f, 0f, 0f);
    static readonly Vector3[] MoonBunnyPositions = { new Vector3(8f, 0f, 1f), new Vector3(10f, 0f, -1f) };
    static readonly Color[] MoonBunnyColors = { new Color(0.72f, 0.72f, 0.76f), new Color(1f, 0.93f, 0.78f) }; // 회색, 크림색
    static readonly string[] MoonBunnyNames = { "Grey", "Cream" };

    [MenuItem("Tools/Hop-Up/Setup Ending")]
    public static void BuildFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var bunnyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BunnyPrefabPath);
        var moonMat = AssetDatabase.LoadAssetAtPath<Material>(MoonMaterialPath);
        if (bunnyPrefab == null || moonMat == null)
        {
            Debug.LogError("[SetupEnding] 토끼 프리팹이나 Moon.mat을 못 찾았습니다: " + BunnyPrefabPath + ", " + MoonMaterialPath);
            return;
        }

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 하늘: 별 파노라마 스카이박스. 주변광은 단색으로 둬서 그늘진 쪽이 새까맣지 않게
        RenderSettings.skybox = LoadOrCreateStarsMaterial();
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.25f, 0.25f, 0.3f);

        var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camGo.tag = "MainCamera";
        var cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.farClipPlane = 2000f;

        // 해: 카메라 쪽(뒤)에서 비춰 지구와 토끼의 보이는 면이 밝게
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.2f;
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(20f, -30f, 0f);

        GameObject ground = Sphere("MoonGround", new Vector3(0f, -MoonGroundDiameter / 2f, 0f), MoonGroundDiameter, moonMat, meshCollider: true);

        GameObject earth = Sphere("Earth", EarthPosition, EarthDiameter, LoadOrCreateEarthMaterial());
        earth.transform.rotation = Quaternion.Euler(23.5f, 0f, 0f); // 자전축 기울기. EndingCutscene이 이 축으로 돌림

        GameObject player = Bunny(bunnyPrefab, "PlayerBunny", PlayerBunnyPosition, null);
        Face(player.transform, EarthPosition);
        var moonBunnies = new Transform[2];
        for (int i = 0; i < 2; i++)
        {
            GameObject b = Bunny(bunnyPrefab, "MoonBunny_" + (i + 1), MoonBunnyPositions[i], LoadOrCreateTint(MoonBunnyNames[i], MoonBunnyColors[i]));
            Face(b.transform, PlayerBunnyPosition);
            moonBunnies[i] = b.transform;
        }

        // 카메라 자리 (씬 화면에서 옮겨 구도를 고침)
        Vector3 head = PlayerBunnyPosition + Vector3.up * 0.6f;
        Vector3 trio = PlayerBunnyPosition + Vector3.right * 1.2f + Vector3.up * 0.5f; // 셋이 모였을 때 가운데쯤
        Transform camEarth = CamSpot("Cam_Earth", EarthPosition + new Vector3(0f, -5f, -90f), EarthPosition);
        Transform camArrive = CamSpot("Cam_Arrive", PlayerBunnyPosition + new Vector3(0.8f, 1.6f, -4.5f), Vector3.Lerp(head, EarthPosition, 0.15f));
        Transform camMeet = CamSpot("Cam_Meet", trio + new Vector3(0f, 0.7f, -6f), trio);
        Transform camWide = CamSpot("Cam_Wide", trio + new Vector3(0f, 3f, -18f), Vector3.Lerp(trio, EarthPosition, 0.25f));
        camGo.transform.SetPositionAndRotation(camEarth.position, camEarth.rotation);

        var cutscene = new GameObject("EndingCutscene").AddComponent<EndingCutscene>();
        var so = new SerializedObject(cutscene);
        so.FindProperty("earth").objectReferenceValue = earth.transform;
        so.FindProperty("playerBunny").objectReferenceValue = player.transform;
        SerializedProperty arr = so.FindProperty("moonBunnies");
        arr.arraySize = 2;
        for (int i = 0; i < 2; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = moonBunnies[i];
        so.FindProperty("camEarth").objectReferenceValue = camEarth;
        so.FindProperty("camArrive").objectReferenceValue = camArrive;
        so.FindProperty("camMeet").objectReferenceValue = camMeet;
        so.FindProperty("camWide").objectReferenceValue = camWide;
        so.FindProperty("ground").objectReferenceValue = ground.GetComponent<Collider>();
        so.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.SaveAssets(); // CreateAsset 뒤에 넣은 텍스처·색을 재질 파일에 씀
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
        AddToBuildScenes();
        Debug.Log("[SetupEnding] " + ScenePath + " 생성, Scene List 등록 완료");
    }

    static GameObject Sphere(string name, Vector3 position, float diameter, Material mat, bool meshCollider = false)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>()); // 보기만 하는 장면이라 물리는 필요 없음
        // 바닥은 실제 메시 모양의 충돌체가 필요: 기본 구는 꼭대기 주변이 평평한 부채꼴이라 진짜 구와 높이가 다름(EndingCutscene이 토끼 발을 여기에 맞춤)
        if (meshCollider) go.AddComponent<MeshCollider>();
        go.transform.position = position;
        go.transform.localScale = Vector3.one * diameter;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    static GameObject Bunny(GameObject prefab, string name, Vector3 position, Material tint)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = name;
        go.transform.position = position;
        LowerArms(go.transform);
        if (tint != null)
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
                r.sharedMaterials = r.sharedMaterials.Select(_ => tint).ToArray();
        return go;
    }

    /// <summary>
    /// 모델이 애니메이션 없이 T자(팔을 옆으로 든) 자세라, 윗팔 뼈를 돌려 팔을 몸 옆으로 내림. 씬에 저장되므로 씬 화면에서도 내린 자세로 보임.
    /// 뼈 축 방향에 상관없게, 윗팔→아래팔 방향을 "아래로, 바깥쪽으로 살짝" 방향으로 돌리는 회전을 월드 기준으로 곱함.
    /// </summary>
    static void LowerArms(Transform bunny)
    {
        foreach (string side in new[] { "Left", "Right" })
        {
            Transform arm = FindBone(bunny, side + "Arm");
            Transform fore = FindBone(bunny, side + "ForeArm");
            if (arm == null || fore == null)
            {
                Debug.LogWarning("[SetupEnding] " + bunny.name + "에서 " + side + "Arm/" + side + "ForeArm 뼈를 못 찾아 팔을 내리지 못했습니다.");
                continue;
            }
            Vector3 current = (fore.position - arm.position).normalized;
            Vector3 outward = Vector3.ProjectOnPlane(current, Vector3.up).normalized;
            Vector3 target = (Vector3.down + outward * ArmSpread).normalized; // 몸통에 파묻히지 않게 바깥으로 조금 벌림
            arm.rotation = Quaternion.FromToRotation(current, target) * arm.rotation;
        }
    }

    const float ArmSpread = 0.2f; // 내린 팔이 바깥으로 벌어지는 정도 (0 = 똑바로 아래, 0.2 ≈ 11°)

    static Transform FindBone(Transform root, string name) =>
        root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

    // 모델 정면이 +Z (Player 프리팹이 회전 없이 이 모델을 씀)
    static void Face(Transform t, Vector3 target)
    {
        Vector3 dir = Vector3.ProjectOnPlane(target - t.position, Vector3.up);
        if (dir.sqrMagnitude > 0.0001f) t.rotation = Quaternion.LookRotation(dir);
    }

    static Transform CamSpot(string name, Vector3 position, Vector3 lookAt)
    {
        var t = new GameObject(name).transform;
        t.position = position;
        t.LookAt(lookAt);
        return t;
    }

    static void AddToBuildScenes()
    {
        var scenes = EditorBuildSettings.scenes;
        if (scenes.Any(s => s.path == ScenePath)) return;
        EditorBuildSettings.scenes = scenes.Append(new EditorBuildSettingsScene(ScenePath, true)).ToArray();
    }

    static Material LoadOrCreateTint(string name, Color color)
    {
        string path = "Assets/Materials/MoonBunny_" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var baseMat = AssetDatabase.LoadAssetAtPath<Material>(BunnyMaterialPath);
            mat = baseMat != null ? new Material(baseMat) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = color; // 토끼 텍스처에 곱해짐
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material LoadOrCreateEarthMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(EarthMaterialPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, EarthMaterialPath);
        }
        mat.SetTexture("_BaseMap", LoadOrCreateTexture(EarthTexturePath, 512, 256, EarthPixel));
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", 0.3f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material LoadOrCreateStarsMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(StarsMaterialPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Skybox/Panoramic"));
            AssetDatabase.CreateAsset(mat, StarsMaterialPath);
        }
        mat.SetTexture("_MainTex", LoadOrCreateTexture(StarsTexturePath, 2048, 1024, null));
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>
    /// 위경도(가로 360°, 세로 180°) 텍스처를 한 번만 만들어 PNG로 저장. 이미 있으면 그대로 씀(사람이 바꿔 끼운 그림을 덮어쓰지 않게).
    /// pixel이 null이면 별 하늘(검정 바탕에 흰 점).
    /// </summary>
    static Texture2D LoadOrCreateTexture(string path, int w, int h, System.Func<Vector3, float, Color> pixel)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex != null) return tex;

        var t = new Texture2D(w, h, TextureFormat.RGB24, false);
        var px = new Color[w * h];
        if (pixel == null)
        {
            var rng = new System.Random(7); // 시드 고정: 다시 만들어도 같은 하늘
            for (int i = 0; i < px.Length; i++) px[i] = Color.black;
            for (int n = 0; n < 1500; n++)
            {
                int x = rng.Next(w), y = rng.Next(h);
                float b = 0.4f + 0.6f * (float)rng.NextDouble();
                px[y * w + x] = new Color(b, b, b);
                if (b > 0.9f && x + 1 < w) px[y * w + x + 1] = new Color(b, b, b) * 0.6f; // 밝은 별은 조금 크게
            }
        }
        else
        {
            for (int y = 0; y < h; y++)
            {
                float lat = (y + 0.5f) / h * Mathf.PI - Mathf.PI / 2f;
                for (int x = 0; x < w; x++)
                {
                    float lon = (x + 0.5f) / w * 2f * Mathf.PI;
                    var p = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
                    px[y * w + x] = pixel(p, lat * Mathf.Rad2Deg);
                }
            }
        }
        t.SetPixels(px);
        System.IO.File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // 지구 한 점의 색: 바다·대륙(높으면 갈색)·극지 + 반투명 구름
    static Color EarthPixel(Vector3 p, float latDeg)
    {
        float land = Noise3(p * 2.2f, 0f) * 0.7f + Noise3(p * 5f, 10f) * 0.3f;
        Color c = land < 0.52f ? Color.Lerp(new Color(0.03f, 0.15f, 0.45f), new Color(0.1f, 0.35f, 0.7f), land / 0.52f)
                : land < 0.66f ? new Color(0.2f, 0.55f, 0.22f)
                : new Color(0.5f, 0.42f, 0.28f);
        if (Mathf.Abs(latDeg) > 70f) c = new Color(0.92f, 0.95f, 1f);
        float cloud = Mathf.Clamp01((Noise3(p * 4f, 30f) - 0.55f) * 3f);
        return Color.Lerp(c, Color.white, cloud * 0.8f);
    }

    // shortcut: 3D 노이즈 대신 세 평면의 2D Perlin 평균, 구에 이음매 없이 입히기엔 충분. 더 자연스러운 대륙이 필요하면 진짜 3D 노이즈로
    static float Noise3(Vector3 p, float seed)
    {
        float s = seed + 100f; // 음수 좌표에서 Perlin이 대칭으로 보이지 않게 밀어 둠
        return (Mathf.PerlinNoise(p.x + s, p.y + s) + Mathf.PerlinNoise(p.y + s, p.z + s) + Mathf.PerlinNoise(p.z + s, p.x + s)) / 3f;
    }
}
