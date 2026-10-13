#nullable enable

using System;
using Houtamelo.Spire.Showcase;
using Houtamelo.Spire.Showcase.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Houtamelo.Spire.Showcase.Tests
{
    public sealed class ShowcaseSceneTests
    {
        private const string RuntimeAssemblyGuid = "de1fa9429c4a323daa60cd365f1c7de3";
        private const string MissingAssemblyGuid = "00000000000000000000000000000000";

        [Test]
        public void ShowcaseScene_LoadsControllerWithAuthoredPreset()
        {
            var previousScenePath = SceneManager.GetActiveScene().path;
            try
            {
                var authoredPreset = AssetDatabase.LoadAssetAtPath<ShowcasePreset>(ShowcasePaths.Preset);
                Assert.That(authoredPreset, Is.Not.Null, "The showcase preset is missing.");

                var scene = EditorSceneManager.OpenScene(ShowcasePaths.Scene, OpenSceneMode.Single);
                Assert.That(scene.IsValid, Is.True, "The showcase scene could not be opened.");
                Assert.That(scene.isLoaded, Is.True);

                var controller = UnityEngine.Object.FindAnyObjectByType<ShowcaseController>();
                Assert.That(controller, Is.Not.Null, "The showcase scene has no ShowcaseController.");
                Assert.That(controller!.Preset, Is.SameAs(authoredPreset));

                ShowcaseTestSupport.AssertResetPreset(controller.Preset!);
                Assert.That(controller.Health, Is.EqualTo(100));
                Assert.That(controller.Position, Is.EqualTo(Vector2.zero));
                Assert.That(controller.LastSpeed, Is.EqualTo(0f));
                Assert.That(controller.LastMessage, Is.EqualTo(string.Empty));
                Assert.That(controller.Mode, Is.EqualTo(ActorMode.Idle));

                AssertUrpProjectConfiguration();
            }
            finally
            {
                if (!string.IsNullOrEmpty(previousScenePath))
                    EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
                else
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [Test]
        public void ShowcasePaths_ResolveRelocatedSampleAssetsFromRuntimeAssembly()
        {
            var resolvedRoot = ShowcasePaths.ResolveRoot(RuntimeAssemblyGuid);

            Assert.That(resolvedRoot, Is.EqualTo(ShowcasePaths.Root));
            Assert.That(AssetDatabase.IsValidFolder(resolvedRoot), Is.True);
            Assert.That(ShowcasePaths.Scene, Does.StartWith(resolvedRoot + "/"));
            Assert.That(ShowcasePaths.Preset, Does.StartWith(resolvedRoot + "/"));
            Assert.That(ShowcasePaths.Pipeline, Does.StartWith(resolvedRoot + "/"));
            Assert.That(ShowcasePaths.Renderer, Does.StartWith(resolvedRoot + "/"));

            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ShowcasePaths.Scene), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<ShowcasePreset>(ShowcasePaths.Preset), Is.Not.Null);
            Assert.That(
                AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(ShowcasePaths.Pipeline),
                Is.Not.Null);
            Assert.That(
                AssetDatabase.LoadAssetAtPath<UniversalRendererData>(ShowcasePaths.Renderer),
                Is.Not.Null);
        }

        [Test]
        public void ShowcasePaths_ResolveRoot_ThrowsWhenRuntimeAssemblyGuidIsMissing()
        {
            Assert.Throws<InvalidOperationException>(() => ShowcasePaths.ResolveRoot(MissingAssemblyGuid));
        }

        private static void AssertUrpProjectConfiguration()
        {
            var pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(ShowcasePaths.Pipeline);
            Assert.That(pipelineAsset, Is.Not.Null, "The URP pipeline asset is missing.");

            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(ShowcasePaths.Renderer);
            Assert.That(rendererData, Is.Not.Null, "The URP renderer data asset is missing.");
            Assert.That(GraphicsSettings.defaultRenderPipeline, Is.SameAs(pipelineAsset));

            var pipelineSerialized = new SerializedObject(pipelineAsset);
            var rendererDataList = pipelineSerialized.FindProperty("m_RendererDataList");
            Assert.That(rendererDataList, Is.Not.Null, "The URP asset has no renderer data list.");
            Assert.That(rendererDataList!.arraySize, Is.GreaterThan(0));
            Assert.That(rendererDataList.GetArrayElementAtIndex(0).objectReferenceValue, Is.SameAs(rendererData));

            var qualityNames = QualitySettings.names;
            Assert.That(qualityNames, Is.Not.Empty, "The project has no quality levels.");
            for (var index = 0; index < qualityNames.Length; index++)
                Assert.That(QualitySettings.GetRenderPipelineAssetAt(index), Is.SameAs(pipelineAsset), qualityNames[index]);

            Assert.That(PlayerSettings.colorSpace, Is.EqualTo(ColorSpace.Linear));

            var cameraObject = GameObject.Find("Main Camera");
            Assert.That(cameraObject, Is.Not.Null, "The tracked scene has no Main Camera.");
            Assert.That(cameraObject!.GetComponent<Camera>(), Is.Not.Null);
            Assert.That(cameraObject.GetComponent<UniversalAdditionalCameraData>(), Is.Not.Null);
        }
    }
}
