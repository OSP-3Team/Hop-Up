using System.Collections.Generic;
using Project.Core;
using Project.Inventory;
using Project.Shop;
using Project.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Tools > Hop-Up > Setup Opening: MainGame에 하늘의 달(Moon)과 오프닝 연출(OpeningSequence)을 배치하고 참조를 채운다.
/// 기존 Moon·OpeningSequence 루트를 지우고 다시 만든다(몇 번 돌려도 같은 결과).
/// 배치 실행: Unity -batchmode -projectPath . -executeMethod OpeningSetupMenu.BuildAndSave -quit
/// </summary>
public static class OpeningSetupMenu
{
    const string ScenePath = "Assets/MainGame.unity";
    const string MoonMaterialPath = "Assets/Materials/Moon.mat";
    const string MoonTexturePath = "Assets/Textures/MoonSurface.png"; // 구용 위경도 텍스처 (크레이터·바다를 코드로 생성)
    const float MoonHeight = 450f;    // 엔딩 높이(300m)보다 위: 지상에서 높이 떠 보이고, 꼭대기에 올라가도 여전히 올려다보게
    const float MoonDiameter = 80f;
    // 스폰 정면 수평 거리 = 반지름 + 2m: 오프닝에서 토끼가 달 가장자리에서 미끄러져 스폰 바로 위로 떨어짐
    const float MoonDistance = MoonDiameter / 2f + 2f;

    [MenuItem("Tools/Hop-Up/Setup Opening")]
    public static void BuildFromMenu()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }
        if (!Build(out GameObject moon, out GameObject opening)) return;
        Undo.RegisterCreatedObjectUndo(moon, "Setup Opening");
        Undo.RegisterCreatedObjectUndo(opening, "Setup Opening");
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    public static void BuildAndSave()
    {
        EditorSceneManager.OpenScene(ScenePath);
        if (Build(out _, out _)) EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    }

    static bool Build(out GameObject moon, out GameObject opening)
    {
        moon = opening = null;
        var playerCtrl = Object.FindFirstObjectByType<PlayerController>();
        if (playerCtrl == null)
        {
            Debug.LogError("[SetupOpening] MainGame에 PlayerController가 없습니다.");
            return false;
        }

        foreach (string name in new[] { "Moon", "OpeningSequence" })
        {
            GameObject old = GameObject.Find(name);
            if (old != null && old.transform.parent == null) Object.DestroyImmediate(old);
        }

        Transform player = playerCtrl.transform;
        Vector3 forward = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
        moon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        moon.name = "Moon";
        Object.DestroyImmediate(moon.GetComponent<Collider>()); // 장식용. 발판 설치 레이캐스트 등에 걸리지 않게
        moon.transform.position = player.position + forward * MoonDistance + Vector3.up * MoonHeight;
        moon.transform.localScale = Vector3.one * MoonDiameter;
        moon.GetComponent<MeshRenderer>().sharedMaterial = LoadOrCreateMoonMaterial();

        opening = new GameObject("OpeningSequence");
        var so = new SerializedObject(opening.AddComponent<OpeningSequence>());
        so.FindProperty("player").objectReferenceValue = player;
        so.FindProperty("moon").objectReferenceValue = moon.transform;

        var toDisable = new List<Behaviour> { playerCtrl };
        AddIfFound<PlatformPlacer>(toDisable);
        AddIfFound<Hotbar>(toDisable);
        AddIfFound<InventoryWindowUI>(toDisable);
        AddIfFound<ShopWindowUI>(toDisable);
        AddIfFound<FirstPersonArm>(toDisable);
        SerializedProperty arr = so.FindProperty("disableDuringOpening");
        arr.arraySize = toDisable.Count;
        for (int i = 0; i < toDisable.Count; i++)
            arr.GetArrayElementAtIndex(i).objectReferenceValue = toDisable[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log($"[SetupOpening] Moon {moon.transform.position}, 끌 컴포넌트 {toDisable.Count}개 연결");
        return true;
    }

    /// <summary>
    /// URP Unlit + 달 표면 텍스처: 해 방향과 상관없이 아래에서 올려다봐도 밝음 (실제 보름달도 거의 평평하게 밝아 보임).
    /// Lit + 발광은 URP가 _EMISSION 키워드를 재임포트 때 지워 어둡게 나와서 쓰지 않음.
    /// </summary>
    static Material LoadOrCreateMoonMaterial()
    {
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MoonMaterialPath);
        if (mat == null)
        {
            mat = new Material(unlit);
            AssetDatabase.CreateAsset(mat, MoonMaterialPath);
        }
        mat.shader = unlit;
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(MoonTexturePath);
        if (tex == null) Debug.LogWarning("[SetupOpening] " + MoonTexturePath + " 텍스처를 못 찾아 단색으로 둡니다.");
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    static void AddIfFound<T>(List<Behaviour> list) where T : Behaviour
    {
        T found = Object.FindFirstObjectByType<T>();
        if (found != null) list.Add(found);
        else Debug.LogWarning("[SetupOpening] " + typeof(T).Name + "을(를) 못 찾아 끌 목록에서 뺍니다.");
    }
}
