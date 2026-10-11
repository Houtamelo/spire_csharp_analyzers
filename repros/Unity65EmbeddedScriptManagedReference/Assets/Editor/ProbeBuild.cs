using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class ProbeBuild
{
    [MenuItem("Tools/Unity 6.5 Repro/Build IL2CPP")]
    public static void Build() => BuildPlayer(ScriptingImplementation.IL2CPP);

    [MenuItem("Tools/Unity 6.5 Repro/Build Mono")]
    public static void BuildMono() => BuildPlayer(ScriptingImplementation.Mono2x);

    private static void BuildPlayer(ScriptingImplementation backend)
    {
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, backend);
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.High);
        const string scenePath = "Assets/Probe.unity";
        Directory.CreateDirectory("Builds/Probe");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { scenePath },
            target = BuildTarget.StandaloneLinux64,
            locationPathName = "Builds/Probe/Probe.x86_64",
            options = BuildOptions.Development | BuildOptions.AllowDebugging | BuildOptions.CleanBuildCache,
        });
        File.WriteAllText("build-result.txt", report.summary.result + "\nerrors=" + report.summary.totalErrors + "\nwarnings=" + report.summary.totalWarnings);
        Debug.Log("MRP_BUILD=" + report.summary.result + ";backend=" + backend);
        if (Application.isBatchMode)
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
