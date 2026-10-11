#nullable enable

using Houtamelo.Spire.Showcase;
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
        private const string ScenePath = "Assets/Scenes/Showcase.unity";
        private const string PresetPath = "Assets/Examples/ShowcasePreset.asset";
        private const string PipelineAssetPath = "Assets/ShowcaseURP.asset";
        private const string RendererDataPath = "Assets/URP_RendererData.asset";

        [Test]
        public void TrackedShowcaseScene_LoadsControllerWithAuthoredPreset()
        {
            var previousScenePath = SceneManager.GetActiveScene().path;
            try
            {
                var authoredPreset = AssetDatabase.LoadAssetAtPath<ShowcasePreset>(PresetPath);
                Assert.That(authoredPreset, Is.Not.Null, "The tracked showcase preset is missing.");

                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Assert.That(scene.IsValid, Is.True, "The tracked showcase scene could not be opened.");
                Assert.That(scene.isLoaded, Is.True);

                var controller = UnityEngine.Object.FindAnyObjectByType<ShowcaseController>();
                Assert.That(controller, Is.Not.Null, "The tracked showcase scene has no ShowcaseController.");
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

        private static void AssertUrpProjectConfiguration()
        {
            var pipelineAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
            Assert.That(pipelineAsset, Is.Not.Null, "The tracked URP pipeline asset is missing.");

            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererDataPath);
            Assert.That(rendererData, Is.Not.Null, "The tracked URP renderer data asset is missing.");
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
