#nullable enable

using System.Collections;
using Houtamelo.Spire.Showcase;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Houtamelo.Spire.Showcase.Tests
{
    public sealed class ShowcasePlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/Showcase.unity";
        private string? previousScenePath;

        [UnityTest]
        public IEnumerator ShowcaseScene_ExecutesAuthoredQueueInPlayMode()
        {
            previousScenePath = SceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Assert.That(scene.IsValid, Is.True, "The tracked showcase scene could not be opened.");

            yield return new EnterPlayMode();
            yield return null;

            var controller = UnityEngine.Object.FindAnyObjectByType<ShowcaseController>();
            Assert.That(controller, Is.Not.Null, "The tracked showcase scene has no ShowcaseController.");
            Assert.That(controller!.Preset, Is.Not.Null, "The ShowcaseController has no bound preset.");

            var preset = controller.Preset!;
            for (var index = 0; index < preset.commands.Count; index++)
                controller.Execute(preset.commands[index]);

            // Allow several real player frames for OnGUI to repaint the authored state.
            yield return null;
            yield return null;
            yield return null;

            Assert.That(RenderPipelineManager.currentPipeline, Is.TypeOf<UniversalRenderPipeline>());
            Assert.That(controller.Health, Is.EqualTo(80));
            Assert.That(controller.Position, Is.EqualTo(new Vector2(3f, 4f)));
            Assert.That(controller.LastSpeed, Is.EqualTo(2f));
            Assert.That(controller.LastMessage, Is.EqualTo("Hello from Spire"));
            Assert.That(controller.Mode, Is.EqualTo(ActorMode.Acting));

            controller.ResetSession();
            Assert.That(controller.Health, Is.EqualTo(100));
            Assert.That(controller.Position, Is.EqualTo(Vector2.zero));
            Assert.That(controller.LastSpeed, Is.EqualTo(0f));
            Assert.That(controller.LastMessage, Is.EqualTo(string.Empty));
            Assert.That(controller.Mode, Is.EqualTo(ActorMode.Idle));

            yield return new ExitPlayMode();
        }

        [UnityTearDown]
        public IEnumerator RestoreEditorModeAndScene()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            if (!string.IsNullOrEmpty(previousScenePath))
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            else
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
