// ReSharper disable AssignNullToNotNullAttribute
// ReSharper disable PossibleNullReferenceException

using System.IO;
using Houtamelo.Spire.Showcase.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Houtamelo.Spire.Showcase.Tests {

internal static class ShowcaseTestSupport {
    public static string TemporaryFolder => ShowcasePaths.Root + "/Tests/TemporaryTest";
    public static string PresetPath      => TemporaryFolder + "/ShowcasePreset.asset";
    public static string FirstHostPath   => TemporaryFolder + "/FirstHost.asset";
    public static string SecondHostPath  => TemporaryFolder + "/SecondHost.asset";

    public static void EnsureTemporaryFolder() {
        var testsFolder = ShowcasePaths.Root + "/Tests";
        if (!AssetDatabase.IsValidFolder(testsFolder))
            AssetDatabase.CreateFolder(ShowcasePaths.Root, "Tests");

        if (!AssetDatabase.IsValidFolder(TemporaryFolder))
            AssetDatabase.CreateFolder(testsFolder, "TemporaryTest");
    }

    public static ShowcasePreset CreatePreset(string path) {
        EnsureTemporaryFolder();

        var preset = ScriptableObject.CreateInstance<ShowcasePreset>();
        AssetDatabase.CreateAsset(preset, path);
        return preset;
    }

    public static ShowcasePreset SaveAndReload(ShowcasePreset preset, string path) {
        EditorUtility.SetDirty(preset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        Resources.UnloadAsset(preset);
        var reloaded = AssetDatabase.LoadAssetAtPath<ShowcasePreset>(path);
        return reloaded ?? throw new AssertionException("Showcase preset reload returned null: " + path);
    }

    public static void DeleteTemporaryFolder() {
        if (AssetDatabase.IsValidFolder(TemporaryFolder)) {
            AssetDatabase.DeleteAsset(TemporaryFolder);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    public static string ReadProjectFile(string assetPath) {
        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var filePath = Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        return File.ReadAllText(filePath);
    }

    public static void AssertResetPreset(ShowcasePreset preset) {
        AssertMoveCommand(preset.selectedCommand, new Vector2(3f, 4f), 2f);

        Assert.That(preset.commands, Has.Count.EqualTo(6));
        AssertIdleCommand(preset.commands[0]);
        AssertMoveCommand(preset.commands[1], new Vector2(3f, 4f), 2f);
        AssertSpeakCommand(preset.commands[2], "Hello from Spire");

        var firstEffect = GetEffect(preset.commands[3]);
        var secondEffect = GetEffect(preset.commands[4]);
        Assert.That(firstEffect, Is.TypeOf<DamageEffect>());
        Assert.That(secondEffect, Is.SameAs(firstEffect));
        Assert.That(((DamageEffect) firstEffect!).amount, Is.EqualTo(10));
        Assert.That(GetEffect(preset.commands[5]), Is.Null);

        AssertRectangle(preset.shape, 3f, 4f);

        AssertSuccess(preset.result, 42);

    }

    public static Effect? GetEffect(GameplayCommand command) {
        switch (command) {
            case { kind: GameplayCommand.Kind.ApplyEffect, applyEffect: var payload }:
                return payload.effect;
            default:
                throw new AssertionException("Expected an ApplyEffect command.");
        }
    }

    public static void AssertIdleCommand(GameplayCommand command) {
        switch (command) {
            case { kind: GameplayCommand.Kind.Idle }:
            break;
            default:
                Assert.Fail("Expected an Idle command.");
            break;
        }
    }

    public static void AssertMoveCommand(GameplayCommand command, Vector2 expectedDestination, float expectedSpeed) {
        switch (command) {
            case { kind: GameplayCommand.Kind.Move, move: var payload }:
                Assert.That(payload.destination, Is.EqualTo(expectedDestination));
                Assert.That(payload.speed, Is.EqualTo(expectedSpeed));
            break;
            default:
                Assert.Fail("Expected a Move command.");
            break;
        }
    }

    public static void AssertSpeakCommand(GameplayCommand command, string expectedSpeech) {
        switch (command) {
            case { kind: GameplayCommand.Kind.Speak, speak: var payload }:
                Assert.That(payload.speech, Is.EqualTo(expectedSpeech));
            break;
            default:
                Assert.Fail("Expected a Speak command.");
            break;
        }
    }

    private static void AssertRectangle(Shape shape, float expectedWidth, float expectedHeight) {
        switch (shape) {
            case { kind: Shape.Kind.Rectangle, rectangle: var payload }:
                Assert.That(payload.width, Is.EqualTo(expectedWidth));
                Assert.That(payload.height, Is.EqualTo(expectedHeight));
            break;
            default:
                Assert.Fail("Expected a Rectangle shape.");
            break;
        }
    }

    public static void AssertPoint(Shape shape) {
        switch (shape) {
            case { kind: Shape.Kind.Point }:
            break;
            default:
                Assert.Fail("Expected a Point shape.");
            break;
        }
    }

    private static void AssertSuccess(OperationResult<int> result, int expectedValue) {
        switch (result) {
            case { kind: OperationResult<int>.Kind.Success, success: var payload }:
                Assert.That(payload.value, Is.EqualTo(expectedValue));
            break;
            default:
                Assert.Fail("Expected a successful operation result.");
            break;
        }
    }

    public static void AssertFailure(OperationResult<int> result, string expectedError) {
        switch (result) {
            case { kind: OperationResult<int>.Kind.Failure, failure: var payload }:
                Assert.That(payload.error, Is.EqualTo(expectedError));
            break;
            default:
                Assert.Fail("Expected a failed operation result.");
            break;
        }
    }

    public static void AssertAuthoredPreset(ShowcasePreset preset) {
        AssertMoveCommand(preset.selectedCommand, new Vector2(5f, 5f), 10f);

        Assert.That(preset.commands, Has.Count.EqualTo(6));
        AssertMoveCommand(preset.commands[0], Vector2.zero, 50f);
        AssertIdleCommand(preset.commands[1]);
        AssertSpeakCommand(preset.commands[2], "Hello from Spire");
        AssertSpeakCommand(preset.commands[3], string.Empty);

        var damage = GetEffect(preset.commands[4]);
        Assert.That(damage, Is.TypeOf<DamageEffect>());
        Assert.That(((DamageEffect) damage!).amount, Is.EqualTo(10));
        Assert.That(GetEffect(preset.commands[5]), Is.Null);

        AssertRectangle(preset.shape, 2554f, 35f);
        AssertSuccess(preset.result, 42);
    }

}
}
