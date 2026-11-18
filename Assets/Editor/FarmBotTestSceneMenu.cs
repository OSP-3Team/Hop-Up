using System.IO;
using HopUp.FarmEconomy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Farm Bot Test Scene: 농사 봇 동작 확인용 씬을 만든다(EconomyHub 1, 밭 4칸, 봇 2대, 카메라).
/// 플레이 후 봇 인스펙터의 ⋮ 메뉴(Debug: ...)로 보유·씨앗 투입·수확을 시험한다.
/// </summary>
public static class FarmBotTestSceneMenu
{
    const string Folder = "Assets/Scenes/BotTest";
    const string ScenePath = Folder + "/BotTest.unity";
    const int PlotCount = 4;   // 새 게임 기본 열린 칸 수(farm.startPlotCount)
    const int BotCount = 2;
    const float PlotSpacing = 2f;

    [MenuItem("Tools/Farm Bot Test Scene")]
    public static void Create()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Farm Bot Test Scene", ScenePath + "이(가) 이미 있습니다. 새로 만들까요?", "새로 만들기", "취소"))
            return;

        Directory.CreateDirectory(Folder);
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        Material ground = SaveMaterial("Ground", new Color(0.55f, 0.75f, 0.45f));
        Material soil = SaveMaterial("Soil", new Color(0.45f, 0.3f, 0.2f));
        Material empty = SaveMaterial("PlotEmpty", new Color(0.6f, 0.45f, 0.3f));
        Material growing = SaveMaterial("PlotGrowing", new Color(0.3f, 0.8f, 0.3f));
        Material ready = SaveMaterial("PlotReady", new Color(1f, 0.55f, 0.1f));
        Material bot = SaveMaterial("Bot", new Color(0.3f, 0.5f, 1f));

        new GameObject("EconomyHub").AddComponent<EconomyHub>();

        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = "Ground";
        plane.transform.position = new Vector3((PlotCount - 1) * PlotSpacing * 0.5f, 0f, 1f);
        plane.GetComponent<Renderer>().sharedMaterial = ground;

        for (int i = 0; i < PlotCount; i++)
            CreatePlot(i, new Vector3(i * PlotSpacing, 0.1f, 0f), soil, empty, growing, ready);

        Camera cam = Camera.main;
        Vector3 center = new Vector3((PlotCount - 1) * PlotSpacing * 0.5f, 0f, 1.5f);
        cam.transform.position = center + new Vector3(0f, 7f, -7f);
        cam.transform.LookAt(center);

        for (int i = 0; i < BotCount; i++)
        {
            float x = i == 0 ? -2f : (PlotCount - 1) * PlotSpacing + 2f;  // 밭 양쪽 끝에서 출발
            CreateBot(i, new Vector3(x, 1f, 4f), bot, cam.transform.rotation);
        }

        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
        Debug.Log("[FarmBotTestScene] " + ScenePath + " 생성. 플레이 후 봇 인스펙터 ⋮ 메뉴: 이 봇 보유하기 → 씨앗 넣기 → 밭 전부 다 자라게");
    }

    static void CreatePlot(int index, Vector3 pos, Material soil, Material empty, Material growing, Material ready)
    {
        GameObject plot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plot.name = "Plot" + index;
        plot.transform.position = pos;
        plot.transform.localScale = new Vector3(1.4f, 0.2f, 1.4f);
        plot.GetComponent<Renderer>().sharedMaterial = soil;

        FarmPlot farmPlot = plot.AddComponent<FarmPlot>();
        var so = new SerializedObject(farmPlot);
        so.FindProperty("plotIndex").intValue = index;
        so.FindProperty("emptyVisual").objectReferenceValue = CreateVisual(plot.transform, "Empty", empty, 0.1f);
        so.FindProperty("growingVisual").objectReferenceValue = CreateVisual(plot.transform, "Growing", growing, 0.4f);
        so.FindProperty("readyVisual").objectReferenceValue = CreateVisual(plot.transform, "Ready", ready, 0.8f);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 부모 스케일(1.4, 0.2, 1.4)을 상쇄해 월드 기준 0.6 x height x 0.6 상자를 밭 위에 올린다
    static GameObject CreateVisual(Transform parent, string name, Material mat, float height)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localScale = new Vector3(0.6f / 1.4f, height / 0.2f, 0.6f / 1.4f);
        go.transform.localPosition = new Vector3(0f, 0.5f + height / 0.4f, 0f);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        go.SetActive(false);  // FarmPlot이 상태에 맞게 켠다
        return go;
    }

    static void CreateBot(int index, Vector3 pos, Material mat, Quaternion faceCamera)
    {
        GameObject bot = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        bot.name = "Bot" + index;
        bot.transform.position = pos;
        bot.GetComponent<Renderer>().sharedMaterial = mat;

        var labelGo = new GameObject("StatusLabel");
        labelGo.transform.SetParent(bot.transform, false);
        labelGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        labelGo.transform.rotation = faceCamera;
        TextMesh label = labelGo.AddComponent<TextMesh>();
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.font = font;
        labelGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        label.fontSize = 48;
        label.characterSize = 0.06f;
        label.anchor = TextAnchor.LowerCenter;
        label.alignment = TextAlignment.Center;
        label.color = Color.black;

        FarmBot farmBot = bot.AddComponent<FarmBot>();
        var so = new SerializedObject(farmBot);
        so.FindProperty("botIndex").intValue = index;
        so.FindProperty("statusLabel").objectReferenceValue = label;
        so.FindProperty("showStateInLabel").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static Material SaveMaterial(string name, Color color)
    {
        string path = Folder + "/" + name + ".mat";
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = color;  // URP Lit은 _BaseColor, Standard는 _Color가 [MainColor]
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
