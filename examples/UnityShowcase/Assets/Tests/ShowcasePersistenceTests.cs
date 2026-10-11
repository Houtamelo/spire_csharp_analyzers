#nullable enable

using Houtamelo.Spire.Showcase;
using NUnit.Framework;
using UnityEditor;
using System.Collections.Generic;
using System;
using System.IO;
using UnityEngine;

namespace Houtamelo.Spire.Showcase.Tests
{
    public sealed class ShowcasePersistenceTests
    {
        [SetUp]
        public void SetUp()
        {
            ShowcaseTestSupport.DeleteTemporaryFolder();
        }

        [TearDown]
        public void TearDown()
        {
            ShowcaseTestSupport.DeleteTemporaryFolder();
        }

        [Test]
        public void ResetExamples_AllLayoutsPersistAcrossSaveUnloadAndReload()
        {
            var preset = ShowcaseTestSupport.CreatePreset(ShowcaseTestSupport.PresetPath);
            preset.ResetExamples();

            var reloaded = ShowcaseTestSupport.SaveAndReload(preset, ShowcaseTestSupport.PresetPath);
            ShowcaseTestSupport.AssertResetPreset(reloaded);
        }

        [Test]
        public void ManagedReferences_PreserveSharingAndNullWithinOneAsset()
        {
            var preset = ShowcaseTestSupport.CreatePreset(ShowcaseTestSupport.PresetPath);
            preset.ResetExamples();

            var beforeReloadFirst = ShowcaseTestSupport.GetEffect(preset.commands[3]);
            var beforeReloadSecond = ShowcaseTestSupport.GetEffect(preset.commands[4]);
            Assert.That(beforeReloadSecond, Is.SameAs(beforeReloadFirst));
            Assert.That(ShowcaseTestSupport.GetEffect(preset.commands[5]), Is.Null);

            var reloaded = ShowcaseTestSupport.SaveAndReload(preset, ShowcaseTestSupport.PresetPath);
            var afterReloadFirst = ShowcaseTestSupport.GetEffect(reloaded.commands[3]);
            var afterReloadSecond = ShowcaseTestSupport.GetEffect(reloaded.commands[4]);
            Assert.That(afterReloadFirst, Is.TypeOf<DamageEffect>());
            Assert.That(afterReloadSecond, Is.SameAs(afterReloadFirst));
            Assert.That(ShowcaseTestSupport.GetEffect(reloaded.commands[5]), Is.Null);
        }

        [Test]
        public void AlternateAndEmptyVariants_PersistWithHealingEffectAndNull()
        {
            var preset = ShowcaseTestSupport.CreatePreset(ShowcaseTestSupport.PresetPath);
            preset.selectedCommand = GameplayCommand.Idle();
            preset.commands = new List<GameplayCommand>
        {
            GameplayCommand.ApplyEffect(new HealingEffect(15)),
            GameplayCommand.ApplyEffect(null),
        };
            preset.shape = Shape.Point();
            preset.result = OperationResult<int>.Failure("declined");
            preset.boxedTuple = BoxedTupleStats.Empty();
            preset.unsafeOverlap = UnsafeStats.Empty();
            preset.overlap = OverlapShape.Point();

            var reloaded = ShowcaseTestSupport.SaveAndReload(preset, ShowcaseTestSupport.PresetPath);
            ShowcaseTestSupport.AssertIdleCommand(reloaded.selectedCommand);
            Assert.That(reloaded.commands, Has.Count.EqualTo(2));
            var healing = ShowcaseTestSupport.GetEffect(reloaded.commands[0]);
            Assert.That(healing, Is.TypeOf<HealingEffect>());
            Assert.That(((HealingEffect)healing!).amount, Is.EqualTo(15));
            Assert.That(ShowcaseTestSupport.GetEffect(reloaded.commands[1]), Is.Null);
            ShowcaseTestSupport.AssertPoint(reloaded.shape);
            ShowcaseTestSupport.AssertFailure(reloaded.result, "declined");
            ShowcaseTestSupport.AssertEmpty(reloaded.boxedTuple);
            ShowcaseTestSupport.AssertEmpty(reloaded.unsafeOverlap);
            ShowcaseTestSupport.AssertPoint(reloaded.overlap);
        }

        [Test]
        public void FormerlySerializedAsSay_RestoresAndCanonicalizesOnSave()
        {
            var preset = ShowcaseTestSupport.CreatePreset(ShowcaseTestSupport.PresetPath);
            preset.selectedCommand = GameplayCommand.Speak("renamed");
            EditorUtility.SetDirty(preset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(ShowcaseTestSupport.PresetPath, ImportAssetOptions.ForceUpdate);

            var assetYaml = ShowcaseTestSupport.ReadProjectFile(ShowcaseTestSupport.PresetPath);
            const string currentTypeIdentity = "GameplayCommand/__SpireSpeakPayload";
            const string formerTypeIdentity = "GameplayCommand/__SpireSayPayload";
            Assert.That(assetYaml, Does.Contain(currentTypeIdentity));
            assetYaml = assetYaml.Replace(currentTypeIdentity, formerTypeIdentity, StringComparison.Ordinal);

            const string selectedCommandMarker = "  selectedCommand:\n";
            var markerIndex = assetYaml.IndexOf(selectedCommandMarker, StringComparison.Ordinal);
            Assert.That(markerIndex, Is.GreaterThanOrEqualTo(0));
            assetYaml = assetYaml.Insert(
                markerIndex + selectedCommandMarker.Length,
                "    _kind: 255\n");
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var assetFilePath = Path.Combine(
                projectRoot,
                ShowcaseTestSupport.PresetPath.Replace('/', Path.DirectorySeparatorChar));
            File.WriteAllText(assetFilePath, assetYaml);

            AssetDatabase.ImportAsset(ShowcaseTestSupport.PresetPath, ImportAssetOptions.ForceUpdate);
            Resources.UnloadAsset(preset);
            var reloaded = AssetDatabase.LoadAssetAtPath<ShowcasePreset>(ShowcaseTestSupport.PresetPath);
            Assert.That(reloaded, Is.Not.Null);
            ShowcaseTestSupport.AssertSpeakCommand(reloaded!.selectedCommand, "renamed");

            EditorUtility.SetDirty(reloaded);
            AssetDatabase.SaveAssets();
            var canonicalYaml = ShowcaseTestSupport.ReadProjectFile(ShowcaseTestSupport.PresetPath);
            Assert.That(canonicalYaml, Does.Contain(currentTypeIdentity));
            Assert.That(canonicalYaml, Does.Not.Contain(formerTypeIdentity));
            Assert.That(canonicalYaml, Does.Not.Contain("_kind: 255"));
        }

        [Test]
        public void ManagedReferences_AreIndependentAcrossAssetHosts()
        {
            var first = ShowcaseTestSupport.CreatePreset(ShowcaseTestSupport.FirstHostPath);
            var second = ShowcaseTestSupport.CreatePreset(ShowcaseTestSupport.SecondHostPath);
            first.ResetExamples();
            second.ResetExamples();

            var firstReloaded = ShowcaseTestSupport.SaveAndReload(first, ShowcaseTestSupport.FirstHostPath);
            var secondReloaded = ShowcaseTestSupport.SaveAndReload(second, ShowcaseTestSupport.SecondHostPath);

            var firstEffect = (DamageEffect)ShowcaseTestSupport.GetEffect(firstReloaded.commands[3])!;
            var firstSharedEffect = (DamageEffect)ShowcaseTestSupport.GetEffect(firstReloaded.commands[4])!;
            var secondEffect = (DamageEffect)ShowcaseTestSupport.GetEffect(secondReloaded.commands[3])!;
            var secondSharedEffect = (DamageEffect)ShowcaseTestSupport.GetEffect(secondReloaded.commands[4])!;

            Assert.That(firstSharedEffect, Is.SameAs(firstEffect));
            Assert.That(secondSharedEffect, Is.SameAs(secondEffect));
            Assert.That(secondEffect, Is.Not.SameAs(firstEffect));

            firstEffect.amount = 99;
            EditorUtility.SetDirty(firstReloaded);
            AssetDatabase.SaveAssets();
            Assert.That(secondEffect.amount, Is.EqualTo(10));
        }

        [Test]
        public void RepeatedUnchangedSave_ProducesStablePresetYaml()
        {
            var preset = ShowcaseTestSupport.CreatePreset(ShowcaseTestSupport.PresetPath);
            preset.ResetExamples();
            var reloaded = ShowcaseTestSupport.SaveAndReload(preset, ShowcaseTestSupport.PresetPath);
            var firstYaml = ShowcaseTestSupport.ReadProjectFile(ShowcaseTestSupport.PresetPath);

            EditorUtility.SetDirty(reloaded);
            AssetDatabase.SaveAssets();
            var secondYaml = ShowcaseTestSupport.ReadProjectFile(ShowcaseTestSupport.PresetPath);

            Assert.That(secondYaml, Is.EqualTo(firstYaml));
        }
    }
}
