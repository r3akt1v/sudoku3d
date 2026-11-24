using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildMacApp
{
    [MenuItem("Tools/Build macOS App")]
    public static void Build()
    {
        var opts = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Main.unity" },
            locationPathName = "Builds/Sudoku3D.app",
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer(opts);
        Debug.Log($"BUILD RESULT: {report.summary.result} errors={report.summary.totalErrors}");
        if (report.summary.result != BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }
}
