// ReSharper disable AssignNullToNotNullAttribute
// ReSharper disable PossibleNullReferenceException

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Houtamelo.Spire.Showcase.Editor {

public static class ShowcaseAuthoring {
    [MenuItem("Tools/Spire/Create or Reset Showcase Examples")]
    public static void CreateExamples() {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var sampleRoot = ShowcasePaths.Root;
        if (!AssetDatabase.IsValidFolder(sampleRoot + "/Examples")) AssetDatabase.CreateFolder(sampleRoot, "Examples");
        if (!AssetDatabase.IsValidFolder(sampleRoot + "/Scenes")) AssetDatabase.CreateFolder(sampleRoot, "Scenes");
        var presetPath = ShowcasePaths.Preset;
        var preset = AssetDatabase.LoadAssetAtPath<ShowcasePreset>(presetPath);
        if (preset == null) {
            preset = ScriptableObject.CreateInstance<ShowcasePreset>();
            AssetDatabase.CreateAsset(preset, presetPath);
        }

        preset.ResetExamples();
        EditorUtility.SetDirty(preset);
        AssetDatabase.SaveAssets();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.08f, 0.1f, 0.14f);
        camera.transform.position = new Vector3(0, 0, -10);
        camera.orthographic = true;
        camera.GetUniversalAdditionalCameraData();

        var controller = new GameObject("Spire Showcase").AddComponent<ShowcaseController>();
        controller.Preset = preset;
        EditorUtility.SetDirty(controller);
        var scenePath = ShowcasePaths.Scene;
        EditorSceneManager.SaveScene(scene, scenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
        Selection.activeObject = preset;
    }

    [MenuItem("Tools/Spire/Configure Showcase URP")]
    public static void ConfigureUrp() {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var rendererPath = ShowcasePaths.Renderer;
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
        if (renderer == null) {
            renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, rendererPath);
        }

        var pipelinePath = ShowcasePaths.Pipeline;
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
        if (pipeline == null) {
            pipeline = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(pipeline, pipelinePath);
        }

        GraphicsSettings.defaultRenderPipeline = pipeline;
        var originalQuality = QualitySettings.GetQualityLevel();
        try {
            for (var quality = 0; quality < QualitySettings.names.Length; quality++) {
                QualitySettings.SetQualityLevel(quality, false);
                QualitySettings.renderPipeline = pipeline;
            }
        } finally {
            QualitySettings.SetQualityLevel(originalQuality, false);
        }

        PlayerSettings.colorSpace = ColorSpace.Linear;

        var scenePath = ShowcasePaths.Scene;
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        foreach (var root in scene.GetRootGameObjects())
        foreach (var camera in root.GetComponentsInChildren<Camera>(true))
            camera.GetUniversalAdditionalCameraData();

        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }
}

}
