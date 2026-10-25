#nullable enable

using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spire.UnityVariantPayloadCore
{

    public static class CorePlayerBuild
    {
        private const string ScenePath = "Assets/VariantPayloadCorePlayer.unity";
        private const string MonoExecutable = "Builds/VariantPayloadCore/Mono/VariantPayloadCore.x86_64";
        private const string Il2CppExecutable = "Builds/VariantPayloadCore/IL2CPP/VariantPayloadCore.x86_64";
        private const string ReportPath = "unity-core-build-report.json";

        public static void BuildPlayers()
        {
            var report = new BuildReport
            {
                unityVersion = Application.unityVersion,
                status = "fail",
                mono = new BackendBuildReport { backend = "Mono" },
                il2cpp = new BackendBuildReport { backend = "IL2CPP" },
            };

            try
            {
                SerializedUnionEditorTests.PreparePlayerAssets();
                EnsureScene();
                report.mono = Build(ScriptingImplementation.Mono2x, MonoExecutable);
                report.il2cpp = Build(ScriptingImplementation.IL2CPP, Il2CppExecutable);
                report.status = report.mono.status == "pass" && report.il2cpp.status == "pass"
                    ? "pass"
                    : "fail";
            }
            catch (Exception exception)
            {
                report.error = exception.ToString();
            }

            try
            {
                File.WriteAllText(ProjectPath(ReportPath), JsonUtility.ToJson(report, true));
            }
            catch (Exception exception)
            {
                report.status = "fail";
                report.error = string.IsNullOrEmpty(report.error)
                    ? "Could not write build report: " + exception
                    : report.error + Environment.NewLine + "Could not write build report: " + exception;
            }

            EditorApplication.Exit(report.status == "pass" ? 0 : 1);
        }

        private static BackendBuildReport Build(ScriptingImplementation requestedBackend, string relativeExecutable)
        {
            var result = new BackendBuildReport
            {
                backend = requestedBackend.ToString(),
                executablePath = ProjectPath(relativeExecutable),
                status = "fail",
            };

            try
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, requestedBackend);
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.High);
                Require(PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone) == requestedBackend,
                    "Scripting backend did not match requested backend.");
                Require(PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Standalone) == ManagedStrippingLevel.High,
                    "Managed stripping level was not High.");

                Directory.CreateDirectory(Path.GetDirectoryName(result.executablePath)!);
                var build = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = result.executablePath,
                    target = BuildTarget.StandaloneLinux64,
                    options = BuildOptions.CleanBuildCache | BuildOptions.StrictMode,
                });
                result.status = build.summary.result == BuildResult.Succeeded ? "pass" : "fail";
                result.errors = build.summary.totalErrors;
                result.warnings = build.summary.totalWarnings;
                if (result.status != "pass")
                    result.error = "Unity build result: " + build.summary.result;
            }
            catch (Exception exception)
            {
                result.error = exception.ToString();
            }

            return result;
        }

        private static void EnsureScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Require(EditorSceneManager.SaveScene(scene, ScenePath), "Could not save player scene.");
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
            public string unityVersion = string.Empty;
            public string status = string.Empty;
            public BackendBuildReport mono = new();
            public BackendBuildReport il2cpp = new();
            public string error = string.Empty;
        }

        [Serializable]
        private sealed class BackendBuildReport
        {
            public string backend = string.Empty;
            public string status = string.Empty;
            public string executablePath = string.Empty;
            public int errors;
            public int warnings;
            public string error = string.Empty;
        }
    }

}
