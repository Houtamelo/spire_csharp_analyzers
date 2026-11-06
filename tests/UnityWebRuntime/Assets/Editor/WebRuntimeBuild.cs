#nullable enable

using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Spire.UnityVariantPayloadCore;

namespace Spire.UnityWebRuntime
{

    public static class WebRuntimeBuild
    {
        private const string ScenePath = "Assets/WebRuntimePlayer.unity";
        private const string OutputPath = "Builds/WebRuntime";
        private const string ReportPath = "unity-web-build-report.json";

        public static void Build()
        {
            var report = new WebBuildReport
            {
                unityVersion = Application.unityVersion,
                target = BuildTarget.WebGL.ToString(),
                backend = "IL2CPP",
                managedStrippingLevel = ManagedStrippingLevel.High.ToString(),
                exceptionSupport = WebGLExceptionSupport.FullWithStacktrace.ToString(),
                compressionFormat = WebGLCompressionFormat.Disabled.ToString(),
                dataCaching = false,
                graphicsApi = GraphicsDeviceType.OpenGLES3.ToString(),
                developmentBuild = false,
                outputPath = ProjectPath(OutputPath),
                status = "fail",
            };

            try
            {
                Require(EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL,
                    "The active build target must already be WebGL before the build method runs.");
                SerializedUnionEditorTests.PreparePlayerAssets();
                EnsureScene();
                ConfigureWebPlayer();
                Directory.CreateDirectory(report.outputPath);
                var build = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = report.outputPath,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.CleanBuildCache | BuildOptions.StrictMode,
                });
                report.errors = build.summary.totalErrors;
                report.warnings = build.summary.totalWarnings;
                report.status = build.summary.result == BuildResult.Succeeded ? "pass" : "fail";
                if (report.status != "pass")
                    report.error = "Unity build result: " + build.summary.result;
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

        private static void ConfigureWebPlayer()
        {
            var web = NamedBuildTarget.WebGL;
            PlayerSettings.SetScriptingBackend(web, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(web, ManagedStrippingLevel.High);
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.FullWithStacktrace;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.dataCaching = false;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.OpenGLES3 });
            Require(PlayerSettings.GetScriptingBackend(web) == ScriptingImplementation.IL2CPP,
                "WebGL scripting backend did not match IL2CPP.");
            Require(PlayerSettings.GetManagedStrippingLevel(web) == ManagedStrippingLevel.High,
                "WebGL managed stripping did not match High.");
            Require(PlayerSettings.WebGL.exceptionSupport == WebGLExceptionSupport.FullWithStacktrace,
                "WebGL exception support did not match FullWithStacktrace.");
            Require(PlayerSettings.WebGL.compressionFormat == WebGLCompressionFormat.Disabled,
                "WebGL compression did not match Disabled.");
            Require(!PlayerSettings.WebGL.dataCaching, "WebGL data caching must be disabled for validation.");
        }

        private static void EnsureScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Require(EditorSceneManager.SaveScene(scene, ScenePath), "Could not save WebGL player scene.");
        }

        private static string ProjectPath(string path)
            => Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, path));

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        [Serializable]
        private sealed class WebBuildReport
        {
            public string unityVersion = string.Empty;
            public string target = string.Empty;
            public string backend = string.Empty;
            public string managedStrippingLevel = string.Empty;
            public string exceptionSupport = string.Empty;
            public string compressionFormat = string.Empty;
            public bool dataCaching;
            public string graphicsApi = string.Empty;
            public bool developmentBuild;
            public string outputPath = string.Empty;
            public string status = string.Empty;
            public int errors;
            public int warnings;
            public string error = string.Empty;
        }
    }

}
