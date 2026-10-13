#nullable enable

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spire.UnityAsmdefIntegration
{
    public static class OverlapConstructorPlayerBuild
    {
        private const string ScenePath = "Assets/GeneratedOverlapPlayer/OverlapConstructorPlayer.unity";
        private const string BuildRelativePath = "Builds/OverlapConstructor/OverlapConstructorPlayer.x86_64";
        private const string BuildReportPath = "Assets/GeneratedOverlapPlayer/overlap-constructor-player-build-report.json";

        public static void BuildIl2Cpp()
        {
            Build(ScriptingImplementation.IL2CPP);
        }

        private static void Build(ScriptingImplementation requestedBackend)
        {
            var result = new BuildReport
            {
                backend = requestedBackend.ToString(),
                high = false,
                status = "fail",
                errors = 0,
                executablePath = ProjectPath(BuildRelativePath),
                error = string.Empty,
            };

            try
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, requestedBackend);
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.High);

                var actualBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
                var actualStripping = PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Standalone);
                Require(
                    actualBackend == requestedBackend,
                    "Standalone scripting backend did not match the requested backend: " + actualBackend);
                Require(
                    actualStripping == ManagedStrippingLevel.High,
                    "Standalone managed stripping level was not High: " + actualStripping);
                result.backend = actualBackend.ToString();
                result.high = actualStripping == ManagedStrippingLevel.High;

                var linkFiles = Directory
                    .EnumerateFiles(Application.dataPath, "link.xml", SearchOption.AllDirectories)
                    .ToArray();
                Require(
                    linkFiles.Length == 0,
                    "The asmdef fixture must not provide link.xml: " + string.Join(", ", linkFiles));

                var unsafeAssemblies = Directory
                    .EnumerateFiles(Application.dataPath, "System.Runtime.CompilerServices.Unsafe.dll", SearchOption.AllDirectories)
                    .ToArray();
                Require(
                    unsafeAssemblies.Length == 0,
                    "The Overlap player fixture must not provide an Unsafe DLL: "
                    + string.Join(", ", unsafeAssemblies));

                Directory.CreateDirectory(Path.GetDirectoryName(ProjectPath(ScenePath))!);
                Directory.CreateDirectory(Path.GetDirectoryName(result.executablePath)!);
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var gameObject = new GameObject("OverlapConstructorPlayerProbe");
                var probeType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType(
                        "Spire.UnityAsmdefIntegration.RuntimeAuto.OverlapConstructorPlayerProbe",
                        throwOnError: false))
                    .FirstOrDefault(type => type != null);
                Require(probeType != null, "The RuntimeAuto player probe type was not loaded.");
                var probe = gameObject.AddComponent(probeType!) as MonoBehaviour;
                Require(probe != null, "The RuntimeAuto player probe could not be added to its scene object.");
                var script = MonoScript.FromMonoBehaviour(probe!);
                Require(
                    script != null && AssetDatabase.Contains(script) &&
                    script.GetClass() == probeType,
                    "OverlapConstructorPlayerProbe must resolve to its tracked script asset.");
                SceneManager.MoveGameObjectToScene(gameObject, scene);
                Require(
                    EditorSceneManager.SaveScene(scene, ScenePath),
                    "Could not save the Overlap constructor player scene.");

                var build = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = result.executablePath,
                    target = BuildTarget.StandaloneLinux64,
                    options = BuildOptions.Development | BuildOptions.CleanBuildCache,
                });
                result.status = build.summary.result == BuildResult.Succeeded ? "pass" : "fail";
                result.errors = build.summary.totalErrors;
                if (result.status != "pass" && string.IsNullOrEmpty(result.error))
                    result.error = "Unity reported build result: " + build.summary.result;
            }
            catch (Exception exception)
            {
                result.status = "fail";
                result.error = exception.ToString();
            }

            try
            {
                var reportPath = ProjectPath(BuildReportPath);
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
                File.WriteAllText(reportPath, JsonUtility.ToJson(result, true));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                result.status = "fail";
                result.error = string.IsNullOrEmpty(result.error)
                    ? "Could not write the build report: " + exception
                    : result.error + Environment.NewLine + "Could not write the build report: " + exception;
            }

            EditorApplication.Exit(result.status == "pass" ? 0 : 1);
        }

        private static string ProjectPath(string path)
            => Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, path));

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        [Serializable]
        private sealed class BuildReport
        {
            public string backend = string.Empty;
            public bool high;
            public string status = string.Empty;
            public int errors;
            public string executablePath = string.Empty;
            public string error = string.Empty;
        }
    }
}
