#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spire.UnityIntegration
{

/// Compact real-Editor integration fixture for the current variant-payload API.
/// Representation-specific legacy layout cases are covered by the migration note
/// in the Unity test matrix and by tests/UnityVariantPayloadCore.
public static class SpireUnityIntegration
{
    private const string AssetFolder = "Assets/SpireUnityIntegration";
    private const string ReportName = "unity-integration-results.json";
    private const string AssetPath = AssetFolder + "/UnitySerializationAsset.asset";
    private const string ManagedReferenceAssetPath = AssetFolder + "/UnityManagedReferenceAsset.asset";
    private const string TexturePath = AssetFolder + "/UnityIntegrationTexture.asset";
    private const string ScenePath = AssetFolder + "/UnityIntegrationPlayer.unity";
    private const string BuildReportPath = AssetFolder + "/unity-integration-player-build-report.json";
    private const string BuildRelativePath = "Builds/UnityIntegrationPlayer/SpireUnityIntegration.x86_64";

    [MenuItem("Spire/Run Unity 6000 integration")]
    public static void Run()
    {
        var report = new IntegrationReport
        {
            probe = "SpireUnityIntegration",
            unityVersion = Application.unityVersion,
            status = "fail",
            checks = 0,
            failures = new List<string>(),
        };

        try
        {
            EnsureAssetFolder();
            RunPersistenceChecks(report);
            RunGeneratedSurfaceChecks(report);
            report.status = report.failures.Count == 0 ? "pass" : "fail";
        }
        catch (Exception exception)
        {
            report.failures.Add(exception.ToString());
            report.status = "fail";
        }
        finally
        {
            CleanupAssets();
            WriteReport(report);
        }

        EditorApplication.Exit(report.status == "pass" ? 0 : 1);
    }

    public static void BuildPlayer()
        => BuildPlayer(ScriptingImplementation.Mono2x, UnityIntegrationFixtureProfile.BaselineOnly);

    public static void BuildIl2CppPlayer()
        => BuildPlayer(ScriptingImplementation.IL2CPP, UnityIntegrationFixtureProfile.BaselineOnly);

    public static void BuildIl2CppPlayerBaselineOnly()
        => BuildPlayer(ScriptingImplementation.IL2CPP, UnityIntegrationFixtureProfile.BaselineOnly);

    public static void BuildIl2CppPlayerManagedReferenceOnly()
        => BuildPlayer(ScriptingImplementation.IL2CPP, UnityIntegrationFixtureProfile.ManagedReferenceOnly);

    public static void BuildIl2CppPlayerManagedReferenceNullOnly()
        => BuildPlayer(ScriptingImplementation.IL2CPP, UnityIntegrationFixtureProfile.ManagedReferenceNullOnly);

    public static void BuildIl2CppPlayerManagedReferenceSharedOnly()
        => BuildPlayer(ScriptingImplementation.IL2CPP, UnityIntegrationFixtureProfile.ManagedReferenceSharedOnly);

    public static void BuildIl2CppPlayerManagedReferenceCycleOnly()
        => BuildPlayer(ScriptingImplementation.IL2CPP, UnityIntegrationFixtureProfile.ManagedReferenceCycleOnly);

    public static void BuildIl2CppPlayerMainBaselineAndManaged()
        => BuildPlayer(ScriptingImplementation.IL2CPP, UnityIntegrationFixtureProfile.MainBaselineAndManaged);

    public static void BuildIl2CppPlayerStabilityBaselineAndManaged()
        => BuildPlayer(ScriptingImplementation.IL2CPP, UnityIntegrationFixtureProfile.StabilityBaselineAndManaged);

    private static void RunPersistenceChecks(IntegrationReport report)
    {
        var texture = new Texture2D(2, 2) { name = "UnityIntegrationTexture" };
        AssetDatabase.CreateAsset(texture, TexturePath);

        var asset = ScriptableObject.CreateInstance<UnitySerializationAsset>();
        asset.single = UnityShapeSerializable.Label("saved", texture);
        asset.array = new[]
        {
            UnityCollectionUnion.Child(UnityCallbackChild.Text(1, "array", texture)),
            UnityCollectionUnion.Number(2.5f),
            UnityCollectionUnion.Empty(),
        };
        asset.list = new List<UnityCollectionUnion>
        {
            UnityCollectionUnion.Number(8.5f),
            UnityCollectionUnion.Child(UnityCallbackChild.Text(2, "list", texture)),
            UnityCollectionUnion.Empty(),
        };
        asset.parent = UnityCallbackParent.Parent(UnityCallbackChild.Text(3, "parent", texture), 9);
        asset.original = UnityCopyProbe.Text(4, "original", texture);
        asset.sharedCopy = asset.original;
        asset.detachedCopy = UnityCopyProbe.Number(7.5f);
        asset.genericInt = UnityGenericUnion<int>.Some(42);
        asset.genericString = UnityGenericUnion<string>.Some("generic");
        asset.boxedFields = UnityBoxedFieldsUnion.Text("boxed-fields", texture);
        asset.boxedTuple = UnityBoxedTupleUnion.Number(26);
        asset.boxedFieldsGenericInt = UnityBoxedFieldsGeneric<int>.Some(27);
        asset.boxedTupleGenericString = UnityBoxedTupleGeneric<string>.Some("boxed-generic");
        asset.unsafeUnion = UnityUnsafeOverlapUnion.Mixed(123, 2.5f, "unsafe", texture);
        asset.unsafeGenericInt = UnityUnsafeOverlapGeneric<int>.Some(37, 19, 1.75f);
        asset.unsafeGenericString = UnityUnsafeOverlapGeneric<string>.Some("unsafe-generic", 19, 1.75f);
        asset.overlapUnion = UnityOverlapUnion.Rectangle(2.5f, 4.5f);
        asset.overlapReference = UnityOverlapReferenceUnion.Text("overlap", texture);
        asset.nested = new UnityNestedOwnerData(
            UnityShapeSerializable.Circle(31.25f),
            new[] { UnityCollectionUnion.Empty() },
            new List<UnityCollectionUnion> { UnityCollectionUnion.Number(4f) },
            UnityShapeSerializable.Label("private-child", texture));
        asset.composite = new UnityCompositeDerived(
            UnityShapeSerializable.Label("base-child", texture),
            UnityShapeSerializable.Circle(12.5f));
        asset.stability.migration = StabilityMigrationUnion.Renamed(7);

        AssetDatabase.CreateAsset(asset, AssetPath);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);

        var reloaded = AssetDatabase.LoadAssetAtPath<UnitySerializationAsset>(AssetPath);
        Require(reloaded != null, "The serialization asset did not reload.");

        Check(report, "AssetSaveReloadRestoresPayloads", () =>
        {
            var value = reloaded!.single;
            if (value.kind != UnityShapeSerializable.Kind.Label)
                throw new InvalidOperationException("The reloaded shape kind was not Label.");
            var label = value.label;
            Require(label.text == "saved" && label.icon == texture, "The reloaded Label payload differed.");

            var arrayChild = reloaded.array[0];
            if (arrayChild.kind != UnityCollectionUnion.Kind.Child)
                throw new InvalidOperationException("The array child kind differed.");
            var arrayNested = arrayChild.child.child;
            if (arrayNested.kind != UnityCallbackChild.Kind.Text)
                throw new InvalidOperationException("The array nested child kind differed.");
            var arrayText = arrayNested.text;
            Require(arrayText.label == "array" && arrayText.asset == texture, "The array child payload differed.");

            var parent = reloaded.parent;
            if (parent.kind != UnityCallbackParent.Kind.Parent)
                throw new InvalidOperationException("The nested parent kind differed.");
            var parentPayload = parent.parent;
            Require(parentPayload.marker == 9, "The nested parent marker differed.");
            if (parentPayload.child.kind != UnityCallbackChild.Kind.Text)
                throw new InvalidOperationException("The nested child kind differed.");
            Require(parentPayload.child.text.label == "parent", "The nested child payload differed.");
        });

        Check(report, "CollectionsAndPrivateAnnotations", () =>
        {
            Require(reloaded!.list.Count == 3, "The list count differed after reload.");
            var listChild = reloaded.list[1];
            if (listChild.kind != UnityCollectionUnion.Kind.Child)
                throw new InvalidOperationException("The list child kind differed.");
            var listNested = listChild.child.child;
            if (listNested.kind != UnityCallbackChild.Kind.Text)
                throw new InvalidOperationException("The list nested child kind differed.");
            Require(listNested.text.asset == texture, "The list child reference differed.");
            var privateNested = reloaded.nested.Child;
            if (privateNested.kind != UnityShapeSerializable.Kind.Label)
                throw new InvalidOperationException("The private nested child kind differed.");
            Require(privateNested.label.text == "private-child", "The private nested child differed.");
            var baseChild = reloaded.composite.BaseChild;
            if (baseChild.kind != UnityShapeSerializable.Kind.Label)
                throw new InvalidOperationException("The private base child kind differed.");
            if (reloaded.composite.derivedChild.kind != UnityShapeSerializable.Kind.Circle)
                throw new InvalidOperationException("The derived child kind differed.");
        });

        Check(report, "CopiedValuesRemainIndependent", () =>
        {
            if (reloaded!.original.kind != UnityCopyProbe.Kind.Text)
                throw new InvalidOperationException("The original copy kind differed.");
            Require(reloaded.original.text.label == "original", "The original copy changed.");
            if (reloaded.detachedCopy.kind != UnityCopyProbe.Kind.Number)
                throw new InvalidOperationException("The detached copy kind differed.");
            Require(reloaded.detachedCopy.number.value == 7.5f, "The detached copy value differed.");
        });

        Check(report, "GenericAndReferenceStorageRoundTrip", () =>
        {
            if (reloaded!.genericInt.kind != UnityGenericUnion<int>.Kind.Some)
                throw new InvalidOperationException("The int generic kind differed.");
            Require(reloaded.genericInt.some.value == 42, "The int generic value differed.");
            if (reloaded.genericString.kind != UnityGenericUnion<string>.Kind.Some)
                throw new InvalidOperationException("The string generic kind differed.");
            Require(reloaded.genericString.some.value == "generic", "The string generic value differed.");
            if (reloaded.boxedFields.kind != UnityBoxedFieldsUnion.Kind.Text)
                throw new InvalidOperationException("The boxed-fields kind differed.");
            Require(reloaded.boxedFields.text.label == "boxed-fields", "The boxed-fields payload differed.");
            if (reloaded.boxedTuple.kind != UnityBoxedTupleUnion.Kind.Number)
                throw new InvalidOperationException("The boxed-tuple kind differed.");
            Require(reloaded.boxedTuple.number.value == 26, "The boxed-tuple payload differed.");
            if (reloaded.unsafeUnion.kind != UnityUnsafeOverlapUnion.Kind.Mixed)
                throw new InvalidOperationException("The unsafe union kind differed.");
            Require(reloaded.unsafeUnion.mixed.raw == 123, "The unsafe union payload differed.");
            if (reloaded.overlapUnion.kind != UnityOverlapUnion.Kind.Rectangle)
                throw new InvalidOperationException("The overlap union kind differed.");
            Require(reloaded.overlapUnion.rectangle.width == 2.5f, "The overlap payload differed.");
        });

        Check(report, "SerializedKindAndManagedPayloadAreDiscoverable", () =>
        {
            var serialized = new SerializedObject(reloaded!);
            serialized.Update();
            Require(serialized.FindProperty("single._kind") != null, "The serialized kind field was unavailable.");
            Require(serialized.FindProperty("single._serializedPayload") != null, "The serialized payload field was unavailable.");
            Require(serialized.FindProperty("single._serializedPayload.value._text") != null,
                "The private payload backing field was not exposed to Unity serialization.");
            Require(serialized.FindProperty("single._serializedPayload.value.text") == null,
                "The logical payload getter was incorrectly serialized as a field.");
            var yaml = File.ReadAllText(ProjectPath(AssetPath));
            Require(yaml.Contains("_kind:", StringComparison.Ordinal), "The serialized YAML did not contain _kind.");
            Require(yaml.Contains("_serializedPayload:", StringComparison.Ordinal), "The serialized YAML did not contain _serializedPayload.");
        });

        Check(report, "AliasPayloadUsesCanonicalRuntimeShape", () =>
        {
            var migration = reloaded!.stability.migration;
            if (migration.kind != StabilityMigrationUnion.Kind.Renamed)
                throw new InvalidOperationException("The alias migration kind differed.");
            Require(migration.renamed.value == 7, "The alias migration value differed.");
        });

        var managed = ScriptableObject.CreateInstance<UnityManagedReferenceAsset>();
        var sharedNode = new UnityManagedReferenceNodeLeaf { id = "shared", amount = 61 };
        managed.data.sharedNode = sharedNode;
        managed.data.additive = UnityManagedReferenceAdditiveUnion.Reference(sharedNode);
        managed.data.boxedFields = UnityManagedReferenceBoxedFieldsUnion.Reference(sharedNode);
        managed.data.boxedTuple = UnityManagedReferenceBoxedTupleUnion.Reference(sharedNode);
        managed.data.renamed = UnityManagedReferenceRenamedUnion.Reference(sharedNode);
        managed.second.additive = UnityManagedReferenceAdditiveUnion.Reference(null);
        AssetDatabase.CreateAsset(managed, ManagedReferenceAssetPath);
        EditorUtility.SetDirty(managed);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(ManagedReferenceAssetPath, ImportAssetOptions.ForceUpdate);
        managed = AssetDatabase.LoadAssetAtPath<UnityManagedReferenceAsset>(ManagedReferenceAssetPath);
        Require(managed != null, "The managed-reference asset did not reload.");

        Check(report, "ManagedReferenceGraphIdentitySurvivesReload", () =>
        {
            var data = managed!.data;
            Require(data.sharedNode != null, "The shared managed reference was null after reload.");
            if (data.additive.kind != UnityManagedReferenceAdditiveUnion.Kind.Reference)
                throw new InvalidOperationException("The managed reference union kind differed.");
            Require(ReferenceEquals(data.sharedNode, data.additive.reference.value), "The additive managed reference did not retain identity.");
            if (data.boxedFields.kind != UnityManagedReferenceBoxedFieldsUnion.Kind.Reference)
                throw new InvalidOperationException("The boxed managed reference kind differed.");
            Require(ReferenceEquals(data.sharedNode, data.boxedFields.reference.value), "The boxed managed reference did not retain identity.");
            if (managed!.second.additive.kind != UnityManagedReferenceAdditiveUnion.Kind.Reference)
                throw new InvalidOperationException("The null managed reference kind differed.");
            Require(managed.second.additive.reference.value == null, "The null managed reference was not retained.");
        });
    }

    private static void RunGeneratedSurfaceChecks(IntegrationReport report)
    {
        Check(report, "PublicPayloadAndWrapperSurface", () =>
        {
            var assembly = typeof(UnityShapeSerializable).Assembly;
            var payloadType = assembly.GetType("Spire.UnityIntegration.UnityShapeSerializableLabel", throwOnError: false);
            Require(payloadType != null && payloadType.IsValueType, "The public Label payload struct was not emitted.");
            Require(payloadType!.GetField("_text", BindingFlags.Instance | BindingFlags.NonPublic) != null,
                "The private payload backing field was not emitted.");
            Require(payloadType.GetProperty("text", BindingFlags.Instance | BindingFlags.Public) != null,
                "The public payload getter was not emitted.");
            Require(assembly.GetType("Houtamelo.Spire.Generated.SpireSerializedPayload`1", throwOnError: false) != null,
                "The shared generic serialized wrapper was not emitted.");
        });

        Check(report, "FactoryDoesNotAllocatePayloadUntilPreparation", () =>
        {
            var value = UnityShapeSerializable.Label("factory", null!);
            var field = typeof(UnityShapeSerializable).GetField("_serializedPayload", BindingFlags.Instance | BindingFlags.NonPublic);
            Require(field != null && field.GetValue(value) == null, "Factory construction allocated a serialized payload.");
            value = value.__SpirePrepareForSerialization();
            Require(field!.GetValue(value) != null, "Serialization preparation did not create a payload.");
        });
    }

    private static void Check(IntegrationReport report, string name, Action action)
    {
        report.checks++;
        try
        {
            action();
            report.passed.Add(name);
        }
        catch (Exception exception)
        {
            report.failures.Add(name + ": " + exception);
        }
    }

    private static void BuildPlayer(ScriptingImplementation requestedBackend, UnityIntegrationFixtureProfile profile)
    {
        var result = new PlayerBuildReport
        {
            unityVersion = Application.unityVersion,
            backend = requestedBackend.ToString(),
            fixtureProfile = profile.ToString(),
            status = "fail",
            executablePath = ProjectPath(BuildRelativePath),
        };

        try
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, requestedBackend);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.High);
            EnsureAssetFolder();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            UnitySerializationMono? host = null;
            if (ExpectsMain(profile))
            {
                var hostObject = new GameObject("UnitySerializationMono");
                host = hostObject.AddComponent<UnitySerializationMono>();
                host.single = UnityShapeSerializable.Label(UnityIntegrationPlayerHook.ExpectedLabel, null!);
                host.boxedFields = UnityBoxedFieldsUnion.Text(UnityIntegrationPlayerHook.ExpectedBoxedFieldsLabel, null!);
                host.boxedTuple = UnityBoxedTupleUnion.Number(UnityIntegrationPlayerHook.ExpectedBoxedTupleValue);
                host.boxedFieldsGenericInt = UnityBoxedFieldsGeneric<int>.Some(UnityIntegrationPlayerHook.ExpectedBoxedGenericValue);
                host.boxedTupleGenericString = UnityBoxedTupleGeneric<string>.Some(UnityIntegrationPlayerHook.ExpectedBoxedGenericLabel);
                host.unsafeUnion = UnityUnsafeOverlapUnion.Mixed(
                    UnityIntegrationPlayerHook.ExpectedUnsafeRaw,
                    UnityIntegrationPlayerHook.ExpectedUnsafeAmount,
                    UnityIntegrationPlayerHook.ExpectedUnsafeLabel,
                    null!);
                host.unsafeGenericInt = UnityUnsafeOverlapGeneric<int>.Some(
                    UnityIntegrationPlayerHook.ExpectedUnsafeGenericValue,
                    UnityIntegrationPlayerHook.ExpectedUnsafeGenericCode,
                    UnityIntegrationPlayerHook.ExpectedUnsafeGenericWeight);
                host.unsafeGenericString = UnityUnsafeOverlapGeneric<string>.Some(
                    UnityIntegrationPlayerHook.ExpectedUnsafeGenericLabel,
                    UnityIntegrationPlayerHook.ExpectedUnsafeGenericCode,
                    UnityIntegrationPlayerHook.ExpectedUnsafeGenericWeight);
                host.overlapUnion = UnityOverlapUnion.Rectangle(
                    UnityIntegrationPlayerHook.ExpectedOverlapWidth,
                    UnityIntegrationPlayerHook.ExpectedOverlapHeight);
                host.overlapReference = UnityOverlapReferenceUnion.Text(UnityIntegrationPlayerHook.ExpectedOverlapLabel, null!);
                hostObject.AddComponent<UnityIntegrationPlayerHook>().fixtureProfile = profile;
                var hook = hostObject.GetComponent<UnityIntegrationPlayerHook>()!;
                hook.expectedBackend = requestedBackend == ScriptingImplementation.IL2CPP ? "IL2CPP" : "Mono";
            }
            else
            {
                var hookObject = new GameObject("UnityIntegrationPlayerHook");
                var hook = hookObject.AddComponent<UnityIntegrationPlayerHook>();
                hook.fixtureProfile = profile;
                hook.expectedBackend = requestedBackend == ScriptingImplementation.IL2CPP ? "IL2CPP" : "Mono";
            }

            Require(EditorSceneManager.SaveScene(scene, ScenePath), "Could not save the integration player scene.");
            var build = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = result.executablePath,
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.CleanBuildCache,
            });
            result.status = build.summary.result == BuildResult.Succeeded ? "pass" : "fail";
            result.errors = build.summary.totalErrors;
            if (result.status != "pass")
                result.error = "Unity player build result: " + build.summary.result;
        }
        catch (Exception exception)
        {
            result.error = exception.ToString();
        }

        File.WriteAllText(ProjectPath(BuildReportPath), JsonUtility.ToJson(result, true));
        EditorApplication.Exit(result.status == "pass" ? 0 : 1);
    }

    private static bool ExpectsMain(UnityIntegrationFixtureProfile profile)
        => profile is UnityIntegrationFixtureProfile.Full or
            UnityIntegrationFixtureProfile.BaselineOnly or
            UnityIntegrationFixtureProfile.MainBaselineAndManaged;

    private static void EnsureAssetFolder()
    {
        if (!AssetDatabase.IsValidFolder(AssetFolder))
            AssetDatabase.CreateFolder("Assets", "SpireUnityIntegration");
    }

    private static void CleanupAssets()
    {
        foreach (var path in new[] { AssetPath, ManagedReferenceAssetPath, TexturePath })
            AssetDatabase.DeleteAsset(path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static string ProjectPath(string path)
        => Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, path));

    private static void WriteReport(IntegrationReport report)
        => File.WriteAllText(ProjectPath(ReportName), JsonUtility.ToJson(report, true));

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    [Serializable]
    private sealed class IntegrationReport
    {
        public string probe = string.Empty;
        public string unityVersion = string.Empty;
        public string status = string.Empty;
        public int checks;
        public List<string> passed = new List<string>();
        public List<string> failures = new List<string>();
    }

    [Serializable]
    private sealed class PlayerBuildReport
    {
        public string unityVersion = string.Empty;
        public string backend = string.Empty;
        public string fixtureProfile = string.Empty;
        public string status = string.Empty;
        public int errors;
        public string executablePath = string.Empty;
        public string error = string.Empty;
    }
}
}
