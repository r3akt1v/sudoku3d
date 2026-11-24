using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using System.IO;

public class SudokuSceneBuilder
{
    const string MatDir = "Assets/Materials";
    const string PrefabDir = "Assets/Prefabs";
    const string SceneDir = "Assets/Scenes";

    [MenuItem("Tools/Build Sudoku Scene")]
    public static void Build()
    {
        Debug.Log("=== Building Sudoku scene ===");
        EnsureFolders();
        FixModelImporters();
        AssetDatabase.Refresh();

        var mats = CreateMaterials();
        var tilePrefabs = CreateTilePrefabs(mats);
        CreateScene(mats, tilePrefabs);
        Debug.Log("=== Scene build complete ===");
    }

    static void EnsureFolders()
    {
        foreach (var d in new[] { MatDir, PrefabDir, SceneDir, "Assets/Models" })
            if (!Directory.Exists(d)) Directory.CreateDirectory(d);
    }

    static void FixModelImporters()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Models" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi != null && (mi.globalScale != 1f || mi.useFileScale == false))
            {
                mi.globalScale = 1f;
                mi.useFileScale = true;
                mi.SaveAndReimport();
            }
        }
    }

    static Material MakeMat(string name, Color color, float smooth = 0.35f)
    {
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = color;
        m.SetFloat("_Glossiness", smooth);
        m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material MakeTransparentMat(string name, Color color)
    {
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = color;
        m.SetFloat("_Mode", 3f); // transparent
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_ALPHABLEND_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = 3000;
        EditorUtility.SetDirty(m);
        return m;
    }

    class Mats
    {
        public Material wood, board, tray, tile, digit, hlValid, hlInvalid;
    }

    static Mats CreateMaterials()
    {
        var m = new Mats();
        m.wood = MakeMat("WoodMat", new Color(0.50f, 0.40f, 0.30f), 0.25f);
        m.board = MakeMat("BoardMat", new Color(0.44f, 0.50f, 0.45f), 0.35f);
        m.tray = MakeMat("TrayMat", new Color(0.32f, 0.39f, 0.35f), 0.45f);
        m.tile = MakeMat("TileMat", new Color(0.82f, 0.78f, 0.70f), 0.45f);
        m.digit = MakeMat("DigitMat", new Color(0.30f, 0.26f, 0.21f), 0.2f);
        m.hlValid = MakeTransparentMat("HLValid", new Color(0.45f, 0.85f, 0.75f, 0.45f));
        m.hlInvalid = MakeTransparentMat("HLInvalid", new Color(0.9f, 0.5f, 0.42f, 0.45f));
        AssetDatabase.SaveAssets();
        return m;
    }

    static TileDrag[] CreateTilePrefabs(Mats mats)
    {
        var prefabs = new TileDrag[9];

        for (int n = 1; n <= 9; n++)
        {
            string path = $"{PrefabDir}/Tile_{n}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                AssetDatabase.DeleteAsset(path);

            var root = new GameObject($"Tile_{n}");

            var col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(0.8f, 0.3f, 0.8f);
            col.center = new Vector3(0, 0.15f, 0);

            var drag = root.AddComponent<TileDrag>();
            drag.number = n;

            // model child with baked digit (FBX has 2 materials: TileMat + DigitMat)
            var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Models/Tile_{n}.fbx");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
            Debug.Log($"TILE {n} fbx root rot={model.transform.localEulerAngles} scale={model.transform.localScale}");
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = Vector3.zero;
            var mb = LocalBounds(model);
            model.transform.localPosition = new Vector3(0, -mb.min.y, 0);
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                var shared = r.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                {
                    string mname = shared[i] != null ? shared[i].name : "";
                    shared[i] = mname.IndexOf("digit", System.StringComparison.OrdinalIgnoreCase) >= 0
                        ? mats.digit : mats.tile;
                }
                r.sharedMaterials = shared;
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            prefabs[n - 1] = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<TileDrag>();
        }
        AssetDatabase.SaveAssets();
        return prefabs;
    }

    static void AssignMaterialDeep(GameObject root, Material mat)
    {
        foreach (var r in root.GetComponentsInChildren<Renderer>())
            r.sharedMaterial = mat;
    }

    static Bounds LocalBounds(GameObject go)
    {
        var rends = go.GetComponentsInChildren<Renderer>();
        Bounds b = rends[0].bounds;
        foreach (var r in rends) b.Encapsulate(r.bounds);
        return b;
    }

    static void LogBounds(string label, GameObject go)
    {
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) { Debug.Log($"BOUNDS {label}: no renderers"); return; }
        Bounds b = rends[0].bounds;
        foreach (var r in rends) b.Encapsulate(r.bounds);
        Debug.Log($"BOUNDS {label}: min=({b.min.x:F2},{b.min.y:F2},{b.min.z:F2}) max=({b.max.x:F2},{b.max.y:F2},{b.max.z:F2})");
    }

    static void CreateScene(Mats mats, TileDrag[] tilePrefabs)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        const float tableTopY = 2.2f;

        // ---- Table ----
        var tablePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Table.fbx");
        var table = (GameObject)PrefabUtility.InstantiatePrefab(tablePrefab);
        table.name = "Table";
        table.transform.position = Vector3.zero;
        // recenter: bottom at y=0, centered on x/z
        var tb = LocalBounds(table);
        table.transform.position += new Vector3(-tb.center.x, -tb.min.y, -tb.center.z);
        AssignMaterialDeep(table, mats.wood);
        LogBounds("Table", table);

        // ---- Tray pad ----
        var tray = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tray.name = "TrayPad";
        tray.transform.position = new Vector3(0, tableTopY + 0.035f, -6.0f);
        tray.transform.localScale = new Vector3(14.6f, 0.07f, 3.4f);
        tray.GetComponent<Renderer>().sharedMaterial = mats.tray;

        // ---- Floor (dark) ----
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.position = new Vector3(0, -0.05f, 0);
        floor.transform.localScale = new Vector3(60f, 0.1f, 60f);
        floor.GetComponent<Renderer>().sharedMaterial = MakeMat("FloorMat", new Color(0.14f, 0.13f, 0.12f), 0.1f);

        // ---- Lights ----
        var sun = new GameObject("Sun");
        var sunLight = sun.AddComponent<Light>();
        sunLight.type = LightType.Directional;
        sunLight.color = new Color(1f, 0.95f, 0.87f);
        sunLight.intensity = 0.75f;
        sunLight.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(52f, -32f, 0f);

        var fill = new GameObject("Fill");
        var fillLight = fill.AddComponent<Light>();
        fillLight.type = LightType.Directional;
        fillLight.color = new Color(0.75f, 0.83f, 0.95f);
        fillLight.intensity = 0.22f;
        fill.transform.rotation = Quaternion.Euler(35f, 150f, 0f);

        // ambient — dim, warm-ish night feel
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.30f, 0.33f, 0.40f);
        RenderSettings.ambientEquatorColor = new Color(0.26f, 0.24f, 0.21f);
        RenderSettings.ambientGroundColor = new Color(0.12f, 0.11f, 0.10f);
        RenderSettings.ambientIntensity = 0.55f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.05f, 0.05f, 0.07f);
        RenderSettings.fogDensity = 0.012f;

        // ---- Camera ----
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 55f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 120f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.05f, 0.07f);
        camGo.transform.position = new Vector3(0f, 11.8f, -14.2f);
        camGo.transform.rotation = Quaternion.LookRotation(
            new Vector3(0f, 1.2f, 0.9f) - camGo.transform.position);
        camGo.AddComponent<AudioListener>();

        // ---- GameManager ----
        var gmGo = new GameObject("GameManager");
        var gm = gmGo.AddComponent<GameManager>();
        gm.tilePrefabs = tilePrefabs;
        gm.board3 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/SudokuBoard_3.fbx");
        gm.board6 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/SudokuBoard_6.fbx");
        gm.board9 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/SudokuBoard_9.fbx");
        gm.hlValid = mats.hlValid;
        gm.hlInvalid = mats.hlInvalid;
        gm.boardMaterial = mats.board;
        gmGo.AddComponent<UIManager>();
        gmGo.AddComponent<AudioManager>();

        // ---- Save scene ----
        string scenePath = $"{SceneDir}/Main.unity";
        EditorSceneManager.SaveScene(scene, scenePath);

        // build settings
        var scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
        EditorBuildSettings.scenes = scenes;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
