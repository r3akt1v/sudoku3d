using UnityEngine;
using UnityEditor;

public class MeasureBounds
{
    [MenuItem("Tools/Measure Model Bounds")]
    public static void Measure()
    {
        string[] paths = {
            "Assets/Models/SudokuBoard.fbx",
            "Assets/Models/Tile_1.fbx",
            "Assets/Models/CellHighlight.fbx"
        };
        foreach (var p in paths)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (go == null) { Debug.Log($"MISSING {p}"); continue; }
            var inst = Object.Instantiate(go);
            var rends = inst.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) { Debug.Log($"{p}: no renderers"); }
            Bounds b = new Bounds(inst.transform.position, Vector3.zero);
            bool first = true;
            foreach (var r in rends)
            {
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            Debug.Log($"MODEL {p} | size=({b.size.x:F3},{b.size.y:F3},{b.size.z:F3}) center=({b.center.x:F3},{b.center.y:F3},{b.center.z:F3}) scale={inst.transform.localScale}");
            Object.DestroyImmediate(inst);
        }
        Debug.Log("MEASURE_DONE");
    }
}
