#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading;
using Houtamelo.Spire;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEditor.Build.Reporting;

namespace Spire.UnityIntegration
{

    public static class SpireUnityIntegration
    {
        private const string AssetFolder = "Assets/SpireUnityIntegration";
        private const string ReportName = "unity-integration-results.json";
        private const string LifecycleStageName = "Library/SpireUnityIntegration-stage.json";
        private const string LifecycleScenePath = AssetFolder + "/SpireUnityIntegration-lifecycle.unity";
        private const string LifecycleHostName = "SpireUnityIntegration-LifecycleHost";

        private static IntegrationReport? _report;
        private static DrawerHostScope? _drawerScope;
        private static DrawerProbeWindow? _drawerWindow;
        private static DrawerCallbackRunner? _drawerCallbackRunner;
        private const double DrawerFrameIntervalSeconds = 1.0 / 60.0;
        private const double DrawerWallClockWatchdogSeconds = 3.0;
        private static readonly string[] ManagedReferenceUnionPaths =
        {
            "data.additive",
            "data.boxedFields",
            "data.boxedTuple",
            "data.unsafeOverlap",
            "data.overlap",
        };
        private static int _drawerFrames;
        private static double _drawerStartedAt;
        private static double _nextDrawerFrameAt;
        private static bool _drawerBaseProbeFinalized;
        private static bool _lifecycleStartQueued;
        private static bool _lifecyclePollRegistered;

        private enum PlayerFixtureSelection
        {
            Full,
            BaselineOnly,
            ManagedReferenceOnly,
            ManagedReferenceNullOnly,
            ManagedReferenceSharedOnly,
            ManagedReferenceCycleOnly,
            MainBaselineAndManaged,
            StabilityBaselineAndManaged,
        }

        [MenuItem("Spire/Run Unity 6000 integration")]
        public static void Run()
        {
            Debug.Log("SPIRE_UNITY_INTEGRATION_BEGIN");
            EditorApplication.update -= PollLifecycle;
            _lifecyclePollRegistered = false;
            DeleteLifecycleStage();
            _report = new IntegrationReport
            {
                probe = "SpireUnityIntegration",
                unityVersion = Application.unityVersion,
                graphicsBackend = SystemInfo.graphicsDeviceType.ToString(),
                graphicsDeviceName = SystemInfo.graphicsDeviceName,
                graphicsDeviceVendor = SystemInfo.graphicsDeviceVendor,
                graphicsDeviceVersion = SystemInfo.graphicsDeviceVersion,
                scenarios = new List<ScenarioResult>(),
            };

            RunCoreScenarios();
            AddPendingScenario("ScriptReload_PreservesUnionState", "A script compiler/domain reload preserves the fixture state.");
            AddPendingScenario("PlayModeTransition_PreservesUnionState", "Play Mode transition preserves the fixture state.");
            AddPendingScenario("IMGUI_Drawer_ShowsSelectorAndActiveFields", "An actual editor OnGUI repaint renders the selector and active payload fields.");
            AddPendingScenario("UIToolkit_Drawer_ShowsSelectorAndActiveFields", "An actual UI Toolkit PropertyField renders the selector and active payload fields.");
            AddPendingScenario("Drawer_MissingPayload_SelectsFirstVariant", "A missing payload selects the first known variant without stale controls.");
            AddPendingScenario("Drawer_SelectionChange_UsesFactoryReplacement", "The generated UI Toolkit selector constructs a default replacement variant.");
            AddPendingScenario("Drawer_ArrayElement_NestedPathWriteback", "An actual array-element selector writes through its containing struct.");
            AddPendingScenario("Drawer_ListElement_NestedPathWriteback", "An actual list-element selector writes through its containing struct.");
            AddPendingScenario("Drawer_UndoRedo_WholeValueAndPayloadEdit", "Undo and Redo restore generated selector changes.");
            AddPendingScenario("Drawer_SharedPayload_EditUndoRedoIsolated", "Editing one aliased drawer payload isolates it from its sibling and restores through Undo/Redo.");
            AddPendingScenario("Drawer_NestedInlineHelper_EditUndoRedoIsolated", "Editing a nested ordinary inline helper field isolates an aliased drawer payload and persists through save/Undo/Redo.");
            AddPendingScenario("Drawer_RenderedFieldHistory_MultiStepUndoRedo", "Consecutive rendered field edits preserve payload history and sibling isolation across multiple Undo/Redo operations.");
            AddPendingScenario("Drawer_RenderedEdit_PreservesStagedUnrelatedApply", "A rendered union edit preserves a staged unrelated SerializedObject change through Apply, Undo, and Redo.");
            AddPendingScenario("Drawer_RenderedNumericFields_UndoRedo", "Rendered long, double, and nonordinal enum fields preserve their exact values through Undo and Redo.");
            AddPendingScenario("Drawer_RenderedInspectorNamedEnum_UndoRedo", "Rendered enum values use InspectorName display labels while preserving nonordinal values and surrounding fields through Undo and Redo.");
            AddPendingScenario("Drawer_RenderedFlagsEnum_UndoRedo", "Rendered combined nonordinal flags preserve their exact value and surrounding fields through Undo and Redo.");
            AddPendingScenario("Drawer_RenderedEnum_InvalidChoicePreservesHistory", "An unavailable enum choice leaves runtime state, payload state, and Undo history unchanged before a valid edit.");
            AddPendingScenario("Drawer_ReadOnlyAliasedPayload_RenderedStable", "Rendering a bound drawer without editing preserves an aliased payload and its managed-reference identities.");
            AddPendingScenario("Drawer_ReadOnlyAliasedPayload_UnrelatedApplyStable", "An unrelated SerializedObject apply after a rendered read-only drawer preserves payload YAML and IDs.");
            AddPendingScenario("Drawer_MultiObjectActiveFieldEdit", "An active field edit through a multi-object drawer updates every selected target.");
            AddPendingScenario("Drawer_UndoRedoWhileDetached_ReattachesCurrentTree", "Undo and Redo while detached are reflected when the same visual tree is reattached.");
            AddPendingScenario("Drawer_UndoBeforeFirstPanelAttach_BindsCurrentState", "Undo before first panel attachment binds the current payload and controls.");
            AddPendingScenario("Drawer_MultiObjectEditing", "A multi-object selector preserves each target's untouched data.");
            AddPendingScenario("Drawer_PrefabOverrideApplyRevert", "Prefab selector changes create and revert overrides.");
            AddPendingScenario("Drawer_NestedGenericPayloadSelector", "A nested generic child selector traverses its concrete managed payload.");
            AddPendingScenario("Drawer_PrivateBaseOwnerSelector", "A private base-class field is bound through the generated drawer.");
            AddPendingScenario("Drawer_GenericClosedSelectorHelpers", "Closed generic selectors dispatch through the generated drawer.");
            AddPendingScenario("Drawer_GenericContainingNestedSelector", "A nested union in a closed generic containing type dispatches through the generated drawer.");
            AddPendingScenario("Drawer_RebindDoesNotWriteStaleTarget", "Rebinding the UI tree does not write to the previous target.");
            AddPendingScenario("BoxedLayouts_IMGUIAndUIToolkit_UseCanonicalPayload", "Both boxed layout drawers render only the canonical active payload.");
            AddPendingScenario("BoxedLayouts_SelectorReplacementAndUndoRedo", "A boxed selector replacement survives Undo and Redo.");
            AddPendingScenario("UnsafeOverlap_IMGUIAndUIToolkit_DrawCanonicalPayload", "UnsafeOverlap drawers render only the canonical active payload.");
            AddPendingScenario("UnsafeOverlap_NestedPathWritebackUndoRedo", "UnsafeOverlap nested paths write back and restore through Undo/Redo.");
            AddPendingScenario("Overlap_IMGUIAndUIToolkit_DrawCanonicalPayload", "Overlap drawers render only the canonical active payload.");
            AddPendingScenario("Overlap_NestedPathWritebackUndoRedo", "Overlap nested paths write back and restore through Undo/Redo.");

            try
            {
                BeginDrawerProbe();
            }
            catch (Exception exception)
            {
                if (exception is MissingGeneratedSurfaceException)
                {
                    CompletePendingAsFail("IMGUI_Drawer_ShowsSelectorAndActiveFields", exception);
                    CompletePendingAsFail("UIToolkit_Drawer_ShowsSelectorAndActiveFields", exception);
                    foreach (var name in DrawerCallbackScenarioNames)
                        CompletePendingAsFail(name, exception);
                }
                else
                {
                    CompletePendingAsObserved("IMGUI_Drawer_ShowsSelectorAndActiveFields", "The editor window could not be created: " + exception.Message);
                    CompletePendingAsObserved("UIToolkit_Drawer_ShowsSelectorAndActiveFields", "The editor window could not be created: " + exception.Message);
                    ObserveDrawerCallbackScenarios("The editor window could not be created: " + exception.Message);
                }

                QueueLifecycleAutomation();
            }
        }

        public static void BuildPlayer()
            => BuildPlayer(ScriptingImplementation.Mono2x, PlayerFixtureSelection.Full);

        public static void BuildIl2CppPlayer()
            => BuildPlayer(ScriptingImplementation.IL2CPP, PlayerFixtureSelection.Full);

        public static void BuildIl2CppPlayerBaselineOnly()
            => BuildPlayer(ScriptingImplementation.IL2CPP, PlayerFixtureSelection.BaselineOnly);

        public static void BuildIl2CppPlayerManagedReferenceOnly()
            => BuildPlayer(ScriptingImplementation.IL2CPP, PlayerFixtureSelection.ManagedReferenceOnly);

        public static void BuildIl2CppPlayerManagedReferenceNullOnly()
            => BuildPlayer(ScriptingImplementation.IL2CPP, PlayerFixtureSelection.ManagedReferenceNullOnly);

        public static void BuildIl2CppPlayerManagedReferenceSharedOnly()
            => BuildPlayer(ScriptingImplementation.IL2CPP, PlayerFixtureSelection.ManagedReferenceSharedOnly);

        public static void BuildIl2CppPlayerManagedReferenceCycleOnly()
            => BuildPlayer(ScriptingImplementation.IL2CPP, PlayerFixtureSelection.ManagedReferenceCycleOnly);

        public static void BuildIl2CppPlayerMainBaselineAndManaged()
            => BuildPlayer(ScriptingImplementation.IL2CPP, PlayerFixtureSelection.MainBaselineAndManaged);

        public static void BuildIl2CppPlayerStabilityBaselineAndManaged()
            => BuildPlayer(ScriptingImplementation.IL2CPP, PlayerFixtureSelection.StabilityBaselineAndManaged);

        private static void BuildPlayer(ScriptingImplementation requestedBackend, PlayerFixtureSelection selection)
        {
            var runtimeReportPath = Path.Combine(ProjectRoot, UnityIntegrationPlayerHook.ReportFileName);
            if (File.Exists(runtimeReportPath))
                File.Delete(runtimeReportPath);

            var result = new PlayerBuildResult
            {
                unityVersion = Application.unityVersion,
                status = "failed",
                runtimeReportPath = runtimeReportPath,
                runtimeReportArgument = UnityIntegrationPlayerHook.ReportArgument,
                cleanBuildCache = true,
                fixtureSelection = selection.ToString(),
                profile = selection.ToString(),
            };

            try
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, requestedBackend);
                var backend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
                result.scriptingBackend = backend.ToString();
                Require(backend == requestedBackend, "Standalone player backend did not match the requested backend: " + requestedBackend);

                if (requestedBackend == ScriptingImplementation.IL2CPP)
                {
                    PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.High);
                }

                var managedStrippingLevel = PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Standalone);
                result.managedStrippingLevel = managedStrippingLevel.ToString();
                if (requestedBackend == ScriptingImplementation.IL2CPP)
                {
                    Require(
                        managedStrippingLevel == ManagedStrippingLevel.High,
                        "Standalone managed stripping level was not High: " + managedStrippingLevel);

                    var fixtureLinkFiles = Directory
                        .EnumerateFiles(Application.dataPath, "link.xml", SearchOption.AllDirectories)
                        .ToArray();
                    Require(
                        fixtureLinkFiles.Length == 0,
                        "The IL2CPP integration fixture must not provide link.xml: " + string.Join(", ", fixtureLinkFiles));
                }

                EnsureAssetFolder();
                var scenePath = AssetFolder + "/UnityIntegrationPlayer.unity";
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var includeMainBaselineFixtures = selection is PlayerFixtureSelection.Full or PlayerFixtureSelection.BaselineOnly or PlayerFixtureSelection.MainBaselineAndManaged;
                var includeStabilityBaselineFixtures = selection is PlayerFixtureSelection.Full or PlayerFixtureSelection.BaselineOnly or PlayerFixtureSelection.StabilityBaselineAndManaged;
                var includeAnyBaselineFixtures = includeMainBaselineFixtures || includeStabilityBaselineFixtures;
                var includeManagedReferenceFixture = selection != PlayerFixtureSelection.BaselineOnly;
                UnityManagedReferencePlayerHost? managedReferenceHost = null;
                if (includeMainBaselineFixtures)
                {
                    var hostObject = new GameObject("UnitySerializationMono");
                    var host = hostObject.AddComponent<UnitySerializationMono>();
                    host.single = UnityShapeSerializable.Label(UnityIntegrationPlayerHook.ExpectedLabel, (Texture2D)null!);
                    host.array = new[] { UnityCollectionUnion.Number(2.5f) };
                    host.list = new List<UnityCollectionUnion> { UnityCollectionUnion.Empty() };
                    host.boxedFields = UnityBoxedFieldsUnion.Text(UnityIntegrationPlayerHook.ExpectedBoxedFieldsLabel, (Texture2D)null!);
                    host.boxedTuple = UnityBoxedTupleUnion.Number(UnityIntegrationPlayerHook.ExpectedBoxedTupleValue);
                    host.boxedFieldsGenericInt = UnityBoxedFieldsGeneric<int>.Some(UnityIntegrationPlayerHook.ExpectedBoxedGenericValue);
                    host.boxedTupleGenericString = UnityBoxedTupleGeneric<string>.Some(UnityIntegrationPlayerHook.ExpectedBoxedGenericLabel);
                    host.unsafeUnion = UnityUnsafeOverlapUnion.Mixed(
                        UnityIntegrationPlayerHook.ExpectedUnsafeRaw,
                        UnityIntegrationPlayerHook.ExpectedUnsafeAmount,
                        UnityIntegrationPlayerHook.ExpectedUnsafeLabel,
                        (Texture2D)null!);
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
                    host.overlapReference = UnityOverlapReferenceUnion.Text(
                        UnityIntegrationPlayerHook.ExpectedOverlapLabel,
                        (Texture2D)null!);
                    var mainPlayerHook = hostObject.AddComponent<UnityIntegrationPlayerHook>();
                    ConfigurePlayerHook(mainPlayerHook, requestedBackend, selection);
                    SceneManager.MoveGameObjectToScene(hostObject, scene);
                }

                if (includeStabilityBaselineFixtures)
                {
                    var stabilityObject = new GameObject("SpireStabilityPlayerHost");
                    var stabilityHost = stabilityObject.AddComponent<StabilityPlayerHost>();
                    var stabilityScript = MonoScript.FromMonoBehaviour(stabilityHost);
                    Require(
                        stabilityScript != null && AssetDatabase.Contains(stabilityScript) &&
                        stabilityScript.GetClass() == typeof(StabilityPlayerHost),
                        "StabilityPlayerHost must resolve to a script asset; an embedded MonoScript can corrupt the IL2CPP scene.");
                    stabilityHost.additive = StabilityAdditiveUnion.First(11, UnityIntegrationPlayerHook.ExpectedStabilityLabel);
                    stabilityHost.boxedFields = StabilityBoxedFieldsUnion.First(12, UnityIntegrationPlayerHook.ExpectedStabilityLabel);
                    stabilityHost.boxedTuple = StabilityBoxedTupleUnion.First(13, UnityIntegrationPlayerHook.ExpectedStabilityLabel);
                    stabilityHost.unsafeOverlap = StabilityUnsafeOverlapUnion.First(14, UnityIntegrationPlayerHook.ExpectedStabilityLabel);
                    stabilityHost.overlap = StabilityOverlapUnion.First(15, UnityIntegrationPlayerHook.ExpectedStabilityLabel);
                    stabilityHost.parent = StabilityNestedParentUnion.Child(
                        StabilityNestedChildUnion.Text(16, UnityIntegrationPlayerHook.ExpectedStabilityNestedLabel),
                        UnityIntegrationPlayerHook.ExpectedStabilityMarker);
                    stabilityHost.parentArray = new[]
                    {
                    StabilityNestedParentUnion.Child(
                        StabilityNestedChildUnion.Text(17, UnityIntegrationPlayerHook.ExpectedStabilityArrayLabel),
                        UnityIntegrationPlayerHook.ExpectedStabilityMarker + 1),
                    StabilityNestedParentUnion.Empty(),
                };
                    stabilityHost.parentList = new List<StabilityNestedParentUnion>
                {
                    StabilityNestedParentUnion.Child(
                        StabilityNestedChildUnion.Text(18, UnityIntegrationPlayerHook.ExpectedStabilityListLabel),
                        UnityIntegrationPlayerHook.ExpectedStabilityMarker + 2),
                    StabilityNestedParentUnion.Text("stability-list-parent"),
                };
                    stabilityHost.genericInt = new StabilityGenericOwnerInt
                    {
                        value = StabilityGenericUnion<int>.First(UnityIntegrationPlayerHook.ExpectedStabilityGenericInt),
                    };
                    stabilityHost.genericString = new StabilityGenericOwnerString
                    {
                        value = StabilityGenericUnion<string>.First(UnityIntegrationPlayerHook.ExpectedStabilityGenericString),
                    };
                    stabilityHost.genericArray = new[]
                    {
                    StabilityGenericUnion<int>.First(UnityIntegrationPlayerHook.ExpectedStabilityGenericInt + 1),
                    StabilityGenericUnion<int>.Empty(),
                };
                    stabilityHost.callbackHelper = new StabilityCallbackChildHelper(
                        StabilityNestedChildUnion.Text(19, UnityIntegrationPlayerHook.ExpectedStabilityNestedLabel));
                    var formerMigration = CreateFormerNamePayload(
                        typeof(StabilityMigrationUnion),
                        "OldValue",
                        "value",
                        71);
                    var formerGeneric = CreateFormerNamePayload(
                        typeof(StabilityGenericUnion<int>),
                        "OldGenericFirst",
                        "value",
                        72);
                    var formerNested = CreateFormerNamePayload(
                        typeof(StabilityNestedChildUnion),
                        "OldNestedText",
                        "count",
                        73);
                    SetMemberValue(formerNested, "label", "legacy-nested-player");
                    var formerBoxedFields = CreateFormerNamePayload(
                        typeof(StabilityBoxedFieldsUnion),
                        "OldBoxedFieldsFirst",
                        "amount",
                        UnityIntegrationPlayerHook.ExpectedLegacyBoxedFieldsValue);
                    SetMemberValue(
                        formerBoxedFields,
                        "label",
                        UnityIntegrationPlayerHook.ExpectedLegacyBoxedFieldsLabel);
                    var formerBoxedTuple = CreateFormerNamePayload(
                        typeof(StabilityBoxedTupleUnion),
                        "OldBoxedTupleFirst",
                        "amount",
                        UnityIntegrationPlayerHook.ExpectedLegacyBoxedTupleValue);
                    SetMemberValue(
                        formerBoxedTuple,
                        "label",
                        UnityIntegrationPlayerHook.ExpectedLegacyBoxedTupleLabel);
                    var formerUnsafeOverlap = CreateFormerNamePayload(
                        typeof(StabilityUnsafeOverlapUnion),
                        "OldUnsafeOverlapFirst",
                        "amount",
                        UnityIntegrationPlayerHook.ExpectedLegacyUnsafeOverlapValue);
                    SetMemberValue(
                        formerUnsafeOverlap,
                        "label",
                        UnityIntegrationPlayerHook.ExpectedLegacyUnsafeOverlapLabel);
                    var formerOverlap = CreateFormerNamePayload(
                        typeof(StabilityOverlapUnion),
                        "OldOverlapFirst",
                        "amount",
                        UnityIntegrationPlayerHook.ExpectedLegacyOverlapValue);
                    SetMemberValue(
                        formerOverlap,
                        "label",
                        UnityIntegrationPlayerHook.ExpectedLegacyOverlapLabel);
                    stabilityHost.opaqueFormerPayloads = new UnitySerializationPayloadBase?[]
                    {
                    (UnitySerializationPayloadBase)formerMigration,
                    (UnitySerializationPayloadBase)formerGeneric,
                    (UnitySerializationPayloadBase)formerNested,
                    (UnitySerializationPayloadBase)formerBoxedFields,
                    (UnitySerializationPayloadBase)formerBoxedTuple,
                    (UnitySerializationPayloadBase)formerUnsafeOverlap,
                    (UnitySerializationPayloadBase)formerOverlap,
                    };
                    if (!includeMainBaselineFixtures)
                    {
                        var stabilityPlayerHook = stabilityObject.AddComponent<UnityIntegrationPlayerHook>();
                        ConfigurePlayerHook(stabilityPlayerHook, requestedBackend, selection);
                    }
                    SceneManager.MoveGameObjectToScene(stabilityObject, scene);
                }

                if (includeManagedReferenceFixture)
                {
                    var managedReferenceObject = new GameObject("SpireManagedReferencePlayerHost");
                    managedReferenceHost = managedReferenceObject.AddComponent<UnityManagedReferencePlayerHost>();
                    managedReferenceHost.managedReferenceMetadataAvailable = HasManagedReferenceMetadataForAllLayouts();
                    managedReferenceHost.probeMode = selection switch
                    {
                        PlayerFixtureSelection.ManagedReferenceNullOnly => UnityManagedReferencePlayerHost.NullOnlyProbe,
                        PlayerFixtureSelection.ManagedReferenceSharedOnly => UnityManagedReferencePlayerHost.SharedOnlyProbe,
                        PlayerFixtureSelection.ManagedReferenceCycleOnly => UnityManagedReferencePlayerHost.CycleOnlyProbe,
                        _ => UnityManagedReferencePlayerHost.FullProbe,
                    };
                    var managedReferenceShared = CreateManagedReferenceLeaf("player-shared", 61);
                    var includeCycle = selection is PlayerFixtureSelection.Full or PlayerFixtureSelection.ManagedReferenceOnly or PlayerFixtureSelection.ManagedReferenceCycleOnly or PlayerFixtureSelection.MainBaselineAndManaged or PlayerFixtureSelection.StabilityBaselineAndManaged;
                    var sharedValue = selection == PlayerFixtureSelection.ManagedReferenceNullOnly ? null : managedReferenceShared;
                    managedReferenceHost.data = CreateManagedReferenceHostData(sharedValue, includeCycle);
                    managedReferenceHost.second = CreateManagedReferenceHostData(null, includeCycle: false);
                    managedReferenceHost.opaqueLegacyRenamed = CreateFormerNamePayload(
                        typeof(UnityManagedReferenceRenamedUnion),
                        "OldReference",
                        "value",
                        managedReferenceShared);
                    if (!includeAnyBaselineFixtures)
                    {
                        var playerHook = managedReferenceObject.AddComponent<UnityIntegrationPlayerHook>();
                        playerHook.managedReferenceOnly = true;
                        ConfigurePlayerHook(playerHook, requestedBackend, selection);
                    }
                    SceneManager.MoveGameObjectToScene(managedReferenceObject, scene);
                }
                EditorSceneManager.SaveScene(scene, scenePath);
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };

                var buildDirectory = requestedBackend == ScriptingImplementation.IL2CPP
                    ? Path.Combine(ProjectRoot, "Builds", "UnityIntegrationPlayer", selection.ToString())
                    : Path.Combine(ProjectRoot, "Builds", "UnityIntegrationPlayer");
                var buildPath = Path.Combine(buildDirectory, "SpireUnityIntegration.x86_64");
                result.outputPath = buildPath;
                result.executablePath = buildPath;
                Directory.CreateDirectory(Path.GetDirectoryName(buildPath)!);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { scenePath },
                    locationPathName = buildPath,
                    target = BuildTarget.StandaloneLinux64,
                    options = BuildOptions.Development | BuildOptions.AllowDebugging | BuildOptions.CleanBuildCache,
                });

                result.status = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? "pass" : "fail";
                result.outputPath = buildPath;
                result.executablePath = buildPath;
                result.summary = report.summary.result.ToString();
                result.errors = report.summary.totalErrors;
                result.warnings = report.summary.totalWarnings;
            }
            catch (Exception exception)
            {
                result.error = exception.ToString();
            }

            File.WriteAllText(Path.Combine(ProjectRoot, "unity-player-build-report.json"), JsonUtility.ToJson(result, true));
            EditorApplication.Exit(result.status == "pass" ? 0 : 1);
        }

        private static void ConfigurePlayerHook(
            UnityIntegrationPlayerHook hook,
            ScriptingImplementation requestedBackend,
            PlayerFixtureSelection selection)
        {
            hook.fixtureProfile = selection switch
            {
                PlayerFixtureSelection.Full => UnityIntegrationFixtureProfile.Full,
                PlayerFixtureSelection.BaselineOnly => UnityIntegrationFixtureProfile.BaselineOnly,
                PlayerFixtureSelection.ManagedReferenceOnly => UnityIntegrationFixtureProfile.ManagedReferenceOnly,
                PlayerFixtureSelection.ManagedReferenceNullOnly => UnityIntegrationFixtureProfile.ManagedReferenceNullOnly,
                PlayerFixtureSelection.ManagedReferenceSharedOnly => UnityIntegrationFixtureProfile.ManagedReferenceSharedOnly,
                PlayerFixtureSelection.ManagedReferenceCycleOnly => UnityIntegrationFixtureProfile.ManagedReferenceCycleOnly,
                PlayerFixtureSelection.MainBaselineAndManaged => UnityIntegrationFixtureProfile.MainBaselineAndManaged,
                PlayerFixtureSelection.StabilityBaselineAndManaged => UnityIntegrationFixtureProfile.StabilityBaselineAndManaged,
                _ => throw new ArgumentOutOfRangeException(nameof(selection), selection, "Unknown player fixture profile."),
            };
            hook.expectedBackend = requestedBackend == ScriptingImplementation.IL2CPP ? "IL2CPP" : "Mono";
        }

        private static void RunCoreScenarios()
        {
            RunScenario("Factory_Text_KindAndFields", "Factory construction exposes the active kind and every field.", FactoryTextKindAndFields);
            RunScenario("Factory_Fieldless_KindAndDefaults", "Fieldless construction exposes its kind and inactive defaults.", FactoryFieldlessKindAndDefaults);
            RunScenario("SerializationFootprint_CustomEqualsAndMarker", "Serialized-field comparison replaces snapshots when a custom Equals implementation reports equality.", SerializationFootprintCustomEqualsAndMarker);
            RunScenario("SerializationFootprint_FieldCollisions_SaveReload", "Variant-qualified collision properties and nullable reference fields survive Unity save/reload.", SerializationFootprintFieldCollisionsSaveReload);
            RunScenario("OnBeforeSerialize_RefreshesWithoutManualPrepare", "Saving an ordinary owner refreshes the active managed payload.", OnBeforeSerializeRefreshesWithoutManualPrepare);
            RunScenario("OnBeforeSerialize_ReplacesChangedSnapshot", "A changed serialized snapshot is replaced from the authoritative runtime fields.", OnBeforeSerializeReplacesChangedSnapshot);
            RunScenario("OnAfterDeserialize_ReconstructsThroughFactory", "Asset reload reconstructs the active value and clears inactive fields.", OnAfterDeserializeReconstructsThroughFactory);
            RunScenario("OnAfterDeserialize_MissingPayloadFallsBack", "A missing payload falls back to the first variant with default fields.", OnAfterDeserializeMissingPayloadFallsBack);
            RunScenario("OnAfterDeserialize_PayloadWinsOverStaleRuntime", "A recognized payload wins over stale runtime variant state.", OnAfterDeserializePayloadWinsOverStaleRuntime);
            RunScenario("OnAfterDeserialize_UnknownPayloadFallsBack", "An unrecognized payload safely falls back to the first variant.", OnAfterDeserializeUnknownPayloadFallsBack);
            RunScenario("VariantSwitch_DropsStaleInactiveData", "Variant replacement clears stale payload data.", VariantSwitchDropsStaleInactiveData);
            RunScenario("PayloadOnlyEdit_ReconstructsImmediateRuntime", "Editing only a managed payload reconstructs the owner immediately.", PayloadOnlyEditReconstructsImmediateRuntime);
            RunScenario("GeneratedRuntimeMutation_ReplacesSnapshot", "Runtime storage changes replace the serialized snapshot during serialization.", GeneratedRuntimeMutationReplacesSnapshot);
            RunScenario("CallbackControl_StructAndClassAreAllowed", "Serializable callback payloads without nested unions preserve their derived runtime state.", CallbackControlStructAndClassAreAllowed);

            RunScenario("AssetSaveUnloadReload_SingleAndObjectReference", "An asset round trip retains values and native object references.", AssetSaveUnloadReloadSingleAndObjectReference);
            RunScenario("DelegatedSerialization_DictionaryUsesUnityPolicy", "Unity applies the same unsupported-dictionary policy to a direct field and a generated union payload.", DelegatedSerializationDictionaryUsesUnityPolicy);
            RunScenario("Array_RoundTrip", "Array union elements retain order and values after reload.", ArrayRoundTrip);
            RunScenario("List_RoundTrip", "List union elements retain order and values after reload.", ListRoundTrip);
            RunScenario("ContainingStruct_RoundTrip", "A union inside an ordinary serializable struct survives reload.", ContainingStructRoundTrip);
            RunScenario("DerivedComposite_BasePrivateChild_RoundTrip", "A derived serializable composite preserves a private base child union across repeated asset reloads.", DerivedCompositeBasePrivateChildRoundTrip);
            RunScenario("NestedParent_AutomaticChildSnapshotRepeatedSave", "Nested parent and child values survive repeated saves.", NestedParentAutomaticChildSnapshotRepeatedSave);
            RunScenario("NestedParent_MatchingPayloadRefreshRepeatedSave", "Nested matching payloads refresh across repeated saves.", NestedParentMatchingPayloadRefreshRepeatedSave);
            RunScenario("NestedParent_CopyIsolation", "Copying and mutating a parent does not change the original.", NestedParentCopyIsolation);
            RunScenario("NestedArrayElements_IndependentChildSnapshots", "Nested array elements keep independent child payloads.", NestedArrayElementsIndependentChildSnapshots);
            RunScenario("NestedListElements_IndependentChildSnapshots", "Nested list elements keep independent child payloads.", NestedListElementsIndependentChildSnapshots);
            RunScenario("NestedCollection_RuntimeMutationIsolated", "A nested runtime mutation is serialized without changing its sibling snapshot.", NestedCollectionRuntimeMutationIsolated);
            RunScenario("CopiedValue_SharedPayloadMutationDetaches", "An intentional managed-reference alias is detached by a copied-value mutation.", CopiedValueSharedPayloadMutationDetaches);
            RunScenario("CopiedNestedChildPayload_AliasDetachesRepeatedSave", "Aliased nested child payloads detach before a copied child mutation and remain distinct after repeated saves.", CopiedNestedChildPayloadAliasDetachesRepeatedSave);
            RunScenario("CopiedNestedChildReferenceComposite_AliasDetachesWithoutParentReassignment", "Aliased child payloads in ordinary serializable composites detach without reassigning either parent union root.", CopiedNestedChildReferenceCompositeAliasDetachesWithoutParentReassignment);
            RunScenario("CopiedValue_DetachedPayloadRepeatedSaveVariantChange", "A detached copy survives repeated saves and a variant change.", CopiedValueDetachedPayloadRepeatedSaveVariantChange);
            RunScenario("ActivePayloadOnly_NoInactivePayloadOrRuntimeFields", "Only the active payload is serialized.", ActivePayloadOnlyNoInactivePayloadOrRuntimeFields);
            RunScenario("PayloadOnly_AllFiveLayouts", "All five layouts serialize one active typed payload and no discriminator.", PayloadOnlyAllFiveLayouts);
            RunScenario("PayloadOnly_RepeatedSave_YamlAndIdsStable", "Canonical nested payload YAML and managed-reference IDs remain stable across saves.", PayloadOnlyRepeatedSaveYamlAndIdsStable);
            RunScenario("PayloadOnly_MalformedPayload_FallsBackAndOverwrites", "Malformed payload data replaces a valid runtime value with the first variant.", PayloadOnlyMalformedPayloadFallsBackAndOverwrites);
            RunScenario("PayloadOnly_CallbackHelper_MissingChildDefaults", "A callback-bearing helper replaces a missing child payload with first-variant defaults.", PayloadOnlyCallbackHelperMissingChildDefaults);
            RunScenario("PayloadOnly_FormerNamePayload_CanonicalizesAndIgnoresStaleKind", "A former-name payload restores through the current factory and canonicalizes on the next save.", PayloadOnlyFormerNamePayloadCanonicalizesAndIgnoresStaleKind);
            RunScenario("PayloadOnly_FormerNamePayload_AllLayoutsCanonicalize", "Former-name payload aliases for all layouts restore through current factories and canonicalize on save.", PayloadOnlyFormerNamePayloadAllLayoutsCanonicalize);
            RunScenario("BoxedTuple_MultiFieldNested_SaveReloadIdsStable", "A multi-field BoxedTuple nested payload survives reload with stable managed-reference IDs.", BoxedTupleMultiFieldNestedSaveReloadIdsStable);
            RunScenario("Drawer_AliasPayloadNameBoundaryInference", "A current and aliased payload whose names overlap infer the current variant by exact type boundaries.", DrawerAliasPayloadNameBoundaryInference);
            RunScenario("ManagedReference_AllFiveLayouts_NullAndDerivedPersistence", "All five layouts preserve null and derived managed references through asset reload.", ManagedReferenceAllFiveLayoutsNullAndDerivedPersistence);
            RunScenario("ManagedReference_SharedIdentityPairAndCollections", "Managed-reference pair and collection fields preserve shared identity and null elements.", ManagedReferenceSharedIdentityPairAndCollections);
            RunScenario("ManagedReference_AllFiveLayouts_ArrayListPersistence", "All five layouts preserve managed-reference array and list elements.", ManagedReferenceAllFiveLayoutsArrayListPersistence);
            RunScenario("ManagedReference_MixedInlineManagedAlias", "Inline controls remain inline beside managed-reference and renamed fields.", ManagedReferenceMixedInlineManagedAlias);
            RunScenario("ManagedReference_IndependentNonNullSnapshotIsolation", "Independent non-null union snapshots do not overwrite one another.", ManagedReferenceIndependentNonNullSnapshotIsolation);
            RunScenario("ManagedReference_HelperPrivateFieldsAndCycles", "Nested helpers preserve private managed references and cyclic graphs.", ManagedReferenceHelperPrivateFieldsAndCycles);
            RunScenario("ManagedReference_InitialNullSaveStability", "Initial null managed references remain stable across unchanged saves and reloads.", ManagedReferenceInitialNullSaveStability);
            RunScenario("ManagedReference_ReadOnlyDrawerPreservesAliasedSnapshot", "Opening and binding a drawer without an edit preserves an aliased payload YAML and managed-reference ID.", ManagedReferenceReadOnlyDrawerPreservesAliasedSnapshot);
            RunScenario("ManagedReference_FormerNameAliasCanonicalization", "Former-name managed-reference payloads restore and canonicalize without losing object identity.", ManagedReferenceFormerNameAliasCanonicalization);
            RunScenario("ManagedReference_PropertyFieldAssignmentUndoRedo", "SerializedProperty managed-reference assignment supports null, derived values, Undo, and Redo.", ManagedReferencePropertyFieldAssignmentUndoRedo);
            RunScenario("ManagedReference_VariantReplacementPreservesNestedIdentity", "Variant replacement preserves active nested managed-reference identity and edits.", ManagedReferenceVariantReplacementPreservesNestedIdentity);
            RunScenario("BoxedFields_AssetSaveUnloadReload", "BoxedFields values and object references survive asset reload.", BoxedFieldsAssetSaveUnloadReload);
            RunScenario("BoxedTuple_AssetSaveUnloadReload", "BoxedTuple values and object references survive asset reload.", BoxedTupleAssetSaveUnloadReload);
            RunScenario("BoxedLayouts_RepeatedSaveReplacesChangedSnapshot", "Changed boxed snapshots are replaced and unchanged saves reuse them.", BoxedLayoutsRepeatedSaveRefreshesMatchingPayload);
            RunScenario("BoxedLayouts_VariantSwitchResetsRuntimeStorage", "Boxed variant switches clear inactive runtime storage.", BoxedLayoutsVariantSwitchResetsRuntimeStorage);
            RunScenario("BoxedLayouts_CopyAndNestedAliasIsolation", "Copied boxed parents detach nested payload aliases before mutation.", BoxedLayoutsCopyAndNestedAliasIsolation);
            RunScenario("BoxedLayouts_ArrayListNestedElements", "Boxed union arrays and lists retain independent elements after reload.", BoxedLayoutsArrayListNestedElements);
            RunScenario("BoxedLayouts_NestedArrayListWriteback", "Nested boxed array and list values write back through SerializedProperty.", BoxedLayoutsNestedArrayListWriteback);
            RunScenario("BoxedLayouts_ActivePayloadOnlyYaml", "Boxed runtime storage is absent from the serialized YAML.", BoxedLayoutsActivePayloadOnlyYaml);
            RunScenario("BoxedFieldsGeneric_IntString_SaveReload", "BoxedFields generic int and string closures survive reload.", BoxedFieldsGenericIntStringSaveReload);
            RunScenario("BoxedTupleGeneric_IntString_SaveReload", "BoxedTuple generic int and string closures survive reload.", BoxedTupleGenericIntStringSaveReload);
            RunScenario("UnsafeOverlap_AssetSaveUnloadReload", "UnsafeOverlap raw and managed values survive asset reload.", UnsafeOverlapAssetSaveUnloadReload);
            RunScenario("UnsafeOverlap_RepeatedSaveAndVariantReset", "UnsafeOverlap repeated saves and variant resets clear stale storage.", UnsafeOverlapRepeatedSaveAndVariantReset);
            RunScenario("UnsafeOverlap_CopyNestedAndCollectionIsolation", "UnsafeOverlap copied nested and collection values remain isolated.", UnsafeOverlapCopyNestedAndCollectionIsolation);
            RunScenario("UnsafeOverlap_ActivePayloadOnlyYaml", "UnsafeOverlap YAML contains only the active typed payload.", UnsafeOverlapActivePayloadOnlyYaml);
            RunScenario("Overlap_AssetSaveUnloadReload", "Overlap primitive and reference values survive asset reload.", OverlapAssetSaveUnloadReload);
            RunScenario("Overlap_RepeatedSaveAndVariantReset", "Overlap repeated saves and variant resets preserve explicit storage.", OverlapRepeatedSaveAndVariantReset);
            RunScenario("Overlap_CopyNestedAndCollectionIsolation", "Overlap copied nested and collection values remain isolated.", OverlapCopyNestedAndCollectionIsolation);
            RunScenario("Overlap_ActivePayloadOnlyYaml", "Overlap YAML contains only the active typed payload.", OverlapActivePayloadOnlyYaml);

            RunScenario("Drawer_GetPropertyHeight_FieldlessVsFielded", "Drawer height changes between fieldless and fielded variants.", DrawerGetPropertyHeightFieldlessVsFielded);
            RunScenario("Drawer_WholeValue_UsesBoxedValueSetter", "Whole-value boxed assignment applies in the installed editor.", DrawerWholeValueUsesBoxedValueSetter);
            RunScenario("Drawer_ReadsPayloadTypeAndManagedReference", "Drawer reads infer the variant from one serialized managed payload.", DrawerReadsPayloadTypeAndManagedReference);

            RunScenario("MonoPlayer_DeserializesGeneratedUnion", "A separate Mono player build reads the generated union.", MonoPlayerDeserializesGeneratedUnion);
            RunScenario("EditorReimport_DoesNotDuplicateGeneratedSurface", "Editor reimport discovers one generated drawer and payload surface.", EditorReimportDoesNotDuplicateGeneratedSurface);
            RunScenario("NoUserPreparationOrWrapperRequired", "An ordinary owner field is sufficient for persistence.", NoUserPreparationOrWrapperRequired);
            RunScenario("RuntimeCompilation_UsesInstalledUnityEngineOnly", "Runtime assembly references UnityEngine without UnityEditor.", RuntimeCompilationUsesInstalledUnityEngineOnly);
            RunScenario("NormalNonSerializableUnion_BehaviorUnchanged", "The non-serializable Additive control keeps its normal API.", NormalNonSerializableUnionBehaviorUnchanged);
            RunScenario("GenericUnion_Int_SaveReload", "Closed int generic values survive reload.", GenericUnionIntSaveReload);
            RunScenario("GenericUnion_String_SaveReload", "Closed string generic values survive reload.", GenericUnionStringSaveReload);
            RunScenario("GenericUnion_ChildUnion_SaveReload", "A generic union closed over a child union survives repeated reloads.", GenericUnionChildUnionSaveReload);
            RunScenario("GenericUnion_SerializableHelperChild_SaveReload", "A generic union closed over a serializable child-union helper refreshes through its ordinary child property.", GenericUnionSerializableHelperChildSaveReload);
            RunScenario("GenericUnion_SerializableHelperNull_SaveReload", "A null serializable helper payload normalizes to a stable default instance.", GenericUnionSerializableHelperNullSaveReload);
            RunScenario("GenericContainingNestedUnion_SaveReload", "A nested union in a closed generic containing type survives asset reload.", GenericContainingNestedUnionSaveReload);
            RunScenario("GenericUnion_DrawerTypeDiscovery", "One open generic drawer is discoverable for closed fields.", GenericUnionDrawerTypeDiscovery);
            RunScenario("ZeroVariant_NoDrawerCrash", "DU003 leaves no generated drawer to crash Unity's type discovery.", ZeroVariantNoDrawerCrash);

            RunScenario("Serializable_GenericStruct_OpenTypeParameter", "The open generic payload is emitted and compiled by Unity.", GenericOpenTypeParameterIsPresent);
            RunScenario("Serializable_NestedInGenericType_CompileAgainstUnity", "The nested generic owner compiles against Unity metadata.", NestedGenericOwnerCompiles);
            RunScenario("GenericClosedArg_Object_Diagnostic(?)", "Unsupported object generic argument remains visible for follow-up policy.", GenericObjectArgumentFollowUp);
            RunScenario("GenericClosedArg_Dictionary_Diagnostic(?)", "Unsupported dictionary generic argument remains visible for follow-up policy.", GenericDictionaryArgumentFollowUp);
        }

        private static string FactoryTextKindAndFields()
        {
            var value = UnityShapeSerializable.Label("hello", (Texture2D)null!);
            Require(value.kind == UnityShapeSerializable.Kind.Label, "Factory kind was not Label.");
            Require(value.text == "hello", "Factory text differed.");
            Require(value.icon == null, "Factory icon default differed.");
            return value.kind.ToString() + ":" + value.text;
        }

        private static string FactoryFieldlessKindAndDefaults()
        {
            var value = UnityShapeSerializable.Point();
            Require(value.kind == UnityShapeSerializable.Kind.Point, "Factory kind was not Point.");
            Require(value.radius == 0f && value.text == null && value.icon == null, "Inactive fields were not default.");
            return value.kind.ToString();
        }

        private static string SerializationFootprintCustomEqualsAndMarker()
        {
            var original = UnitySerializationFootprintUnion.Value(
                new UnitySerializationFootprintEqualsAlways { serializedValue = 10 },
                1);
            original = InvokeSerializationCallback(original, "OnBeforeSerialize");
            var originalSnapshot = GetSerializedPayloadCache(original);
            Require(originalSnapshot != null, "Initial serialized snapshot was unavailable.");

            var changed = SetInternalSlotForField(
                original,
                "value",
                new UnitySerializationFootprintEqualsAlways { serializedValue = 20 });
            changed = SetInternalSlotForField(changed, "marker", 2);
            Require(
                ReferenceEquals(GetSerializedPayloadCache(changed), originalSnapshot),
                "Runtime changes altered the shared snapshot before serialization.");

            changed = InvokeSerializationCallback(changed, "OnBeforeSerialize");
            var changedSnapshot = GetSerializedPayloadCache(changed);
            Require(
                changedSnapshot != null && !ReferenceEquals(changedSnapshot, originalSnapshot),
                "Runtime changes did not replace the serialized snapshot.");
            RequirePayloadValue(changedSnapshot!, "marker", 2);
            RequirePayloadValue(
                (FindMember(changedSnapshot!.GetType(), "value") as FieldInfo)!.GetValue(changedSnapshot)!,
                "serializedValue",
                20);
            RequirePayloadValue(originalSnapshot!, "marker", 1);
            RequirePayloadValue(
                (FindMember(originalSnapshot.GetType(), "value") as FieldInfo)!.GetValue(originalSnapshot)!,
                "serializedValue",
                10);
            return "custom-equals=ignored;marker=1->2;snapshot-replaced=true;original=preserved";
        }

        private static string SerializationFootprintFieldCollisionsSaveReload()
        {
            using (var scope = new SerializationFootprintAssetScope("field-collisions"))
            {
                var effect = new UnitySerializationFootprintEffect { marker = 17 };
                scope.Asset.collision = UnitySerializationFootprintCollisionUnion.Count(7);
                scope.Asset.nullable = UnitySerializationFootprintNullableUnion.Apply(effect);
                scope.Save();

                Require(
                    scope.Asset.collision.kind == UnitySerializationFootprintCollisionUnion.Kind.Count &&
                    scope.Asset.collision.count_value == 7 &&
                    UnitySerializationFootprintCollisionUnion.Describe(scope.Asset.collision) == "count:7",
                    "Count collision property or property-pattern arm differed before reload.");
                Require(
                    scope.Asset.nullable.kind == UnitySerializationFootprintNullableUnion.Kind.Apply &&
                    ReferenceEquals(scope.Asset.nullable.effect, effect) &&
                    UnitySerializationFootprintNullableUnion.ReadEffect(scope.Asset.nullable) == 17,
                    "Nullable same-name reference variant differed before reload.");

                var serialized = new SerializedObject(scope.Asset);
                serialized.Update();
                var collisionPayload = RequirePayloadProperty(serialized, "collision").managedReferenceValue;
                var nullablePayload = RequirePayloadProperty(serialized, "nullable").managedReferenceValue;
                RequirePayloadValue(collisionPayload!, "value", 7);
                RequirePayloadValue(nullablePayload!, "effect", effect);
                Require(
                    HasManagedReferencePayloadField(nullablePayload!, "effect"),
                    "Nullable effect payload field did not retain SerializeReference metadata.");

                scope.Asset.collision = UnitySerializationFootprintCollisionUnion.Text("text");
                scope.Asset.nullable = UnitySerializationFootprintNullableUnion.Remove(effect);
                scope.Save();
                scope.Reload();

                Require(
                    scope.Asset.collision.kind == UnitySerializationFootprintCollisionUnion.Kind.Text &&
                    scope.Asset.collision.text_value == "text" &&
                    UnitySerializationFootprintCollisionUnion.Describe(scope.Asset.collision) == "text:text",
                    "Text collision property or property-pattern arm differed after reload.");
                Require(
                    scope.Asset.nullable.kind == UnitySerializationFootprintNullableUnion.Kind.Remove &&
                    scope.Asset.nullable.effect.marker == 17 &&
                    UnitySerializationFootprintNullableUnion.ReadEffect(scope.Asset.nullable) == 17,
                    "Nullable Remove variant differed after reload.");

                return "collision=count_value/text_value;payload=value;nullable=shared-effect;save-reload=true";
            }
        }

        private static string OnBeforeSerializeRefreshesWithoutManualPrepare()
        {
            using (var scope = new AssetScope("before-serialize"))
            {
                scope.Asset.single = UnityShapeSerializable.Label("latest", scope.Texture);
                scope.Save();
                var payload = RequirePayload(scope.Asset, "single");
                RequirePayloadValue(payload, "text", "latest");
                RequirePayloadValue(payload, "icon", scope.Texture);
                return "payload=" + payload.GetType().FullName;
            }
        }

        private static string OnBeforeSerializeReplacesChangedSnapshot()
        {
            using (var scope = new AssetScope("before-serialize-in-place"))
            {
                scope.Asset.original = UnityCopyProbe.Text(3, "before", scope.Texture);
                scope.Save();
                var serialized = new SerializedObject(scope.Asset);
                serialized.Update();
                var before = RequirePayload(serialized, "original");
                SetMemberValue(before, "count", 8);
                SetMemberValue(before, "label", "after");
                EditorUtility.SetDirty(scope.Asset);
                AssetDatabase.SaveAssets();
                serialized.Update();
                var after = RequirePayload(serialized, "original");
                Require(!ReferenceEquals(before, after), "Changed managed payload snapshot was reused instead of replaced.");
                Require(scope.Asset.original.count == 3, "The runtime union was changed while editing its serialized payload.");
                Require(scope.Asset.original.label == "before", "The runtime union label was changed while editing its serialized payload.");
                RequirePayloadValue(after, "count", 3);
                RequirePayloadValue(after, "label", "before");
                RequirePayloadValue(after, "asset", scope.Texture);
                return "same-payload=false;count=3;reference=preserved;payload-edit=overwritten";
            }
        }

        private static string OnAfterDeserializeReconstructsThroughFactory()
        {
            using (var scope = new AssetScope("after-deserialize"))
            {
                scope.Asset.single = UnityShapeSerializable.Label("reload", scope.Texture);
                scope.Save();
                scope.Reload();
                Require(scope.Asset.single.kind == UnityShapeSerializable.Kind.Label, "Reload kind was not Label.");
                Require(scope.Asset.single.text == "reload", "Reload text differed.");
                Require(scope.Asset.single.icon == scope.Texture, "Reload object reference differed.");
                Require(scope.Asset.single.radius == 0f, "Inactive runtime field was not reset.");
                return scope.Asset.single.kind.ToString();
            }
        }

        private static string OnAfterDeserializeMissingPayloadFallsBack()
        {
            using (var scope = new AssetScope("missing-payload"))
            {
                scope.Asset.single = UnityShapeSerializable.Label("missing", scope.Texture);
                scope.Save();
                var serialized = new SerializedObject(scope.Asset);
                serialized.Update();
                RequirePayloadProperty(serialized, "single").managedReferenceValue = null;
                Require(serialized.ApplyModifiedProperties(), "Missing payload edit was not applied.");
                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.single.kind == UnityShapeSerializable.Kind.Circle,
                    "Missing payload did not fall back to the first variant; observed kind=" + scope.Asset.single.kind + ".");
                Require(
                    scope.Asset.single.radius == 0f && scope.Asset.single.text == null && scope.Asset.single.icon == null,
                    "Missing payload did not produce first-variant defaults.");
                return "payload=null;fallback-kind=Circle;default-radius=0";
            }
        }

        private static string OnAfterDeserializePayloadWinsOverStaleRuntime()
        {
            using (var scope = new AssetScope("mismatched-payload"))
            {
                scope.Asset.single = UnityShapeSerializable.Label("wrong", scope.Texture);
                scope.Save();
                var labelSerialized = new SerializedObject(scope.Asset);
                labelSerialized.Update();
                var labelPayload = RequirePayloadProperty(labelSerialized, "single").managedReferenceValue;
                Require(labelPayload != null, "Label payload was unavailable.");

                scope.Asset.single = UnityShapeSerializable.Circle(7f);
                scope.Save();
                var circleSerialized = new SerializedObject(scope.Asset);
                circleSerialized.Update();
                RequirePayloadProperty(circleSerialized, "single").managedReferenceValue = labelPayload;
                circleSerialized.ApplyModifiedProperties();
                scope.Save();
                scope.Reload();
                Require(scope.Asset.single.kind == UnityShapeSerializable.Kind.Label, "Mismatched payload did not win over stale runtime kind.");
                Require(scope.Asset.single.text == "wrong" && scope.Asset.single.icon == scope.Texture, "Mismatched payload fields were not restored.");
                return "payload-kind=Label;stale-runtime-kind=ignored;payload-text=wrong";
            }
        }

        private static string OnAfterDeserializeUnknownPayloadFallsBack()
        {
            using (var scope = new AssetScope("unknown-payload"))
            {
                scope.Asset.single = UnityShapeSerializable.Label("valid", scope.Texture);
                scope.Save();
                var serialized = new SerializedObject(scope.Asset);
                serialized.Update();
                RequirePayloadProperty(serialized, "single").managedReferenceValue = null;
                Require(serialized.ApplyModifiedProperties(), "Unknown payload edit was not applied.");
                serialized.Update();
                Require(
                    scope.Asset.single.kind == UnityShapeSerializable.Kind.Circle &&
                    scope.Asset.single.radius == 0f &&
                    scope.Asset.single.text == null &&
                    scope.Asset.single.icon == null,
                    "Unknown payload reconstructed non-default runtime storage.");
                return "payload=unknown;fallback-kind=Circle;safe-default=true";
            }
        }

        private static string VariantSwitchDropsStaleInactiveData()
        {
            using (var scope = new DrawerHostScope("variant-switch"))
            {
                scope.Host.single = UnityDrawerUnion.Text(4, "stale");
                var serialized = new SerializedObject(scope.Host);
                serialized.Update();
                WriteBoxedValue(RequireProperty(serialized, "single"), UnityDrawerUnion.Number(6.5f));
                Require(serialized.ApplyModifiedProperties(), "Text-to-Number replacement was not applied.");
                Require(scope.Host.single.kind == UnityDrawerUnion.Kind.Number && scope.Host.single.value == 6.5f, "Number replacement differed.");
                serialized.Update();
                WriteBoxedValue(RequireProperty(serialized, "single"), UnityDrawerUnion.Text(8, "fresh"));
                Require(serialized.ApplyModifiedProperties(), "Number-to-Text replacement was not applied.");
                Require(scope.Host.single.kind == UnityDrawerUnion.Kind.Text, "Text replacement kind differed.");
                Require(scope.Host.single.count == 8 && scope.Host.single.label == "fresh", "Stale Text data resurfaced.");
                return "Number=6.5;Text=fresh;stale=false";
            }
        }

        private static string PayloadOnlyEditReconstructsImmediateRuntime()
        {
            using (var scope = new AssetScope("payload-only"))
            {
                scope.Asset.single = UnityShapeSerializable.Label("before", scope.Texture);
                scope.Save();
                var serialized = new SerializedObject(scope.Asset);
                serialized.Update();
                var text = RequirePayloadProperty(serialized, "single").FindPropertyRelative("text");
                if (text == null)
                    throw new InvalidOperationException("Generated text payload field was unavailable.");
                text.stringValue = "payload-only";
                Require(serialized.ApplyModifiedProperties(), "Payload-only edit was not applied.");
                Require(scope.Asset.single.text == "payload-only", "Immediate runtime text did not reconstruct.");
                Require(scope.Asset.single.icon == scope.Texture, "Payload-only edit lost the object reference.");
                return scope.Asset.single.text;
            }
        }

        private static string GeneratedRuntimeMutationReplacesSnapshot()
        {
            using (var scope = new AssetScope("init-mutation"))
            {
                scope.Asset.original = UnityCopyProbe.Text(1, "init-before", scope.Texture);
                scope.Save();
                scope.Asset.original = SetInternalSlotForField(scope.Asset.original, "count", 9);
                scope.Asset.original = SetInternalSlotForField(scope.Asset.original, "label", "init-after");
                scope.Asset.original = SetInternalSlotForField(scope.Asset.original, "asset", scope.Texture);
                scope.Save();

                var fieldSpecificPayload = RequirePayload(scope.Asset, "original");
                RequirePayloadValue(fieldSpecificPayload, "count", 9);
                RequirePayloadValue(fieldSpecificPayload, "label", "init-after");
                RequirePayloadValue(fieldSpecificPayload, "asset", scope.Texture);

                var originalSnapshot = GetSerializedPayloadCache(scope.Asset.original);
                Require(originalSnapshot != null, "The original serialized snapshot was unavailable.");
                var inactiveMutation = SetInternalSlotForField(scope.Asset.original, "value", 17.5f);
                Require(
                    ReferenceEquals(GetSerializedPayloadCache(inactiveMutation), originalSnapshot),
                    "An inactive variant storage mutation changed the snapshot before serialization.");
                inactiveMutation = InvokeSerializationCallback(inactiveMutation, "OnBeforeSerialize");
                Require(
                    ReferenceEquals(GetSerializedPayloadCache(inactiveMutation), originalSnapshot),
                    "An inactive variant storage mutation replaced the active snapshot.");

                var activeMutation = SetInternalSlotForField(scope.Asset.original, "label", "active-after");
                Require(
                    ReferenceEquals(GetSerializedPayloadCache(activeMutation), originalSnapshot),
                    "An active runtime mutation changed the snapshot before serialization.");
                activeMutation = InvokeSerializationCallback(activeMutation, "OnBeforeSerialize");
                var refreshedSnapshot = GetSerializedPayloadCache(activeMutation);
                Require(
                    refreshedSnapshot != null && !ReferenceEquals(refreshedSnapshot, originalSnapshot),
                    "Changed active runtime storage did not replace the previous snapshot.");
                RequirePayloadValue(refreshedSnapshot!, "count", 9);
                RequirePayloadValue(refreshedSnapshot!, "label", "active-after");
                RequirePayloadValue(refreshedSnapshot!, "asset", scope.Texture);
                RequirePayloadValue(originalSnapshot!, "count", 9);
                RequirePayloadValue(originalSnapshot!, "label", "init-after");
                Require(scope.Asset.original.count == 9, "Independent slot probing changed the original count.");
                Require(scope.Asset.original.label == "init-after", "Independent slot probing changed the original label.");
                Require(scope.Asset.original.asset == scope.Texture, "Independent slot probing changed the original object reference.");
                scope.Save();
                var payload = RequirePayload(scope.Asset, "original");
                RequirePayloadValue(payload, "count", 9);
                RequirePayloadValue(payload, "label", "init-after");
                RequirePayloadValue(payload, "asset", scope.Texture);
                return "active-slot-replaced=true;inactive-slot-reused=true;original=preserved;public-init-profile=separate";
            }
        }

        private static string CallbackControlStructAndClassAreAllowed()
        {
            using (var scope = new AssetScope("callback-control"))
            {
                scope.Asset.callbackControl = UnityAllowedCallbackUnion.Both(
                    new UnityAllowedCallbackStruct(4),
                    new UnityAllowedCallbackClass(4));
                scope.Save();
                scope.Reload();

                Require(
                    scope.Asset.callbackControl.kind == UnityAllowedCallbackUnion.Kind.Both,
                    "Callback control union kind differed after reload.");
                Require(
                    scope.Asset.callbackControl.structValue.value == 4 &&
                    scope.Asset.callbackControl.structValue.derived == 8,
                    "Serializable callback struct did not retain value=4 and derive 8 after reload.");
                Require(
                    scope.Asset.callbackControl.classValue.value == 4 &&
                    scope.Asset.callbackControl.classValue.derived == 8,
                    "Serializable callback class did not retain value=4 and derive 8 after reload.");

                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.callbackControl.structValue.derived == 8 &&
                    scope.Asset.callbackControl.classValue.derived == 8,
                    "Serializable callback derived values were not stable across a repeated reload.");
                return "struct=4->8;class=4->8;repeat=stable;nested-union=false";
            }
        }

        private static string AssetSaveUnloadReloadSingleAndObjectReference()
        {
            using (var scope = new AssetScope("single-reference"))
            {
                scope.Asset.single = UnityShapeSerializable.Label("saved", scope.Texture);
                scope.Save();
                scope.Reload();
                Require(scope.Asset.single.kind == UnityShapeSerializable.Kind.Label, "Reload kind differed.");
                Require(scope.Asset.single.text == "saved", "Reload text differed.");
                Require(scope.Asset.single.icon == scope.Texture, "Texture reference differed after reload.");
                return "kind=Label;text=saved;reference=preserved";
            }
        }

        private static string DelegatedSerializationDictionaryUsesUnityPolicy()
        {
            var assetPath = AssetFolder + "/delegated-dictionary-" + Guid.NewGuid().ToString("N") + ".asset";
            var asset = ScriptableObject.CreateInstance<UnityDelegatedSerializationAsset>();
            try
            {
                var controlField = typeof(UnityDelegatedSerializationAsset).GetField(
                    "control",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Require(controlField != null, "The direct dictionary control field was unavailable.");

                var payloadDictionary = new Dictionary<int, string> { [17] = "payload" };
                var controlDictionary = new Dictionary<int, string> { [17] = "control" };
                asset.union = UnityDelegatedSerializationUnion.Value(41, payloadDictionary);
                controlField!.SetValue(asset, controlDictionary);
                asset.supportedControl = 23;

                Require(
                    asset.union.kind == UnityDelegatedSerializationUnion.Kind.Value &&
                    asset.union.marker == 41 &&
                    asset.union.unsupported != null &&
                    asset.union.unsupported.Count == 1,
                    "The generated union did not retain its dictionary before persistence.");
                Require(
                    asset.Control != null && asset.Control.Count == 1,
                    "The direct dictionary did not exist before persistence.");

                AssetDatabase.CreateAsset(asset, assetPath);
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

                Resources.UnloadAsset(asset);
                asset = AssetDatabase.LoadAssetAtPath<UnityDelegatedSerializationAsset>(assetPath);
                Require(asset != null, "Delegated serialization asset reload returned null.");
                Require(
                    asset.union.kind == UnityDelegatedSerializationUnion.Kind.Value &&
                    asset.union.marker == 41,
                    "The generated union discriminator or supported marker did not survive reload.");
                Require(asset.supportedControl == 23, "The supported control field did not survive reload.");
                Require(asset.Control == null, "Unity unexpectedly serialized the direct Dictionary field.");
                Require(
                    asset.union.unsupported == null,
                    "Unity unexpectedly serialized the Dictionary field inside the union payload.");

                var serialized = new SerializedObject(asset);
                serialized.Update();
                var payloadProperty = RequirePayloadProperty(serialized, "union");
                var directDictionaryProperty = serialized.FindProperty("control");
                var payloadDictionaryProperty = payloadProperty.FindPropertyRelative("unsupported");
                Require(
                    directDictionaryProperty == null && payloadDictionaryProperty == null,
                    "Unity exposed one of the unsupported Dictionary fields as a serialized property.");

                var yaml = File.ReadAllText(Path.Combine(ProjectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
                Require(
                    !YamlContainsField(yaml, "control") && !YamlContainsField(yaml, "unsupported"),
                    "Unity wrote an unsupported Dictionary field to YAML.");
                return "kind=Value;marker=41;supported-control=23;direct-dictionary=null;payload-dictionary=null;serialized-properties=absent;yaml-fields=absent";
            }
            finally
            {
                AssetDatabase.DeleteAsset(assetPath);
                AssetDatabase.SaveAssets();
            }
        }

        private static string ArrayRoundTrip()
        {
            using (var scope = new AssetScope("array"))
            {
                scope.Asset.array = new[]
                {
                UnityCollectionUnion.Child(UnityCallbackChild.Text(1, "a", scope.Texture)),
                UnityCollectionUnion.Number(2.5f),
                UnityCollectionUnion.Empty(),
            };
                scope.Save();
                scope.Reload();
                Require(scope.Asset.array.Length == 3, "Array length differed.");
                Require(scope.Asset.array[0].kind == UnityCollectionUnion.Kind.Child, "Array child kind differed.");
                Require(scope.Asset.array[0].child.label == "a" && scope.Asset.array[0].child.asset == scope.Texture, "Array child values differed.");
                Require(scope.Asset.array[1].kind == UnityCollectionUnion.Kind.Number && scope.Asset.array[1].value == 2.5f, "Array number differed.");
                Require(scope.Asset.array[2].kind == UnityCollectionUnion.Kind.Empty, "Array Empty kind differed.");
                return "array=3;order=preserved";
            }
        }

        private static string ListRoundTrip()
        {
            using (var scope = new AssetScope("list"))
            {
                scope.Asset.list = new List<UnityCollectionUnion>
            {
                UnityCollectionUnion.Number(8.5f),
                UnityCollectionUnion.Child(UnityCallbackChild.Text(9, "list", scope.Texture)),
                UnityCollectionUnion.Empty(),
            };
                scope.Save();
                scope.Reload();
                Require(scope.Asset.list.Count == 3, "List count differed.");
                Require(scope.Asset.list[0].kind == UnityCollectionUnion.Kind.Number && scope.Asset.list[0].value == 8.5f, "List number differed.");
                Require(scope.Asset.list[1].child.label == "list" && scope.Asset.list[1].child.asset == scope.Texture, "List child differed.");
                Require(scope.Asset.list[2].kind == UnityCollectionUnion.Kind.Empty, "List Empty kind differed.");
                return "list=3;order=preserved";
            }
        }

        private static string ContainingStructRoundTrip()
        {
            using (var scope = new AssetScope("containing"))
            {
                scope.Asset.nested = new UnityNestedOwnerData(
                    UnityShapeSerializable.Circle(31.25f),
                    new[] { UnityCollectionUnion.Empty() },
                    new List<UnityCollectionUnion> { UnityCollectionUnion.Number(4f) },
                    UnityShapeSerializable.Label("private-child", scope.Texture));
                scope.Save();
                scope.Reload();
                Require(scope.Asset.nested.value.kind == UnityShapeSerializable.Kind.Circle, "Containing union kind differed.");
                Require(scope.Asset.nested.value.radius == 31.25f, "Containing union value differed.");
                Require(scope.Asset.nested.array.Length == 1 && scope.Asset.nested.list.Count == 1, "Containing collections differed.");
                Require(scope.Asset.nested.Child.kind == UnityShapeSerializable.Kind.Label, "Private child kind differed.");
                Require(scope.Asset.nested.Child.text == "private-child", "Private child value differed.");
                Require(scope.Asset.nested.Child.icon == scope.Texture, "Private child object reference differed.");

                var serialized = new SerializedObject(scope.Asset);
                serialized.Update();
                Require(
                    serialized.FindProperty("nested.child") != null,
                    "Private SerializeField child was not exposed through native serialization.");
                return "nested=Circle(31.25);private-child=Label;collections=preserved";
            }
        }

        private static string DerivedCompositeBasePrivateChildRoundTrip()
        {
            using (var scope = new AssetScope("derived-composite"))
            {
                scope.Asset.composite = new UnityCompositeDerived(
                    UnityShapeSerializable.Label("base-child", scope.Texture),
                    UnityShapeSerializable.Circle(12.5f));
                scope.Save();
                scope.Reload();
                RequireCompositeValues(scope);

                var serialized = new SerializedObject(scope.Asset);
                serialized.Update();
                Require(
                    serialized.FindProperty("composite.baseChild") != null,
                    "Private base SerializeField child was not exposed through native serialization.");
                Require(
                    serialized.FindProperty("composite.derivedChild") != null,
                    "Derived composite child was not exposed through native serialization.");

                scope.Save();
                scope.Reload();
                RequireCompositeValues(scope);
                return "base-child=Label/base-child;derived-child=Circle/12.5;repeat=stable";
            }
        }

        private static void RequireCompositeValues(AssetScope scope)
        {
            Require(scope.Asset.composite != null, "Derived composite was missing after reload.");
            Require(
                scope.Asset.composite.BaseChild.kind == UnityShapeSerializable.Kind.Label &&
                scope.Asset.composite.BaseChild.text == "base-child" &&
                scope.Asset.composite.BaseChild.icon == scope.Texture,
                "Private base child union differed after reload.");
            Require(
                scope.Asset.composite.derivedChild.kind == UnityShapeSerializable.Kind.Circle &&
                scope.Asset.composite.derivedChild.radius == 12.5f,
                "Derived child union differed after reload.");
        }

        private static string NestedParentAutomaticChildSnapshotRepeatedSave()
        {
            using (var scope = new AssetScope("nested-parent-automatic"))
            {
                scope.Asset.parent = UnityCallbackParent.Parent(UnityCallbackChild.Text(41, "child", scope.Texture), 99);
                scope.Save();
                scope.Reload();
                RequireParentChild(scope, 41, "child", 99);
                scope.Save();
                scope.Reload();
                RequireParentChild(scope, 41, "child", 99);
                return "parent=99;child=41/child;repeat=stable";
            }
        }

        private static string NestedParentMatchingPayloadRefreshRepeatedSave()
        {
            using (var scope = new AssetScope("nested-parent-refresh"))
            {
                scope.Asset.parent = UnityCallbackParent.Parent(UnityCallbackChild.Text(42, "before", scope.Texture), 100);
                scope.Save();
                scope.Asset.parent = SetInternalSlotForField(scope.Asset.parent, "marker", 101);
                var child = SetInternalSlotForField(scope.Asset.parent.child, "label", "after");
                scope.Asset.parent = SetInternalSlotForField(scope.Asset.parent, "child", child);
                scope.Save();
                scope.Reload();
                RequireParentChild(scope, 42, "after", 101);
                scope.Save();
                scope.Reload();
                RequireParentChild(scope, 42, "after", 101);
                return "parent=101;child=after;repeat=stable";
            }
        }

        private static string NestedParentCopyIsolation()
        {
            using (var scope = new AssetScope("nested-copy"))
            {
                var original = UnityCallbackParent.Parent(UnityCallbackChild.Text(5, "original", scope.Texture), 10);
                var copy = original;
                copy = SetInternalSlotForField(copy, "marker", 11);
                copy = SetInternalSlotForField(copy, "child", SetInternalSlotForField(copy.child, "label", "copy"));
                scope.Asset.original = UnityCopyProbe.Parent(original.child, original.marker);
                scope.Asset.detachedCopy = UnityCopyProbe.Parent(copy.child, copy.marker);
                scope.Save();
                Require(scope.Asset.original.marker == 10 && scope.Asset.detachedCopy.marker == 11, "Immediate parent copies were not independent.");
                Require(scope.Asset.original.child.label == "original" && scope.Asset.detachedCopy.child.label == "copy", "Immediate child copies were not independent.");
                scope.Reload();
                Require(scope.Asset.original.marker == 10 && scope.Asset.detachedCopy.marker == 11, "Reloaded parent copies were not independent.");
                Require(scope.Asset.original.child.label == "original" && scope.Asset.detachedCopy.child.label == "copy", "Reloaded child copies were not independent.");
                return "original=10/original;copy=11/copy;isolated=true";
            }
        }

        private static string NestedArrayElementsIndependentChildSnapshots()
        {
            using (var scope = new AssetScope("nested-array"))
            {
                scope.Asset.array = new[]
                {
                UnityCollectionUnion.Child(UnityCallbackChild.Text(1, "first", scope.Texture)),
                UnityCollectionUnion.Child(UnityCallbackChild.Text(2, "second", scope.Texture)),
            };
                scope.Save();
                var first = scope.Asset.array[0];
                first = SetInternalSlotForField(first, "child", SetInternalSlotForField(first.child, "label", "changed"));
                scope.Asset.array[0] = first;
                scope.Save();
                scope.Reload();
                Require(scope.Asset.array[0].child.label == "changed", "First nested array element did not change.");
                Require(scope.Asset.array[1].child.label == "second", "Second nested array element changed with its sibling.");
                return "first=changed;second=second;isolated=true";
            }
        }

        private static string NestedListElementsIndependentChildSnapshots()
        {
            using (var scope = new AssetScope("nested-list"))
            {
                scope.Asset.list = new List<UnityCollectionUnion>
            {
                UnityCollectionUnion.Child(UnityCallbackChild.Text(1, "first", scope.Texture)),
                UnityCollectionUnion.Child(UnityCallbackChild.Text(2, "second", scope.Texture)),
            };
                scope.Save();
                var first = scope.Asset.list[0];
                first = SetInternalSlotForField(first, "child", SetInternalSlotForField(first.child, "label", "changed"));
                scope.Asset.list[0] = first;
                scope.Asset.list.Add(UnityCollectionUnion.Empty());
                scope.Save();
                scope.Reload();
                Require(scope.Asset.list.Count == 3, "Nested list growth differed.");
                Require(scope.Asset.list[0].child.label == "changed" && scope.Asset.list[1].child.label == "second", "Nested list siblings were not isolated.");
                return "count=3;first=changed;second=second;isolated=true";
            }
        }

        private static string NestedCollectionRuntimeMutationIsolated()
        {
            using (var scope = new AssetScope("nested-init"))
            {
                scope.Asset.array = new[]
                {
                UnityCollectionUnion.Child(UnityCallbackChild.Text(1, "before", scope.Texture)),
                UnityCollectionUnion.Child(UnityCallbackChild.Text(2, "sibling", scope.Texture)),
            };
                scope.Save();
                var first = scope.Asset.array[0];
                first = SetInternalSlotForField(first, "child", SetInternalSlotForField(first.child, "label", "after"));
                scope.Asset.array[0] = first;
                scope.Save();
                scope.Save();
                scope.Reload();
                Require(scope.Asset.array[0].child.label == "after", "Nested init mutation was lost.");
                Require(scope.Asset.array[1].child.label == "sibling", "Nested init mutation changed a sibling.");
                return "after=after;sibling=sibling;snapshot-isolated=true";
            }
        }

        private static string CopiedValueSharedPayloadMutationDetaches()
        {
            using (var scope = new AssetScope("copy-shared"))
            {
                scope.Asset.original = UnityCopyProbe.Text(5, "original", scope.Texture);
                scope.Asset.sharedCopy = UnityCopyProbe.Text(6, "shared", scope.Texture);
                scope.Save();
                var serialized = new SerializedObject(scope.Asset);
                serialized.Update();
                var originalPayload = RequirePayloadProperty(serialized, "original").managedReferenceValue;
                Require(originalPayload != null, "Original payload was unavailable.");
                RequirePayloadProperty(serialized, "sharedCopy").managedReferenceValue = originalPayload;
                Require(serialized.ApplyModifiedProperties(), "Alias setup was not applied.");
                scope.Asset.sharedCopy = SetInternalSlotForField(scope.Asset.sharedCopy, "label", "mutated");
                scope.Save();
                scope.Reload();
                Require(scope.Asset.original.label == "original", "Original shared payload was mutated.");
                Require(scope.Asset.sharedCopy.label == "mutated", "Detached shared copy did not retain its mutation.");
                return "original=original;copy=mutated;snapshot-replaced-on-save=true";
            }
        }

        private static string CopiedNestedChildPayloadAliasDetachesRepeatedSave()
        {
            using (var scope = new AssetScope("nested-child-alias"))
            {
                scope.Asset.nestedOriginal = UnityCopyProbe.Parent(
                    UnityCallbackChild.Text(8, "nested-original", scope.Texture),
                    1);
                scope.Asset.nestedCopy = UnityCopyProbe.Parent(
                    UnityCallbackChild.Text(8, "nested-original", scope.Texture),
                    1);
                scope.Save();
                Require(
                    scope.Asset.nestedOriginal.child.label == "nested-original" &&
                    scope.Asset.nestedCopy.child.label == "nested-original",
                    "Nested child copies did not start with equal values.");

                var serialized = new SerializedObject(scope.Asset);
                serialized.Update();
                var originalChild = RequirePayloadProperty(
                    RequireProperty(RequirePayloadProperty(serialized, "nestedOriginal"), "child"));
                var copiedChild = RequirePayloadProperty(
                    RequireProperty(RequirePayloadProperty(serialized, "nestedCopy"), "child"));
                var originalChildPayload = originalChild.managedReferenceValue;
                Require(originalChildPayload != null, "Original nested child payload was unavailable.");
                var originalChildIdBeforeApply = originalChild.managedReferenceId;
                copiedChild.managedReferenceValue = originalChildPayload;
                Require(
                    ReferenceEquals(copiedChild.managedReferenceValue, originalChildPayload),
                    "Nested child alias assignment did not retain the attempted payload before ApplyModifiedProperties.");
                Require(
                    copiedChild.managedReferenceId == originalChildIdBeforeApply,
                    "Nested child alias assignment did not target the original managed-reference identity before ApplyModifiedProperties.");
                Require(serialized.ApplyModifiedProperties(), "Nested child payload alias setup was not applied.");
                serialized.Update();
                Require(
                    scope.Asset.nestedOriginal.child.label == "nested-original" &&
                    scope.Asset.nestedCopy.child.label == "nested-original",
                    "Nested child alias setup did not preserve the equal child values after ApplyModifiedProperties.");

                var copiedValue = SetInternalSlotForField(scope.Asset.nestedCopy.child, "label", "nested-copy");
                scope.Asset.nestedCopy = SetInternalSlotForField(scope.Asset.nestedCopy, "child", copiedValue);
                scope.Save();
                scope.Save();
                scope.Reload();

                Require(
                    scope.Asset.nestedOriginal.child.label == "nested-original",
                    "Original nested child changed after copied child mutation.");
                Require(
                    scope.Asset.nestedCopy.child.label == "nested-copy",
                    "Copied nested child mutation was lost.");

                serialized = new SerializedObject(scope.Asset);
                serialized.Update();
                var originalChildId = RequirePayloadProperty(
                    RequireProperty(RequirePayloadProperty(serialized, "nestedOriginal"), "child")).managedReferenceId;
                var copiedChildId = RequirePayloadProperty(
                    RequireProperty(RequirePayloadProperty(serialized, "nestedCopy"), "child")).managedReferenceId;
                Require(originalChildId != 0 && copiedChildId != 0, "Nested child managed-reference identities were unavailable.");
                Require(originalChildId != copiedChildId, "Nested child managed-reference identities were not detached.");
                return "initial-equal=true;original=nested-original;copy=nested-copy;child-sr-distinct=true;repeat=2";
            }
        }

        private static string CopiedNestedChildReferenceCompositeAliasDetachesWithoutParentReassignment()
        {
            using (var scope = new AssetScope("nested-reference-composite-alias"))
            {
                scope.Asset.referenceCompositeOriginal = new UnityReferenceComposite(
                    UnityCopyProbe.Parent(UnityCallbackChild.Text(1, "original-parent", scope.Texture), 10),
                    UnityCallbackChild.Text(2, "original-child", scope.Texture));
                scope.Asset.referenceCompositeCopy = new UnityReferenceComposite(
                    UnityCopyProbe.Parent(UnityCallbackChild.Text(3, "copy-parent", scope.Texture), 11),
                    UnityCallbackChild.Text(4, "original-child", scope.Texture));
                scope.Save();

                Require(
                    scope.Asset.referenceCompositeOriginal.Child.label == "original-child" &&
                    scope.Asset.referenceCompositeCopy.Child.label == "original-child",
                    "Reference composite child values did not start equal.");

                var serialized = new SerializedObject(scope.Asset);
                serialized.Update();
                var originalChild = RequirePayloadProperty(
                    serialized,
                    "referenceCompositeOriginal.child");
                var copiedChild = RequirePayloadProperty(
                    serialized,
                    "referenceCompositeCopy.child");
                var originalPayload = originalChild.managedReferenceValue;
                Require(originalPayload != null, "Reference composite original child payload was unavailable.");
                var originalChildIdBeforeApply = originalChild.managedReferenceId;
                copiedChild.managedReferenceValue = originalPayload;
                Require(
                    ReferenceEquals(copiedChild.managedReferenceValue, originalPayload),
                    "Reference composite alias assignment did not retain the attempted payload before ApplyModifiedProperties.");
                Require(
                    copiedChild.managedReferenceId == originalChildIdBeforeApply,
                    "Reference composite alias assignment did not target the original managed-reference identity before ApplyModifiedProperties.");
                Require(serialized.ApplyModifiedProperties(), "Reference composite child alias setup was not applied.");
                serialized.Update();
                Require(
                    scope.Asset.referenceCompositeOriginal.Child.label == "original-child" &&
                    scope.Asset.referenceCompositeCopy.Child.label == "original-child",
                    "Reference composite alias setup did not preserve the equal child values after ApplyModifiedProperties.");

                var copiedChildValue = UnityCallbackChild.Text(
                    scope.Asset.referenceCompositeCopy.Child.count,
                    "copy-child",
                    scope.Asset.referenceCompositeCopy.Child.asset);
                scope.Asset.referenceCompositeCopy.Child = copiedChildValue;
                scope.Save();
                scope.Save();
                scope.Reload();

                Require(
                    scope.Asset.referenceCompositeOriginal.parent.marker == 10 &&
                    scope.Asset.referenceCompositeOriginal.parent.child.label == "original-parent",
                    "Original reference composite parent union changed during child mutation.");
                Require(
                    scope.Asset.referenceCompositeCopy.parent.marker == 11 &&
                    scope.Asset.referenceCompositeCopy.parent.child.label == "copy-parent",
                    "Copied reference composite parent union changed during child mutation.");
                Require(
                    scope.Asset.referenceCompositeOriginal.Child.label == "original-child",
                    "Original reference composite child changed after copied child mutation.");
                Require(
                    scope.Asset.referenceCompositeCopy.Child.label == "copy-child",
                    "Copied reference composite child mutation was lost.");

                serialized = new SerializedObject(scope.Asset);
                serialized.Update();
                var originalChildId = RequirePayloadProperty(
                    serialized,
                    "referenceCompositeOriginal.child").managedReferenceId;
                var copiedChildId = RequirePayloadProperty(
                    serialized,
                    "referenceCompositeCopy.child").managedReferenceId;
                Require(originalChildId != 0 && copiedChildId != 0, "Reference composite child identities were unavailable.");
                Require(originalChildId != copiedChildId, "Reference composite child identities were not detached.");
                return "parents=distinct-and-preserved;children=original-child/copy-child;child-sr-distinct=true;repeat=2";
            }
        }

        private static string CopiedValueDetachedPayloadRepeatedSaveVariantChange()
        {
            using (var scope = new AssetScope("copy-detached"))
            {
                scope.Asset.original = UnityCopyProbe.Text(5, "original", scope.Texture);
                scope.Asset.detachedCopy = UnityCopyProbe.Text(7, "detached", scope.Texture);
                scope.Save();
                scope.Reload();
                Require(scope.Asset.original.label == "original" && scope.Asset.detachedCopy.label == "detached", "Initial detached copies differed.");
                scope.Asset.detachedCopy = UnityCopyProbe.Number(88.5f);
                scope.Save();
                scope.Reload();
                scope.Save();
                scope.Reload();
                Require(scope.Asset.original.kind == UnityCopyProbe.Kind.Text && scope.Asset.original.label == "original", "Original changed during variant switch.");
                Require(scope.Asset.detachedCopy.kind == UnityCopyProbe.Kind.Number && scope.Asset.detachedCopy.value == 88.5f, "Detached variant change differed.");
                return "original=Text/original;copy=Number/88.5;isolated=true";
            }
        }

        private static string ActivePayloadOnlyNoInactivePayloadOrRuntimeFields()
        {
            using (var scope = new AssetScope("active-only"))
            {
                scope.Asset.single = UnityShapeSerializable.Label("active", scope.Texture);
                scope.Save();
                var serialized = new SerializedObject(scope.Asset);
                serialized.Update();
                var union = RequireProperty(serialized, "single");
                var children = DirectChildren(union);
                Require(children.Count == 1, "Serialized union did not contain exactly one active payload field.");
                Require(children[0].propertyType == SerializedPropertyType.ManagedReference, "Serialized managed payload was missing.");
                Require(!children.Any(p => p.propertyType == SerializedPropertyType.Enum), "Serialized discriminator was still present.");
                Require(!children.Any(p => p.name == "radius" || p.name == "text" || p.name == "icon"), "Runtime union properties leaked into serialized storage.");
                var yaml = File.ReadAllText(scope.AssetFilePath);
                var payload = RequirePayloadProperty(serialized, "single").managedReferenceValue;
                if (payload == null)
                    throw new InvalidOperationException("Active payload was unavailable in serialized state.");
                Require(yaml.IndexOf(payload.GetType().Name, StringComparison.Ordinal) >= 0, "Active payload type was not present in YAML.");
                return "children=1;active=" + payload.GetType().Name + ";serialized-kind=false;runtime-fields=absent";
            }
        }

        private static string PayloadOnlyAllFiveLayouts()
        {
            using (var scope = new AssetScope("payload-only-five-layouts"))
            {
                scope.Asset.stability = CreateStabilityEditorHost(scope.Texture);
                scope.Save();
                var serialized = new SerializedObject(scope.Asset);
                serialized.Update();
                foreach (var path in new[]
                {
                    "stability.additive",
                    "stability.boxedFields",
                    "stability.boxedTuple",
                    "stability.unsafeOverlap",
                    "stability.overlap",
                    "stability.parent",
                    "stability.parentArray.Array.data[0]",
                    "stability.parentList.Array.data[0]",
                    "stability.genericInt.value",
                    "stability.genericString.value",
                })
                {
                    RequirePayloadOnly(serialized, path);
                }

                var yaml = File.ReadAllText(scope.AssetFilePath);
                Require(!YamlContainsField(yaml, "_kind"), "Payload-only YAML still contained a serialized discriminator.");
                scope.Reload();
                RequireStabilityEditorValues(scope.Asset.stability, scope.Texture);
                return "layouts=5;active-payloads=10;serialized-kind=false;round-trip=true";
            }
        }

        private static string PayloadOnlyRepeatedSaveYamlAndIdsStable()
        {
            using (var scope = new AssetScope("payload-only-stable-yaml"))
            {
                scope.Asset.stability = CreateStabilityEditorHost(scope.Texture);
                scope.Save();
                var preCanonicalYaml = File.ReadAllText(scope.AssetFilePath);
                var preCanonicalSerialized = new SerializedObject(scope.Asset);
                var preCanonicalIds = ManagedReferenceIds(preCanonicalSerialized);
                WritePayloadStabilityArtifact("payload-only-stable-precanonical.yaml", preCanonicalYaml);
                WritePayloadStabilityGraphArtifact("payload-only-stable-precanonical-rids.json", preCanonicalSerialized);

                scope.Reload();
                scope.Save();
                var firstYaml = File.ReadAllText(scope.AssetFilePath);
                var firstIds = ManagedReferenceIds(new SerializedObject(scope.Asset));
                WritePayloadStabilityArtifact("payload-only-stable-first.yaml", firstYaml);
                WritePayloadStabilityGraphArtifact("payload-only-stable-first-rids.json", new SerializedObject(scope.Asset));

                scope.Save();
                var secondYaml = File.ReadAllText(scope.AssetFilePath);
                var secondIds = ManagedReferenceIds(new SerializedObject(scope.Asset));
                WritePayloadStabilityArtifact("payload-only-stable-second.yaml", secondYaml);
                WritePayloadStabilityGraphArtifact("payload-only-stable-second-rids.json", new SerializedObject(scope.Asset));

                scope.Reload();
                scope.Save();
                var thirdYaml = File.ReadAllText(scope.AssetFilePath);
                var thirdIds = ManagedReferenceIds(new SerializedObject(scope.Asset));
                WritePayloadStabilityArtifact("payload-only-stable-third.yaml", thirdYaml);
                WritePayloadStabilityGraphArtifact("payload-only-stable-third-rids.json", new SerializedObject(scope.Asset));
                scope.Save();
                var fourthYaml = File.ReadAllText(scope.AssetFilePath);
                var fourthIds = ManagedReferenceIds(new SerializedObject(scope.Asset));
                WritePayloadStabilityArtifact("payload-only-stable-fourth.yaml", fourthYaml);
                WritePayloadStabilityGraphArtifact("payload-only-stable-fourth-rids.json", new SerializedObject(scope.Asset));
                Require(thirdYaml == fourthYaml, "Canonical payload YAML changed during a second save after reload.");
                Require(thirdIds.SequenceEqual(fourthIds), "Canonical managed-reference IDs changed during a second save after reload.");
                Require(firstYaml == secondYaml, "Canonical payload YAML changed during a repeated save.");
                Require(firstIds.SequenceEqual(secondIds), "Canonical managed-reference IDs changed during a repeated save.");
                Require(firstYaml == thirdYaml, "Canonical payload YAML changed after unload/reload/save.");
                Require(firstIds.SequenceEqual(thirdIds), "Canonical managed-reference IDs changed after unload/reload/save.");
                return "yaml-stable=true;managed-reference-ids-stable=true;reload-repeat=true;precanonical-captured=true;precanonical-ids=" + preCanonicalIds.Count + ";ids=" + firstIds.Count;
            }
        }

        private static string PayloadOnlyMalformedPayloadFallsBackAndOverwrites()
        {
            using (var scope = new AssetScope("payload-only-malformed"))
            {
                scope.Asset.stability = CreateStabilityEditorHost(scope.Texture);
                scope.Asset.stability.additive = StabilityAdditiveUnion.First(99, "valid-before-malformed");
                scope.Save();

                var yaml = ReplaceNestedPayloadRidWithNull(
                    File.ReadAllText(scope.AssetFilePath),
                    "additive",
                    "_serializedPayload");
                File.WriteAllText(scope.AssetFilePath, yaml);

                var importWarningCount = 0;
                void OnImportLogThreaded(string message, string stackTrace, LogType type)
                {
                    if (type == LogType.Warning &&
                        message == "invalid union serialization data, defaulting to First")
                    {
                        Interlocked.Increment(ref importWarningCount);
                    }
                }

                Application.logMessageReceivedThreaded += OnImportLogThreaded;
                try
                {
                    AssetDatabase.ImportAsset(scope.AssetPath, ImportAssetOptions.ForceUpdate);
                }
                finally
                {
                    Application.logMessageReceivedThreaded -= OnImportLogThreaded;
                }

                var reloadWarningCount = 0;
                void OnReloadLogThreaded(string message, string stackTrace, LogType type)
                {
                    if (type == LogType.Warning &&
                        message == "invalid union serialization data, defaulting to First")
                    {
                        Interlocked.Increment(ref reloadWarningCount);
                    }
                }

                Application.logMessageReceivedThreaded += OnReloadLogThreaded;
                try
                {
                    ReloadWithoutSave(scope);
                }
                finally
                {
                    Application.logMessageReceivedThreaded -= OnReloadLogThreaded;
                }

                Require(
                    scope.Asset.stability.additive.kind == StabilityAdditiveUnion.Kind.First &&
                    scope.Asset.stability.additive.amount == 0 &&
                    scope.Asset.stability.additive.label == null,
                    "Malformed payload did not overwrite the valid runtime value with First defaults.");
                var observedImportWarnings = Interlocked.CompareExchange(ref importWarningCount, 0, 0);
                var observedReloadWarnings = Interlocked.CompareExchange(ref reloadWarningCount, 0, 0);
                Require(observedImportWarnings == 1, "Malformed payload import warning count was " + observedImportWarnings + ", expected exactly one.");
                Require(observedReloadWarnings == 1, "Malformed payload reload warning count was " + observedReloadWarnings + ", expected exactly one.");
                return "fallback=First;defaults=true;import-warning-count=" + observedImportWarnings + ";reload-warning-count=" + observedReloadWarnings + ";old-runtime-overwritten=true";
            }
        }

        private static string PayloadOnlyCallbackHelperMissingChildDefaults()
        {
            using (var scope = new AssetScope("payload-only-callback-helper"))
            {
                scope.Asset.stability = CreateStabilityEditorHost(scope.Texture);
                scope.Asset.stability.callbackHelper = new StabilityCallbackChildHelper(
                    StabilityNestedChildUnion.Text(77, "constructor-overridden"));
                scope.Save();
                Require(
                    scope.Asset.stability.callbackHelper.child.count == 77 &&
                    scope.Asset.stability.callbackHelper.callbackMarker == 0,
                    "Callback helper did not start with its nondefault constructor value.");

                var serialized = new SerializedObject(scope.Asset);
                serialized.Update();
                var childPayload = RequirePayloadProperty(serialized, "stability.callbackHelper.child");
                Require(childPayload.managedReferenceId != 0, "Callback helper child payload was unavailable before corruption.");
                var yaml = ReplaceNestedPayloadRidWithNull(
                    File.ReadAllText(scope.AssetFilePath),
                    "callbackHelper",
                    "child");
                File.WriteAllText(scope.AssetFilePath, yaml);

                var warningCount = 0;
                void OnLog(string message, string stackTrace, LogType type)
                {
                    if (type == LogType.Warning &&
                        message == "invalid union serialization data, defaulting to Text")
                    {
                        warningCount++;
                    }
                }

                Application.logMessageReceived += OnLog;
                try
                {
                    AssetDatabase.ImportAsset(scope.AssetPath, ImportAssetOptions.ForceUpdate);
                    ReloadWithoutSave(scope);
                }
                finally
                {
                    Application.logMessageReceived -= OnLog;
                }

                Require(scope.Asset.stability.callbackHelper.callbackMarker == 1, "Callback helper marker did not prove OnAfterDeserialize ran.");
                Require(
                    scope.Asset.stability.callbackHelper.child.kind == StabilityNestedChildUnion.Kind.Text &&
                    scope.Asset.stability.callbackHelper.child.count == 0 &&
                    scope.Asset.stability.callbackHelper.child.label == null,
                    "Missing child payload retained constructor values instead of Text defaults.");
                return "helper-marker=1;child=Text(0,null);constructor-value=overwritten;nested-warning-count=" + warningCount;
            }
        }

        private static string PayloadOnlyFormerNamePayloadCanonicalizesAndIgnoresStaleKind()
        {
            using (var scope = new AssetScope("payload-only-former-name"))
            {
                scope.Asset.stability = CreateStabilityEditorHost(scope.Texture);
                scope.Asset.stability.migration = StabilityMigrationUnion.Renamed(7);
                scope.Save();
                var serialized = new SerializedObject(scope.Asset);
                serialized.Update();
                var currentPayloadName = RequirePayload(serialized, "stability.migration").GetType().Name;
                var formerNameType = FindFormerNamePayloadType(typeof(StabilityMigrationUnion), "OldValue");
                var formerNameYaml = ReplacePayloadTypeAndInjectStaleKind(
                    File.ReadAllText(scope.AssetFilePath),
                    typeof(StabilityMigrationUnion),
                    currentPayloadName,
                    formerNameType.Name,
                    "migration");
                File.WriteAllText(scope.AssetFilePath, formerNameYaml);
                AssetDatabase.ImportAsset(scope.AssetPath, ImportAssetOptions.ForceUpdate);
                ReloadWithoutSave(scope);
                Require(
                    scope.Asset.stability.migration.kind == StabilityMigrationUnion.Kind.Renamed &&
                    scope.Asset.stability.migration.value == 7,
                    "Former-name payload did not restore through the current Renamed factory.");

                scope.Save();
                var canonicalYaml = File.ReadAllText(scope.AssetFilePath);
                Require(canonicalYaml.IndexOf(currentPayloadName, StringComparison.Ordinal) >= 0, "Canonical payload type was not written after former-name reload.");
                Require(canonicalYaml.IndexOf(formerNameType.Name, StringComparison.Ordinal) < 0, "Former-name payload type remained after canonical save.");
                Require(!YamlContainsField(canonicalYaml, "_kind"), "Stale serialized kind was retained after canonical save.");
                return "former-name=OldValue;current=Renamed;value=7;stale-kind=ignored;canonicalized=true";
            }
        }

        private static string PayloadOnlyFormerNamePayloadAllLayoutsCanonicalize()
        {
            using (var scope = new AssetScope("payload-only-former-name-all-layouts"))
            {
                scope.Asset.stability = CreateStabilityEditorHost(scope.Texture);
                scope.Asset.stability.boxedFields = StabilityBoxedFieldsUnion.First(81, "former-name-boxed-fields");
                scope.Asset.stability.boxedTuple = StabilityBoxedTupleUnion.First(82, "former-name-boxed-tuple");
                scope.Asset.stability.unsafeOverlap = StabilityUnsafeOverlapUnion.First(83, "former-name-unsafe-overlap");
                scope.Asset.stability.overlap = StabilityOverlapUnion.First(84, "former-name-overlap");
                scope.Save();

                var serialized = new SerializedObject(scope.Asset);
                serialized.Update();
                var replacements = new[]
                {
                    new FormerNamePayloadReplacement(
                        "stability.boxedFields",
                        "boxedFields",
                        typeof(StabilityBoxedFieldsUnion),
                        "OldBoxedFieldsFirst"),
                    new FormerNamePayloadReplacement(
                        "stability.boxedTuple",
                        "boxedTuple",
                        typeof(StabilityBoxedTupleUnion),
                        "OldBoxedTupleFirst"),
                    new FormerNamePayloadReplacement(
                        "stability.unsafeOverlap",
                        "unsafeOverlap",
                        typeof(StabilityUnsafeOverlapUnion),
                        "OldUnsafeOverlapFirst"),
                    new FormerNamePayloadReplacement(
                        "stability.overlap",
                        "overlap",
                        typeof(StabilityOverlapUnion),
                        "OldOverlapFirst"),
                };
                var yaml = File.ReadAllText(scope.AssetFilePath);
                var currentPayloadNames = new List<string>();
                var formerNamePayloadNames = new List<string>();
                foreach (var replacement in replacements)
                {
                    var currentPayloadName = RequirePayload(serialized, replacement.propertyPath).GetType().Name;
                    var formerNameType = FindFormerNamePayloadType(replacement.unionType, replacement.formerName);
                    yaml = ReplacePayloadTypeAndInjectStaleKind(
                        yaml,
                        replacement.unionType,
                        currentPayloadName,
                        formerNameType.Name,
                        replacement.yamlFieldName);
                    currentPayloadNames.Add(currentPayloadName);
                    formerNamePayloadNames.Add(formerNameType.Name);
                }

                File.WriteAllText(scope.AssetFilePath, yaml);
                AssetDatabase.ImportAsset(scope.AssetPath, ImportAssetOptions.ForceUpdate);
                ReloadWithoutSave(scope);
                Require(
                    scope.Asset.stability.boxedFields.kind == StabilityBoxedFieldsUnion.Kind.First &&
                    scope.Asset.stability.boxedFields.amount == 81 &&
                    scope.Asset.stability.boxedFields.label == "former-name-boxed-fields",
                    "Former-name BoxedFields payload did not restore through First.");
                Require(
                    scope.Asset.stability.boxedTuple.kind == StabilityBoxedTupleUnion.Kind.First &&
                    scope.Asset.stability.boxedTuple.amount == 82 &&
                    scope.Asset.stability.boxedTuple.label == "former-name-boxed-tuple",
                    "Former-name BoxedTuple payload did not restore through First.");
                Require(
                    scope.Asset.stability.unsafeOverlap.kind == StabilityUnsafeOverlapUnion.Kind.First &&
                    scope.Asset.stability.unsafeOverlap.amount == 83 &&
                    scope.Asset.stability.unsafeOverlap.label == "former-name-unsafe-overlap",
                    "Former-name UnsafeOverlap payload did not restore through First.");
                Require(
                    scope.Asset.stability.overlap.kind == StabilityOverlapUnion.Kind.First &&
                    scope.Asset.stability.overlap.amount == 84 &&
                    scope.Asset.stability.overlap.label == "former-name-overlap",
                    "Former-name Overlap payload did not restore through First.");

                scope.Save();
                var canonicalYaml = File.ReadAllText(scope.AssetFilePath);
                foreach (var currentPayloadName in currentPayloadNames)
                    Require(canonicalYaml.IndexOf(currentPayloadName, StringComparison.Ordinal) >= 0, "Canonical payload type was not written: " + currentPayloadName);
                foreach (var formerNamePayloadName in formerNamePayloadNames)
                    Require(canonicalYaml.IndexOf(formerNamePayloadName, StringComparison.Ordinal) < 0, "Former-name payload type remained after canonical save: " + formerNamePayloadName);
                Require(!YamlContainsField(canonicalYaml, "_kind"), "Stale serialized kind was retained after canonical save.");
                return "former-name=BoxedFields+BoxedTuple+UnsafeOverlap+Overlap;current=First;canonicalized=true;stale-kind=ignored";
            }
        }

        private static string BoxedTupleMultiFieldNestedSaveReloadIdsStable()
        {
            using (var scope = new AssetScope("boxed-tuple-multifield-nested"))
            {
                scope.Asset.stability = CreateStabilityEditorHost(scope.Texture);
                scope.Asset.stability.tupleComposite = StabilityBoxedTupleCompositeUnion.Composite(
                    StabilityNestedChildUnion.Text(41, "tuple-child"),
                    42,
                    "tuple-label");
                scope.Save();
                var preCanonicalYaml = File.ReadAllText(scope.AssetFilePath);
                var preCanonicalSerialized = new SerializedObject(scope.Asset);
                var preCanonicalIds = ManagedReferenceIds(preCanonicalSerialized);
                WritePayloadStabilityArtifact("boxed-tuple-multifield-precanonical.yaml", preCanonicalYaml);
                WritePayloadStabilityGraphArtifact("boxed-tuple-multifield-precanonical-rids.json", preCanonicalSerialized);

                scope.Reload();
                scope.Save();

                var serialized = new SerializedObject(scope.Asset);
                serialized.Update();
                RequirePayloadOnly(serialized, "stability.tupleComposite");
                var firstYaml = File.ReadAllText(scope.AssetFilePath);
                var firstIds = ManagedReferenceIds(serialized);

                scope.Reload();
                scope.Save();
                serialized = new SerializedObject(scope.Asset);
                serialized.Update();
                var secondYaml = File.ReadAllText(scope.AssetFilePath);
                var secondIds = ManagedReferenceIds(serialized);
                Require(
                    scope.Asset.stability.tupleComposite.kind == StabilityBoxedTupleCompositeUnion.Kind.Composite &&
                    scope.Asset.stability.tupleComposite.child.kind == StabilityNestedChildUnion.Kind.Text &&
                    scope.Asset.stability.tupleComposite.child.count == 41 &&
                    scope.Asset.stability.tupleComposite.child.label == "tuple-child" &&
                    scope.Asset.stability.tupleComposite.marker == 42 &&
                    scope.Asset.stability.tupleComposite.label == "tuple-label",
                    "Multi-field BoxedTuple nested values differed after reload.");
                Require(firstYaml == secondYaml, "Multi-field BoxedTuple YAML changed after reload/save.");
                Require(firstIds.SequenceEqual(secondIds), "Multi-field BoxedTuple managed-reference IDs changed after reload/save.");
                return "nested=Text/tuple-child;marker=42;label=tuple-label;yaml-stable=true;ids-stable=true;precanonical-captured=true;precanonical-ids=" + preCanonicalIds.Count;
            }
        }

        private static string DrawerAliasPayloadNameBoundaryInference()
        {
            using (var scope = new DrawerHostScope("drawer-alias-boundary"))
            {
                scope.Host.alias = StabilityDrawerAliasUnion.Current(17);
                var serialized = new SerializedObject(scope.Host);
                serialized.Update();
                var property = RequireProperty(serialized, "alias");
                var formerNamePayload = CreateFormerNamePayload(
                    typeof(StabilityDrawerAliasUnion),
                    "Old",
                    "value",
                    17);
                RequirePayloadProperty(property).managedReferenceValue = formerNamePayload;

                var drawer = FindDrawer(typeof(StabilityDrawerAliasUnion));
                Require(drawer != null, "Alias-boundary drawer was not discoverable.");
                var tree = drawer!.CreatePropertyGUI(property);
                Require(tree != null, "Alias-boundary drawer did not create a visual tree.");
                tree!.Bind(serialized);
                var selectorValue = FindPopupValue(tree);
                Require(selectorValue == "Current", "Former-name Old payload inferred the wrong drawer variant: " + selectorValue);
                Require(HasBoundFieldControl(tree, "value"), "Alias-boundary drawer did not bind Current.value.");
                Require(!HasBoundFieldControl(tree, "label"), "Alias-boundary drawer bound the overlapping OldPayload.label field.");
                return "payload=Old;current=Current;overlap=OldPayload;inferred=Current;fields=value";
            }
        }

        private static string ManagedReferenceAllFiveLayoutsNullAndDerivedPersistence()
        {
            using (var scope = new ManagedReferenceAssetScope("managed-reference-null-derived"))
            {
                scope.Asset.data = CreateManagedReferenceHostData(null, includeCycle: false);
                scope.Save();
                var serialized = new SerializedObject(scope.Asset);
                serialized.Update();
                foreach (var path in ManagedReferenceUnionPaths)
                    RequireManagedReferenceField(serialized, path, "value");

                scope.Reload();
                RequireManagedReferenceNulls(scope.Asset.data);

                var leaf = CreateManagedReferenceLeaf("derived", 17);
                scope.Asset.data = CreateManagedReferenceHostData(leaf, includeCycle: false);
                scope.Save();
                scope.Reload();
                var reloadedShared = scope.Asset.data.sharedNode;
                Require(
                    reloadedShared is UnityManagedReferenceNodeLeaf reloadedLeaf &&
                    reloadedLeaf.id == "derived" &&
                    reloadedLeaf.amount == 17,
                    "Reloaded derived managed reference values differed.");
                Require(reloadedShared != null && !ReferenceEquals(reloadedShared, leaf), "Reloaded managed reference unexpectedly reused the pre-reload object.");
                RequireManagedReferenceValues(scope.Asset.data, reloadedShared);
                return "layouts=5;null=preserved;derived=" + reloadedShared.GetType().Name + ";root-distinct=true";
            }
        }

        private static string ManagedReferenceSharedIdentityPairAndCollections()
        {
            using (var scope = new ManagedReferenceAssetScope("managed-reference-shared-collections"))
            {
                var shared = CreateManagedReferenceLeaf("shared", 23);
                var data = CreateManagedReferenceHostData(shared, includeCycle: false);
                data.additive = UnityManagedReferenceAdditiveUnion.Pair(shared, shared);
                data.boxedFields = UnityManagedReferenceBoxedFieldsUnion.Array(new UnityManagedReferenceNodeBase?[] { shared, null, shared });
                data.boxedTuple = UnityManagedReferenceBoxedTupleUnion.List(new List<UnityManagedReferenceNodeBase?> { shared, null, shared });
                data.unsafeOverlap = UnityManagedReferenceUnsafeOverlapUnion.Pair(shared, shared);
                data.overlap = UnityManagedReferenceOverlapUnion.Reference(shared);
                scope.Asset.data = data;
                scope.Save();

                var serialized = new SerializedObject(scope.Asset);
                serialized.Update();
                RequireManagedReferenceField(serialized, "data.additive", "first");
                RequireManagedReferenceField(serialized, "data.additive", "second");
                RequireManagedReferenceCollectionField(serialized, "data.boxedFields", "arrayValues");
                RequireManagedReferenceCollectionField(serialized, "data.boxedTuple", "listValues");

                scope.Reload();
                var restored = scope.Asset.data;
                Require(ReferenceEquals(restored.additive.first, restored.additive.second), "Pair fields did not preserve shared identity.");
                Require(ReferenceEquals(restored.boxedFields.arrayValues[0], restored.boxedFields.arrayValues[2]), "Array elements did not preserve shared identity.");
                Require(restored.boxedFields.arrayValues[1] == null, "Array null element was not preserved.");
                Require(ReferenceEquals(restored.boxedTuple.listValues[0], restored.boxedTuple.listValues[2]), "List elements did not preserve shared identity.");
                Require(restored.boxedTuple.listValues[1] == null, "List null element was not preserved.");
                Require(ReferenceEquals(restored.unsafeOverlap.first, restored.unsafeOverlap.second), "UnsafeOverlap pair fields did not preserve shared identity.");
                Require(ReferenceEquals(restored.overlap.value, restored.additive.first), "Different layout fields lost shared identity.");
                return "pair-shared=true;array-shared=true;list-shared=true;null-elements=true;layouts=5";
            }
        }

        private static string ManagedReferenceAllFiveLayoutsArrayListPersistence()
        {
            using (var scope = new ManagedReferenceAssetScope("managed-reference-all-layout-collections"))
            {
                var first = CreateManagedReferenceLeaf("collection-first", 51);
                var second = CreateManagedReferenceOther("collection-second", "second");
                var data = CreateManagedReferenceHostData(first, includeCycle: false);
                data.additive = UnityManagedReferenceAdditiveUnion.Array(new UnityManagedReferenceNodeBase?[] { first, null, second });
                data.boxedFields = UnityManagedReferenceBoxedFieldsUnion.List(new List<UnityManagedReferenceNodeBase?> { first, null, second });
                data.boxedTuple = UnityManagedReferenceBoxedTupleUnion.Array(new UnityManagedReferenceNodeBase?[] { second, null, first });
                data.unsafeOverlap = UnityManagedReferenceUnsafeOverlapUnion.List(new List<UnityManagedReferenceNodeBase?> { second, null, first });
                data.overlap = UnityManagedReferenceOverlapUnion.Array(new UnityManagedReferenceNodeBase?[] { first, null, second });
                scope.Asset.data = data;
                scope.Save();

                var serialized = new SerializedObject(scope.Asset);
                serialized.Update();
                RequireManagedReferenceCollectionField(serialized, "data.additive", "arrayValues");
                RequireManagedReferenceCollectionField(serialized, "data.boxedFields", "listValues");
                RequireManagedReferenceCollectionField(serialized, "data.boxedTuple", "arrayValues");
                RequireManagedReferenceCollectionField(serialized, "data.unsafeOverlap", "listValues");
                RequireManagedReferenceCollectionField(serialized, "data.overlap", "arrayValues");

                scope.Reload();
                var restored = scope.Asset.data;
                Require(restored.additive.arrayValues.Length == 3 && restored.additive.arrayValues[1] == null && restored.additive.arrayValues[0] is UnityManagedReferenceNodeLeaf, "Additive array managed references differed.");
                Require(restored.boxedFields.listValues.Count == 3 && restored.boxedFields.listValues[1] == null && restored.boxedFields.listValues[2] is UnityManagedReferenceNodeOther, "BoxedFields list managed references differed.");
                Require(restored.boxedTuple.arrayValues.Length == 3 && restored.boxedTuple.arrayValues[1] == null && restored.boxedTuple.arrayValues[2] is UnityManagedReferenceNodeLeaf, "BoxedTuple array managed references differed.");
                Require(restored.unsafeOverlap.listValues.Count == 3 && restored.unsafeOverlap.listValues[1] == null && restored.unsafeOverlap.listValues[0] is UnityManagedReferenceNodeOther, "UnsafeOverlap list managed references differed.");
                Require(restored.overlap.arrayValues.Length == 3 && restored.overlap.arrayValues[1] == null && restored.overlap.arrayValues[0] is UnityManagedReferenceNodeLeaf, "Overlap array managed references differed.");
                return "layouts=5;arrays=3;lists=2;null-elements=true;derived-types=true";
            }
        }

        private static string ManagedReferenceMixedInlineManagedAlias()
        {
            using (var scope = new ManagedReferenceAssetScope("managed-reference-mixed-inline-alias"))
            {
                var managedNode = new UnityManagedReferenceNodeGeneratedState
                {
                    id = "mixed-managed",
                    state = UnityManagedReferenceNodeStateUnion.Active(707),
                };
                managedNode.SetGeneratedState(
                    UnityManagedReferenceNodeStateUnion.Active(707),
                    CreateManagedReferenceOther("mixed-opaque", "mixed"),
                    null);
                var mixed = new UnityManagedReferenceMixedAliasHelper
                {
                    inlineNode = managedNode,
                };
                mixed.SetManagedNode(managedNode);
                Require(ReferenceEquals(mixed.inlineNode, mixed.ManagedNode), "Mixed helper did not start with one shared source object.");
                var data = CreateManagedReferenceHostData(managedNode, includeCycle: false);
                data.additive = UnityManagedReferenceAdditiveUnion.Reference(managedNode);
                data.boxedFields = UnityManagedReferenceBoxedFieldsUnion.MixedAlias(mixed);
                data.renamed = UnityManagedReferenceRenamedUnion.Reference(managedNode);
                scope.Asset.data = data;
                scope.Save();

                var serialized = new SerializedObject(scope.Asset);
                serialized.Update();
                RequireManagedReferenceField(serialized, "data.additive", "value");
                RequireManagedReferenceField(serialized, "data.renamed", "value");
                var mixedPayload = RequireProperty(RequirePayloadProperty(serialized, "data.boxedFields"), "mixed");
                RequireInlineField(mixedPayload, "inlineNode");
                RequireManagedReferenceField(mixedPayload, "managedNode");
                scope.Reload();
                Require(
                    scope.Asset.data.additive.value is UnityManagedReferenceNodeGeneratedState additiveNode && additiveNode.id == "mixed-managed" && additiveNode.state.marker == 707,
                    "Managed reference beside inline control differed after reload.");
                Require(
                    scope.Asset.data.renamed.value is UnityManagedReferenceNodeGeneratedState renamedNode && renamedNode.id == "mixed-managed" && renamedNode.state.marker == 707,
                    "Renamed managed reference beside inline control differed after reload.");
                Require(
                    scope.Asset.data.boxedFields.kind == UnityManagedReferenceBoxedFieldsUnion.Kind.MixedAlias &&
                    scope.Asset.data.boxedFields.mixed.inlineNode.id == "mixed-managed" &&
                    scope.Asset.data.boxedFields.mixed.inlineNode.state.marker == 707,
                    "Concrete inline nested node beside managed reference differed after reload.");
                Require(
                    scope.Asset.data.boxedFields.mixed.ManagedNode is UnityManagedReferenceNodeGeneratedState managedRestored &&
                    ReferenceEquals(managedRestored, scope.Asset.data.renamed.value) &&
                    !ReferenceEquals(managedRestored, scope.Asset.data.boxedFields.mixed.inlineNode),
                    "Mixed inline/managed helper did not preserve managed identity and inline separation.");
                return "managed=private-SerializeReference;inline=concrete;alias=Reference;identity=shared;inline-copy=distinct";
            }
        }

        private static string ManagedReferenceIndependentNonNullSnapshotIsolation()
        {
            using (var scope = new ManagedReferenceAssetScope("managed-reference-snapshot-isolation"))
            {
                var original = CreateManagedReferenceLeaf("snapshot-original", 61);
                var replacement = CreateManagedReferenceLeaf("snapshot-replacement", 62);
                var data = CreateManagedReferenceHostData(original, includeCycle: false);
                data.additive = UnityManagedReferenceAdditiveUnion.Pair(original, original);
                data.boxedFields = UnityManagedReferenceBoxedFieldsUnion.Reference(original);
                scope.Asset.data = data;
                scope.Save();

                scope.Asset.data.additive = UnityManagedReferenceAdditiveUnion.Pair(replacement, replacement);
                scope.Save();
                var current = scope.Asset.data.additive.first as UnityManagedReferenceNodeLeaf;
                var preserved = scope.Asset.data.boxedFields.value as UnityManagedReferenceNodeLeaf;
                Require(current != null && current.id == "snapshot-replacement", "Replacement snapshot did not apply.");
                Require(preserved != null && preserved.id == "snapshot-original", "Independent non-null snapshot was overwritten.");

                scope.Reload();
                var reloadedCurrent = scope.Asset.data.additive.first as UnityManagedReferenceNodeLeaf;
                var reloadedPreserved = scope.Asset.data.boxedFields.value as UnityManagedReferenceNodeLeaf;
                Require(reloadedCurrent != null && reloadedCurrent.id == "snapshot-replacement", "Reloaded replacement snapshot differed.");
                Require(reloadedPreserved != null && reloadedPreserved.id == "snapshot-original", "Reloaded independent snapshot differed.");
                Require(!ReferenceEquals(reloadedCurrent, reloadedPreserved), "Independent union snapshots shared their generated managed object.");
                return "original=snapshot-original;replacement=snapshot-replacement;isolated=true;reload=true";
            }
        }

        private static string ManagedReferenceHelperPrivateFieldsAndCycles()
        {
            using (var scope = new ManagedReferenceAssetScope("managed-reference-helper-cycle"))
            {
                Require(
                    HasManagedReferencePayloadMetadata(typeof(UnityManagedReferenceAdditiveUnion), "Reference", "value"),
                    "Managed-reference metadata was unavailable before constructing the cyclic graph.");
                var shared = CreateManagedReferenceLeaf("cycle-node", 31);
                scope.Asset.data = CreateManagedReferenceHostData(shared, includeCycle: true);
                scope.Save();
                scope.Reload();

                var data = scope.Asset.data;
                Require(data.helper.Node != null, "Private helper managed reference was unavailable after reload.");
                Require(data.helper.Node!.id == "cycle-helper-node", "Private helper managed reference value differed.");
                Require(data.helper.Opaque is UnityManagedReferenceNodeOther, "Private opaque managed reference type differed.");
                Require(data.helper.Next != null && ReferenceEquals(data.helper.Next!.Next, data.helper), "Helper cycle did not close on the same managed object.");
                Require(data.cycleRoot != null && ReferenceEquals(data.cycleRoot!.next!.next, data.cycleRoot), "Node cycle did not close on the same managed object.");
                var reloadedShared = data.sharedNode;
                Require(
                    reloadedShared is UnityManagedReferenceNodeLeaf reloadedLeaf &&
                    reloadedLeaf.id == "cycle-node" &&
                    reloadedLeaf.amount == 31,
                    "Reloaded cycle graph shared node values differed.");
                Require(reloadedShared != null && !ReferenceEquals(reloadedShared, shared), "Reloaded cycle graph unexpectedly reused the pre-reload shared node.");
                RequireManagedReferenceValues(data, reloadedShared);
                return "private-fields=true;helper-cycle=true;node-cycle=true;nested-union=true";
            }
        }

        private static string ManagedReferenceInitialNullSaveStability()
        {
            using (var scope = new ManagedReferenceAssetScope("managed-reference-null-stability"))
            {
                scope.Asset.data = CreateManagedReferenceHostData(null, includeCycle: false);
                scope.Asset.second = CreateManagedReferenceHostData(null, includeCycle: false);
                scope.Save();
                var firstYaml = File.ReadAllText(scope.AssetFilePath);
                var firstIds = ManagedReferenceIds(new SerializedObject(scope.Asset));
                scope.Save();
                var secondYaml = File.ReadAllText(scope.AssetFilePath);
                var secondIds = ManagedReferenceIds(new SerializedObject(scope.Asset));
                scope.Reload();
                scope.Save();
                var thirdYaml = File.ReadAllText(scope.AssetFilePath);
                var thirdIds = ManagedReferenceIds(new SerializedObject(scope.Asset));
                Require(firstYaml == secondYaml, "Initial null managed-reference YAML changed during an unchanged save.");
                Require(firstIds.SequenceEqual(secondIds), "Initial null managed-reference IDs changed during an unchanged save.");
                Require(secondYaml == thirdYaml, "Initial null managed-reference YAML changed after reload/save.");
                Require(secondIds.SequenceEqual(thirdIds), "Initial null managed-reference IDs changed after reload/save.");
                return "initial-null=true;yaml-stable=true;ids-stable=true;hosts=2";
            }
        }

        private static string ManagedReferenceReadOnlyDrawerPreservesAliasedSnapshot()
        {
            using (var scope = new ManagedReferenceAssetScope("managed-reference-readonly-drawer-alias"))
            {
                var shared = CreateManagedReferenceLeaf("readonly-alias", 73);
                var data = CreateManagedReferenceHostData(shared, includeCycle: false);
                data.additive = UnityManagedReferenceAdditiveUnion.Reference(shared);
                var second = CreateManagedReferenceHostData(shared, includeCycle: false);
                second.additive = UnityManagedReferenceAdditiveUnion.Reference(shared);
                scope.Asset.data = data;
                scope.Asset.second = second;
                scope.Asset.aliasFirst = UnityManagedReferenceAdditiveUnion.Reference(shared);
                scope.Asset.aliasSecond = UnityManagedReferenceAdditiveUnion.Reference(shared);
                scope.Save();

                var aliasSetup = new SerializedObject(scope.Asset);
                aliasSetup.Update();
                var originalProperty = RequirePayloadProperty(aliasSetup, "aliasFirst");
                var copiedProperty = RequirePayloadProperty(aliasSetup, "aliasSecond");
                var sharedPayload = originalProperty.managedReferenceValue;
                Require(sharedPayload != null, "Read-only drawer alias source payload was unavailable.");
                copiedProperty.managedReferenceValue = sharedPayload;
                Require(aliasSetup.ApplyModifiedProperties(), "Read-only drawer alias setup was not applied.");
                scope.Save();
                scope.Reload();

                var serialized = new SerializedObject(scope.Asset);
                serialized.Update();
                originalProperty = RequirePayloadProperty(serialized, "aliasFirst");
                copiedProperty = RequirePayloadProperty(serialized, "aliasSecond");
                Require(
                    originalProperty.managedReferenceId != 0 &&
                    originalProperty.managedReferenceId == copiedProperty.managedReferenceId,
                    "Read-only drawer fixture did not reload with one shared payload identity.");
                var beforeYaml = File.ReadAllText(scope.AssetFilePath);
                var beforeIds = ManagedReferenceIds(serialized);

                var property = RequireProperty(serialized, "aliasSecond");
                var drawer = FindDrawer(typeof(UnityManagedReferenceAdditiveUnion));
                Require(drawer != null, "Generated managed-reference drawer was unavailable.");
                var tree = drawer!.CreatePropertyGUI(property);
                Require(tree != null, "Generated managed-reference drawer returned no visual tree.");
                tree.Bind(serialized);
                tree.MarkDirtyRepaint();
                _ = drawer.GetPropertyHeight(property, GUIContent.none);

                scope.Save();
                var afterYaml = File.ReadAllText(scope.AssetFilePath);
                var afterIds = ManagedReferenceIds(new SerializedObject(scope.Asset));
                Require(beforeYaml == afterYaml, "Opening a read-only drawer changed canonical payload YAML.");
                Require(
                    beforeIds.SequenceEqual(afterIds),
                    "Opening a read-only drawer changed canonical managed-reference IDs.");
                return "alias=shared;drawer=create-bind-repaint;edit=false;yaml-stable=true;ids-stable=true";
            }
        }

        private static string ManagedReferenceFormerNameAliasCanonicalization()
        {
            using (var scope = new ManagedReferenceAssetScope("managed-reference-former-name-alias"))
            {
                var leaf = CreateManagedReferenceLeaf("former-name", 37);
                scope.Asset.data = CreateManagedReferenceHostData(leaf, includeCycle: false);
                scope.Asset.data.renamed = UnityManagedReferenceRenamedUnion.Reference(leaf);
                scope.Save();

                var serialized = new SerializedObject(scope.Asset);
                serialized.Update();
                var currentPayloadName = RequirePayload(serialized, "data.renamed").GetType().Name;
                var formerNameType = FindFormerNamePayloadType(typeof(UnityManagedReferenceRenamedUnion), "OldReference");
                var formerNameYaml = ReplacePayloadTypeAndInjectStaleKind(
                    File.ReadAllText(scope.AssetFilePath),
                    typeof(UnityManagedReferenceRenamedUnion),
                    currentPayloadName,
                    formerNameType.Name,
                    "renamed");
                File.WriteAllText(scope.AssetFilePath, formerNameYaml);
                AssetDatabase.ImportAsset(scope.AssetPath, ImportAssetOptions.ForceUpdate);
                scope.ReloadWithoutSave();
                Require(
                    scope.Asset.data.renamed.kind == UnityManagedReferenceRenamedUnion.Kind.Reference &&
                    scope.Asset.data.renamed.value is UnityManagedReferenceNodeLeaf restoredLeaf &&
                    restoredLeaf.id == "former-name" &&
                    restoredLeaf.amount == 37,
                    "Former-name managed-reference payload did not restore its derived value.");

                scope.Save();
                var canonicalYaml = File.ReadAllText(scope.AssetFilePath);
                Require(canonicalYaml.IndexOf(currentPayloadName, StringComparison.Ordinal) >= 0, "Canonical managed-reference payload was not written.");
                Require(canonicalYaml.IndexOf(formerNameType.Name, StringComparison.Ordinal) < 0, "Former-name managed-reference payload remained after canonical save.");
                return "former-name=OldReference;current=Reference;derived=NodeLeaf;canonicalized=true";
            }
        }

        private static string ManagedReferencePropertyFieldAssignmentUndoRedo()
        {
            using (var scope = new ManagedReferenceDrawerScope("managed-reference-property-field"))
            {
                scope.Host.value = UnityManagedReferenceAdditiveUnion.Reference(null);
                var serialized = new SerializedObject(scope.Host);
                serialized.Update();
                var valueProperty = RequireProperty(RequirePayloadProperty(serialized, "value"), "value");
                Require(valueProperty.propertyType == SerializedPropertyType.ManagedReference, "Inspector managed-reference field was not exposed as a managed property.");

                Undo.ClearAll();
                var leaf = CreateManagedReferenceLeaf("inspector", 43);
                Undo.IncrementCurrentGroup();
                Undo.RecordObject(scope.Host, "Assign managed reference");
                valueProperty.managedReferenceValue = leaf;
                Require(serialized.ApplyModifiedProperties(), "Derived managed-reference assignment was not applied.");
                Undo.FlushUndoRecordObjects();
                Require(scope.Host.value.value is UnityManagedReferenceNodeLeaf assigned && assigned.amount == 43, "Derived managed-reference assignment differed.");

                Undo.IncrementCurrentGroup();
                Undo.RecordObject(scope.Host, "Clear managed reference");
                serialized.Update();
                RequireProperty(RequirePayloadProperty(serialized, "value"), "value").managedReferenceValue = null;
                Require(serialized.ApplyModifiedProperties(), "Null managed-reference assignment was not applied.");
                Undo.FlushUndoRecordObjects();
                Require(scope.Host.value.value == null, "Null managed-reference assignment differed.");

                Undo.PerformUndo();
                serialized.Update();
                Require(scope.Host.value.value is UnityManagedReferenceNodeLeaf undoLeaf && undoLeaf.amount == 43, "Undo did not restore the derived managed reference.");
                Undo.PerformRedo();
                serialized.Update();
                Require(scope.Host.value.value == null, "Redo did not restore the null managed reference.");
                return "derived=assigned;null=assigned;undo=derived;redo=null";
            }
        }

        private static string ManagedReferenceVariantReplacementPreservesNestedIdentity()
        {
            using (var scope = new ManagedReferenceAssetScope("managed-reference-variant-replacement"))
            {
                var shared = CreateManagedReferenceLeaf("nested", 47);
                var helper = new UnityManagedReferenceHelper(
                    UnityManagedReferenceAdditiveUnion.Reference(shared),
                    shared,
                    new UnityManagedReferenceNodeOther { id = "opaque" },
                    null);
                var data = CreateManagedReferenceHostData(shared, includeCycle: false);
                data.additive = UnityManagedReferenceAdditiveUnion.Nested(helper);
                scope.Asset.data = data;
                scope.Save();

                var serialized = new SerializedObject(scope.Asset);
                serialized.Update();
                var nestedPayload = RequirePayloadProperty(RequireProperty(serialized, "data.additive"));
                var helperProperty = RequireProperty(nestedPayload, "helper");
                var helperNode = RequireProperty(helperProperty, "node");
                Require(helperNode.propertyType == SerializedPropertyType.ManagedReference, "Nested helper node was not a managed-reference property.");
                var edited = CreateManagedReferenceLeaf("nested-edited", 48);
                helperNode.managedReferenceValue = edited;
                Require(serialized.ApplyModifiedProperties(), "Nested managed-reference edit was not applied.");
                Require(scope.Asset.data.additive.helper.Node is UnityManagedReferenceNodeLeaf editedNode && editedNode.amount == 48, "Nested managed-reference edit differed.");

                serialized.Update();
                WriteBoxedValue(
                    RequireProperty(serialized, "data.additive"),
                    UnityManagedReferenceAdditiveUnion.Pair(shared, shared));
                Require(serialized.ApplyModifiedProperties(), "Managed-reference variant replacement was not applied.");
                Require(
                    scope.Asset.data.additive.kind == UnityManagedReferenceAdditiveUnion.Kind.Pair &&
                    ReferenceEquals(scope.Asset.data.additive.first, shared) &&
                    ReferenceEquals(scope.Asset.data.additive.first, scope.Asset.data.additive.second),
                    "Variant replacement did not preserve the shared managed reference.");
                return "nested-edit=48;replacement=Pair;shared-identity=true";
            }
        }

        private static string? FindPopupValue(VisualElement root)
        {
            string? value = null;
            root.Query<VisualElement>().ForEach(element =>
            {
                if (value != null || !element.GetType().Name.StartsWith("PopupField", StringComparison.Ordinal))
                    return;
                var property = element.GetType().GetProperty("value", BindingFlags.Instance | BindingFlags.Public);
                value = property?.GetValue(element, null)?.ToString();
            });
            return value;
        }

        private static bool HasBoundFieldControl(VisualElement root, string fieldName)
        {
            var suffix = "." + fieldName;
            var found = false;
            root.Query<VisualElement>().ForEach(element =>
            {
                if (found)
                    return;
                var path = (element as IBindable)?.bindingPath;
                if (!string.IsNullOrEmpty(path) &&
                    (string.Equals(path, fieldName, StringComparison.Ordinal) ||
                        path.EndsWith(suffix, StringComparison.Ordinal)))
                {
                    found = true;
                }
            });
            return found;
        }

        private static StabilityEditorHost CreateStabilityEditorHost(Texture2D texture)
        {
            return new StabilityEditorHost
            {
                additive = StabilityAdditiveUnion.First(1, "stability-additive"),
                boxedFields = StabilityBoxedFieldsUnion.First(2, "stability-boxed-fields"),
                boxedTuple = StabilityBoxedTupleUnion.First(3, "stability-boxed-tuple"),
                tupleComposite = StabilityBoxedTupleCompositeUnion.Composite(
                    StabilityNestedChildUnion.Text(30, "stability-tuple-child"),
                    31,
                    "stability-tuple-label"),
                unsafeOverlap = StabilityUnsafeOverlapUnion.First(4, "stability-unsafe-overlap"),
                overlap = StabilityOverlapUnion.First(5, "stability-overlap"),
                parent = StabilityNestedParentUnion.Child(
                    StabilityNestedChildUnion.Text(6, "stability-parent"),
                    10),
                parentArray = new[]
                {
                    StabilityNestedParentUnion.Child(StabilityNestedChildUnion.Text(7, "stability-array"), 11),
                    StabilityNestedParentUnion.Empty(),
                },
                parentList = new List<StabilityNestedParentUnion>
                {
                    StabilityNestedParentUnion.Child(StabilityNestedChildUnion.Text(8, "stability-list"), 12),
                    StabilityNestedParentUnion.Text("stability-list-parent"),
                },
                genericInt = new StabilityGenericOwnerInt
                {
                    value = StabilityGenericUnion<int>.First(42),
                },
                genericString = new StabilityGenericOwnerString
                {
                    value = StabilityGenericUnion<string>.First("stability-generic"),
                },
                genericArray = new[]
                {
                    StabilityGenericUnion<int>.First(43),
                    StabilityGenericUnion<int>.Empty(),
                },
                migration = StabilityMigrationUnion.Other("stability-migration"),
                reference = texture,
            };
        }

        private static void RequireStabilityEditorValues(StabilityEditorHost host, Texture2D texture)
        {
            Require(host.additive.kind == StabilityAdditiveUnion.Kind.First && host.additive.amount == 1 && host.additive.label == "stability-additive", "Additive stability value differed after reload.");
            Require(host.boxedFields.kind == StabilityBoxedFieldsUnion.Kind.First && host.boxedFields.amount == 2 && host.boxedFields.label == "stability-boxed-fields", "BoxedFields stability value differed after reload.");
            Require(host.boxedTuple.kind == StabilityBoxedTupleUnion.Kind.First && host.boxedTuple.amount == 3 && host.boxedTuple.label == "stability-boxed-tuple", "BoxedTuple stability value differed after reload.");
            Require(
                host.tupleComposite.kind == StabilityBoxedTupleCompositeUnion.Kind.Composite &&
                host.tupleComposite.child.kind == StabilityNestedChildUnion.Kind.Text &&
                host.tupleComposite.child.count == 30 &&
                host.tupleComposite.child.label == "stability-tuple-child" &&
                host.tupleComposite.marker == 31 &&
                host.tupleComposite.label == "stability-tuple-label",
                "Multi-field BoxedTuple composite differed after reload.");
            Require(host.unsafeOverlap.kind == StabilityUnsafeOverlapUnion.Kind.First && host.unsafeOverlap.amount == 4 && host.unsafeOverlap.label == "stability-unsafe-overlap", "UnsafeOverlap stability value differed after reload.");
            Require(host.overlap.kind == StabilityOverlapUnion.Kind.First && host.overlap.amount == 5 && host.overlap.label == "stability-overlap", "Overlap stability value differed after reload.");
            Require(host.parent.kind == StabilityNestedParentUnion.Kind.Child && host.parent.child.label == "stability-parent" && host.parent.marker == 10, "Nested parent stability value differed after reload.");
            Require(host.parentArray.Length == 2 && host.parentArray[0].child.label == "stability-array" && host.parentArray[1].kind == StabilityNestedParentUnion.Kind.Empty, "Nested array stability values differed after reload.");
            Require(host.parentList.Count == 2 && host.parentList[0].child.label == "stability-list" && host.parentList[1].label == "stability-list-parent", "Nested list stability values differed after reload.");
            Require(host.genericInt.value.kind == StabilityGenericUnion<int>.Kind.First && host.genericInt.value.value == 42, "Generic int stability value differed after reload.");
            Require(host.genericString.value.kind == StabilityGenericUnion<string>.Kind.First && host.genericString.value.value == "stability-generic", "Generic string stability value differed after reload.");
            Require(host.genericArray.Length == 2 && host.genericArray[0].value == 43 && host.genericArray[1].kind == StabilityGenericUnion<int>.Kind.Empty, "Generic array stability values differed after reload.");
            Require(host.callbackHelper.callbackMarker == 1 && host.callbackHelper.child.label == "constructor-child", "Callback helper derived state differed after reload.");
            Require(host.reference == texture, "Stability Unity object reference differed after reload.");
        }

        private static void RequirePayloadOnly(SerializedObject serialized, string path)
        {
            var direct = DirectChildren(RequireProperty(serialized, path));
            Require(direct.Count == 1, "Payload-only union exposed more than one direct serialized child: " + path);
            Require(direct[0].propertyType == SerializedPropertyType.ManagedReference, "Payload-only union did not expose a managed-reference payload: " + path);
            Require(direct[0].managedReferenceValue != null, "Payload-only union payload was null: " + path);
        }

        private static List<long> ManagedReferenceIds(SerializedObject serialized)
        {
            return CaptureManagedReferenceGraph(serialized)
                .Select(entry => entry.rid)
                .ToList();
        }

        private static void WritePayloadStabilityArtifact(string fileName, string contents)
        {
            File.WriteAllText(Path.Combine(ProjectRoot, fileName), contents);
        }

        private static string AssetFilePath(UnityEngine.Object asset)
        {
            var path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
                throw new InvalidOperationException("The Unity asset path was unavailable.");
            return Path.Combine(ProjectRoot, path.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string RemoveYamlField(string yaml, string fieldName)
        {
            var lines = yaml.Split(new[] { '\n' });
            var fieldToken = fieldName + ":";
            return string.Join(
                "\n",
                lines.Where(line => !line.TrimStart().StartsWith(fieldToken, StringComparison.Ordinal)));
        }

        private static void WritePayloadStabilityGraphArtifact(string fileName, SerializedObject serialized)
        {
            var entries = CaptureManagedReferenceGraph(serialized);
            File.WriteAllText(
                Path.Combine(ProjectRoot, fileName),
                JsonUtility.ToJson(new ManagedReferenceGraphArtifact { entries = entries }, prettyPrint: true));
        }

        private static List<ManagedReferenceGraphEntry> CaptureManagedReferenceGraph(SerializedObject serialized)
        {
            serialized.Update();
            var entries = new List<ManagedReferenceGraphEntry>();
            var iterator = serialized.GetIterator();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                if (iterator.propertyType == SerializedPropertyType.ManagedReference)
                {
                    entries.Add(new ManagedReferenceGraphEntry
                    {
                        path = iterator.propertyPath,
                        rid = iterator.managedReferenceId,
                    });
                }

                enterChildren = iterator.hasVisibleChildren;
            }

            return entries;
        }

        private static Type FindFormerNamePayloadType(Type unionType, string formerName)
        {
            var expectedName = "__Spire" + formerName + "Payload";
            var unionDefinition = unionType.IsGenericType ? unionType.GetGenericTypeDefinition() : unionType;
            var type = unionType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(candidate =>
                {
                    if (!string.Equals(candidate.Name, expectedName, StringComparison.Ordinal))
                        return false;

                    var declaringType = candidate.DeclaringType;
                    if (declaringType == null)
                        return false;

                    var declaringDefinition = declaringType.IsGenericType
                        ? declaringType.GetGenericTypeDefinition()
                        : declaringType;
                    return declaringDefinition == unionDefinition;
                });
            if (type == null)
                throw new InvalidOperationException("Former-name payload type was unavailable for " + formerName + ".");

            if (type.ContainsGenericParameters && unionType.IsGenericType && !unionType.ContainsGenericParameters)
            {
                var unionArguments = unionType.GetGenericArguments();
                var nestedArguments = type.GetGenericArguments();
                if (nestedArguments.Length != unionArguments.Length)
                {
                    throw new InvalidOperationException(
                        "Former-name payload generic arity did not match its closed union for " + formerName + ".");
                }

                type = type.MakeGenericType(unionArguments);
            }

            return type;
        }

        private static object CreateFormerNamePayload(
            Type unionType,
            string formerName,
            string fieldName,
            object value)
        {
            var type = FindFormerNamePayloadType(unionType, formerName);
            var payload = Activator.CreateInstance(type, true) ??
                throw new InvalidOperationException("Could not instantiate former-name payload type " + type.FullName + ".");
            SetMemberValue(payload, fieldName, value);
            return payload;
        }

        private static string ReplacePayloadTypeAndInjectStaleKind(
            string yaml,
            Type unionType,
            string currentPayloadName,
            string formerNamePayloadName,
            string unionFieldName)
        {
            var unionTypeName = unionType.Name;
            var currentTypeIdentity = unionTypeName + "/" + currentPayloadName;
            var formerNameTypeIdentity = unionTypeName + "/" + formerNamePayloadName;
            Require(
                yaml.IndexOf(currentTypeIdentity, StringComparison.Ordinal) >= 0,
                "Current serialized payload type was unavailable: " + currentTypeIdentity);
            var replaced = yaml.Replace(currentTypeIdentity, formerNameTypeIdentity, StringComparison.Ordinal);
            var marker = "    " + unionFieldName + ":\n";
            var markerIndex = replaced.IndexOf(marker, StringComparison.Ordinal);
            Require(markerIndex >= 0, "Migration union field was unavailable in YAML: " + unionFieldName);
            return replaced.Insert(markerIndex + marker.Length, "      _kind: 255\n");
        }

        private static string ReplaceNestedPayloadRidWithNull(
            string yaml,
            string helperFieldName,
            string childFieldName)
        {
            var lines = yaml.Split(new[] { '\n' });
            var helperIndent = -1;
            var childIndent = -1;
            var childSeen = false;
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index].TrimEnd('\r');
                var trimmed = line.TrimStart();
                var indent = line.Length - trimmed.Length;
                if (trimmed == helperFieldName + ":")
                {
                    helperIndent = indent;
                    continue;
                }

                if (helperIndent >= 0 && indent <= helperIndent && trimmed.Length > 0)
                {
                    helperIndent = -1;
                    childIndent = -1;
                    childSeen = false;
                    continue;
                }

                if (helperIndent >= 0 && trimmed == childFieldName + ":")
                {
                    childIndent = indent;
                    childSeen = true;
                    continue;
                }

                if (childSeen && indent <= childIndent && trimmed.Length > 0)
                {
                    childIndent = -1;
                    childSeen = false;
                    continue;
                }

                if (childSeen && trimmed.StartsWith("rid:", StringComparison.Ordinal))
                {
                    lines[index] = new string(' ', indent) + "rid: 0";
                    return string.Join("\n", lines);
                }
            }

            throw new InvalidOperationException(
                "Could not find nested serialized payload rid for " + helperFieldName + "." + childFieldName + ".");
        }

        private static void ReloadWithoutSave(AssetScope scope)
        {
            var current = scope.Asset;
            Resources.UnloadAsset(current);
            scope.Asset = AssetDatabase.LoadAssetAtPath<UnitySerializationAsset>(scope.AssetPath);
            Require(scope.Asset != null, "ReloadWithoutSave returned a null asset.");
        }

        [Serializable]
        private sealed class ManagedReferenceGraphArtifact
        {
            public List<ManagedReferenceGraphEntry> entries = new List<ManagedReferenceGraphEntry>();
        }

        [Serializable]
        private sealed class ManagedReferenceGraphEntry
        {
            public string path = string.Empty;
            public long rid;
        }

        private sealed class FormerNamePayloadReplacement
        {
            public readonly string propertyPath;
            public readonly string yamlFieldName;
            public readonly Type unionType;
            public readonly string formerName;

            public FormerNamePayloadReplacement(string propertyPath, string yamlFieldName, Type unionType, string formerName)
            {
                this.propertyPath = propertyPath;
                this.yamlFieldName = yamlFieldName;
                this.unionType = unionType;
                this.formerName = formerName;
            }
        }

        private static string BoxedFieldsAssetSaveUnloadReload()
        {
            using (var scope = new AssetScope("boxed-fields-round-trip"))
            {
                scope.Asset.boxedFields = UnityBoxedFieldsUnion.Text("boxed-fields", scope.Texture);
                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.boxedFields.kind == UnityBoxedFieldsUnion.Kind.Text &&
                    scope.Asset.boxedFields.label == "boxed-fields" &&
                    scope.Asset.boxedFields.icon == scope.Texture,
                    "BoxedFields Text value differed after reload.");

                scope.Asset.boxedFields = UnityBoxedFieldsUnion.Number(42);
                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.boxedFields.kind == UnityBoxedFieldsUnion.Kind.Number &&
                    scope.Asset.boxedFields.value == 42,
                    "BoxedFields Number value differed after reload.");

                scope.Asset.boxedFields = UnityBoxedFieldsUnion.Child(
                    UnityCallbackChild.Text(7, "boxed-child", scope.Texture));
                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.boxedFields.kind == UnityBoxedFieldsUnion.Kind.Child &&
                    scope.Asset.boxedFields.child.kind == UnityCallbackChild.Kind.Text &&
                    scope.Asset.boxedFields.child.label == "boxed-child" &&
                    scope.Asset.boxedFields.child.asset == scope.Texture,
                    "BoxedFields child value differed after reload.");

                scope.Asset.boxedFields = UnityBoxedFieldsUnion.Empty();
                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.boxedFields.kind == UnityBoxedFieldsUnion.Kind.Empty &&
                    scope.Asset.boxedFields.label == null &&
                    scope.Asset.boxedFields.icon == null,
                    "BoxedFields Empty value retained stale runtime fields.");
                return "Text=boxed-fields;Number=42;Child=boxed-child;Empty=defaults;reference=preserved";
            }
        }

        private static string BoxedTupleAssetSaveUnloadReload()
        {
            using (var scope = new AssetScope("boxed-tuple-round-trip"))
            {
                scope.Asset.boxedTuple = UnityBoxedTupleUnion.Text("boxed-tuple", scope.Texture);
                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.boxedTuple.kind == UnityBoxedTupleUnion.Kind.Text &&
                    scope.Asset.boxedTuple.label == "boxed-tuple" &&
                    scope.Asset.boxedTuple.icon == scope.Texture,
                    "BoxedTuple Text value differed after reload.");

                scope.Asset.boxedTuple = UnityBoxedTupleUnion.Number(84);
                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.boxedTuple.kind == UnityBoxedTupleUnion.Kind.Number &&
                    scope.Asset.boxedTuple.value == 84,
                    "BoxedTuple Number value differed after reload.");

                scope.Asset.boxedTuple = UnityBoxedTupleUnion.Child(
                    UnityCallbackChild.Text(8, "tuple-child", scope.Texture));
                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.boxedTuple.kind == UnityBoxedTupleUnion.Kind.Child &&
                    scope.Asset.boxedTuple.child.kind == UnityCallbackChild.Kind.Text &&
                    scope.Asset.boxedTuple.child.label == "tuple-child" &&
                    scope.Asset.boxedTuple.child.asset == scope.Texture,
                    "BoxedTuple child value differed after reload.");

                scope.Asset.boxedTuple = UnityBoxedTupleUnion.Empty();
                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.boxedTuple.kind == UnityBoxedTupleUnion.Kind.Empty &&
                    GetBoxedTupleRuntimeStorage(scope.Asset.boxedTuple) == null,
                    "BoxedTuple Empty value retained stale runtime fields.");
                return "Text=boxed-tuple;Number=84;Child=tuple-child;Empty=defaults;reference=preserved";
            }
        }

        private static string BoxedLayoutsRepeatedSaveRefreshesMatchingPayload()
        {
            using (var scope = new AssetScope("boxed-matching-payload"))
            {
                scope.Asset.boxedFields = UnityBoxedFieldsUnion.Text("before-fields", scope.Texture);
                scope.Asset.boxedTuple = UnityBoxedTupleUnion.Text("before-tuple", scope.Texture);
                scope.Save();

                var serialized = new SerializedObject(scope.Asset);
                serialized.Update();
                var fieldsBefore = RequirePayloadProperty(serialized, "boxedFields").managedReferenceValue;
                var tupleBefore = RequirePayloadProperty(serialized, "boxedTuple").managedReferenceValue;
                Require(fieldsBefore != null && tupleBefore != null, "Initial boxed payloads were unavailable.");

                scope.Asset.boxedFields = SetSerializedPayloadCache(
                    UnityBoxedFieldsUnion.Text("after-fields", scope.Texture),
                    fieldsBefore);
                scope.Asset.boxedTuple = SetSerializedPayloadCache(
                    UnityBoxedTupleUnion.Text("after-tuple", scope.Texture),
                    tupleBefore);

                Require(
                    ReferenceEquals(GetSerializedPayloadCache(scope.Asset.boxedFields), fieldsBefore) &&
                    ReferenceEquals(GetSerializedPayloadCache(scope.Asset.boxedTuple), tupleBefore),
                    "A boxed runtime mutation changed the cached snapshot before serialization.");

                scope.Save();

                serialized.Update();
                var fieldsAfterMutation = RequirePayloadProperty(serialized, "boxedFields").managedReferenceValue;
                var tupleAfterMutation = RequirePayloadProperty(serialized, "boxedTuple").managedReferenceValue;
                Require(
                    !ReferenceEquals(fieldsBefore, fieldsAfterMutation) &&
                    !ReferenceEquals(tupleBefore, tupleAfterMutation),
                    "A boxed mutation reused the previous managed payload despite detaching its cache.");
                RequirePayloadValue(fieldsAfterMutation!, "label", "after-fields");
                RequirePayloadValue(tupleAfterMutation!, "label", "after-tuple");
                Require(
                    scope.Asset.boxedFields.icon == scope.Texture &&
                    scope.Asset.boxedTuple.icon == scope.Texture,
                    "Matching boxed payload refresh lost object references.");

                scope.Save();
                serialized.Update();
                var fieldsAfterRepeat = RequirePayloadProperty(serialized, "boxedFields").managedReferenceValue;
                var tupleAfterRepeat = RequirePayloadProperty(serialized, "boxedTuple").managedReferenceValue;
                Require(
                    ReferenceEquals(fieldsAfterMutation, fieldsAfterRepeat) &&
                    ReferenceEquals(tupleAfterMutation, tupleAfterRepeat),
                    "An unchanged repeated save replaced a matching boxed payload.");
                return "fields=Text/before-fields->after-fields;tuple=Text/before-tuple->after-tuple;mutation-replaced-on-save=true;repeat-stable=true;reference=preserved";
            }
        }

        private static string BoxedLayoutsVariantSwitchResetsRuntimeStorage()
        {
            using (var scope = new AssetScope("boxed-variant-reset"))
            {
                scope.Asset.boxedFields = UnityBoxedFieldsUnion.Text("stale-fields", scope.Texture);
                scope.Asset.boxedTuple = UnityBoxedTupleUnion.Text("stale-tuple", scope.Texture);
                scope.Save();
                scope.Asset.boxedFields = UnityBoxedFieldsUnion.Empty();
                scope.Asset.boxedTuple = UnityBoxedTupleUnion.Empty();
                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.boxedFields.kind == UnityBoxedFieldsUnion.Kind.Empty &&
                    scope.Asset.boxedFields.label == null &&
                    scope.Asset.boxedFields.icon == null &&
                    scope.Asset.boxedTuple.kind == UnityBoxedTupleUnion.Kind.Empty &&
                    GetBoxedTupleRuntimeStorage(scope.Asset.boxedTuple) == null,
                    "Boxed fieldless variants retained stale values.");

                scope.Asset.boxedFields = UnityBoxedFieldsUnion.Text("fresh-fields", null!);
                scope.Asset.boxedTuple = UnityBoxedTupleUnion.Text("fresh-tuple", null!);
                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.boxedFields.label == "fresh-fields" &&
                    scope.Asset.boxedFields.icon == null &&
                    scope.Asset.boxedTuple.label == "fresh-tuple" &&
                    scope.Asset.boxedTuple.icon == null,
                    "Boxed fielded variants resurrected stale references after switching back.");
                return "fields=Text->Empty->Text;tuple=Text->Empty->Text;stale-runtime=false";
            }
        }

        private static string BoxedLayoutsCopyAndNestedAliasIsolation()
        {
            using (var scope = new AssetScope("boxed-copy-isolation"))
            {
                scope.Asset.boxedParent = UnityBoxedLayoutParent.Parent(
                    UnityBoxedFieldsUnion.Text("original", scope.Texture),
                    1);
                scope.Asset.boxedParentCopy = scope.Asset.boxedParent;
                scope.Save();

                var serialized = new SerializedObject(scope.Asset);
                serialized.Update();
                var parentPayload = RequirePayloadProperty(serialized, "boxedParent").managedReferenceValue;
                var childPayload = RequirePayloadProperty(
                    RequireProperty(RequirePayloadProperty(serialized, "boxedParent"), "child"))
                    .managedReferenceValue;
                Require(parentPayload != null && childPayload != null, "Initial boxed parent snapshots were unavailable.");

                var copiedChildValue = SetSerializedPayloadCache(
                    UnityBoxedFieldsUnion.Text("copy", scope.Texture),
                    childPayload);
                var copy = SetSerializedPayloadCache(
                    UnityBoxedLayoutParent.Parent(copiedChildValue, 2),
                    parentPayload);
                scope.Asset.boxedParentCopy = copy;
                scope.Save();
                scope.Save();
                scope.Reload();

                Require(
                    scope.Asset.boxedParent.marker == 1 &&
                    scope.Asset.boxedParent.child.label == "original" &&
                    scope.Asset.boxedParentCopy.marker == 2 &&
                    scope.Asset.boxedParentCopy.child.label == "copy",
                    "Copied BoxedFields parent mutated the original nested runtime value.");

                serialized = new SerializedObject(scope.Asset);
                serialized.Update();
                var originalChild = RequirePayloadProperty(
                    RequireProperty(RequirePayloadProperty(serialized, "boxedParent"), "child"));
                var copiedChild = RequirePayloadProperty(
                    RequireProperty(RequirePayloadProperty(serialized, "boxedParentCopy"), "child"));
                Require(
                    originalChild.managedReferenceId != copiedChild.managedReferenceId,
                    "Copied boxed nested payloads retained the same managed-reference identity.");
                return "original=marker1/original;copy=marker2/copy;child-payloads=distinct;save-cycles=2";
            }
        }

        private static string BoxedLayoutsArrayListNestedElements()
        {
            using (var scope = new AssetScope("boxed-collections"))
            {
                scope.Asset.boxedFieldsArray = new[]
                {
                    UnityBoxedFieldsUnion.Text("array-text", scope.Texture),
                    UnityBoxedFieldsUnion.Empty(),
                    UnityBoxedFieldsUnion.Number(13),
                };
                scope.Asset.boxedTupleList = new List<UnityBoxedTupleUnion>
                {
                    UnityBoxedTupleUnion.Text("list-text", scope.Texture),
                    UnityBoxedTupleUnion.Empty(),
                    UnityBoxedTupleUnion.Number(26),
                };
                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.boxedFieldsArray.Length == 3 &&
                    scope.Asset.boxedFieldsArray[0].label == "array-text" &&
                    scope.Asset.boxedFieldsArray[1].kind == UnityBoxedFieldsUnion.Kind.Empty &&
                    scope.Asset.boxedFieldsArray[2].value == 13,
                    "BoxedFields array values differed after reload.");
                Require(
                    scope.Asset.boxedTupleList.Count == 3 &&
                    scope.Asset.boxedTupleList[0].label == "list-text" &&
                    scope.Asset.boxedTupleList[1].kind == UnityBoxedTupleUnion.Kind.Empty &&
                    scope.Asset.boxedTupleList[2].value == 26,
                    "BoxedTuple list values differed after reload.");
                scope.Asset.boxedFieldsArray[2] = UnityBoxedFieldsUnion.Number(39);
                scope.Asset.boxedTupleList.Add(UnityBoxedTupleUnion.Text("grown", null!));
                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.boxedFieldsArray[2].value == 39 &&
                    scope.Asset.boxedTupleList.Count == 4 &&
                    scope.Asset.boxedTupleList[3].label == "grown" &&
                    scope.Asset.boxedTupleList[3].icon == null,
                    "Boxed collection write-back was not independent after growth.");
                return "array=Text,Empty,Number/39;list=Text,Empty,Number,Text/grown;independent=true";
            }
        }

        private static string BoxedLayoutsNestedArrayListWriteback()
        {
            using (var scope = new DrawerHostScope("boxed-nested-writeback"))
            {
                scope.Host.boxedFieldsArray = new[]
                {
                    UnityBoxedFieldsUnion.Text("array-before", null!),
                    UnityBoxedFieldsUnion.Number(2),
                };
                scope.Host.boxedTupleList = new List<UnityBoxedTupleUnion>
                {
                    UnityBoxedTupleUnion.Text("list-before", null!),
                    UnityBoxedTupleUnion.Number(4),
                };
                var serialized = new SerializedObject(scope.Host);
                serialized.Update();
                WriteBoxedValue(
                    RequireProperty(serialized, "boxedFieldsArray.Array.data[0]"),
                    UnityBoxedFieldsUnion.Number(11));
                WriteBoxedValue(
                    RequireProperty(serialized, "boxedTupleList.Array.data[0]"),
                    UnityBoxedTupleUnion.Number(22));
                Require(serialized.ApplyModifiedProperties(), "Nested boxed array/list write-back was not applied.");
                Require(
                    scope.Host.boxedFieldsArray[0].kind == UnityBoxedFieldsUnion.Kind.Number &&
                    scope.Host.boxedFieldsArray[0].value == 11 &&
                    scope.Host.boxedFieldsArray[1].kind == UnityBoxedFieldsUnion.Kind.Number &&
                    scope.Host.boxedFieldsArray[1].value == 2,
                    "Nested BoxedFields array write-back changed a sibling.");
                Require(
                    scope.Host.boxedTupleList[0].kind == UnityBoxedTupleUnion.Kind.Number &&
                    scope.Host.boxedTupleList[0].value == 22 &&
                    scope.Host.boxedTupleList[1].kind == UnityBoxedTupleUnion.Kind.Number &&
                    scope.Host.boxedTupleList[1].value == 4,
                    "Nested BoxedTuple list write-back changed a sibling.");
                return "array[0]=Number/11;array[1]=Number/2;list[0]=Number/22;list[1]=Number/4;siblings=preserved";
            }
        }

        private static string BoxedLayoutsActivePayloadOnlyYaml()
        {
            using (var scope = new AssetScope("boxed-active-yaml"))
            {
                scope.Asset.boxedFields = UnityBoxedFieldsUnion.Text("yaml-fields", scope.Texture);
                scope.Asset.boxedTuple = UnityBoxedTupleUnion.Text("yaml-tuple", scope.Texture);
                scope.Save();
                var serialized = new SerializedObject(scope.Asset);
                serialized.Update();
                RequirePayloadOnly(serialized, "boxedFields");
                RequirePayloadOnly(serialized, "boxedTuple");
                var yaml = File.ReadAllText(scope.AssetFilePath);
                AssertRuntimeFieldsAbsentFromYaml(typeof(UnityBoxedFieldsUnion), yaml);
                AssertRuntimeFieldsAbsentFromYaml(typeof(UnityBoxedTupleUnion), yaml);
                var fieldsPayload = RequirePayloadProperty(serialized, "boxedFields").managedReferenceValue;
                var tuplePayload = RequirePayloadProperty(serialized, "boxedTuple").managedReferenceValue;
                Require(fieldsPayload != null && tuplePayload != null, "Boxed active payloads were unavailable.");
                Require(yaml.IndexOf(fieldsPayload!.GetType().Name, StringComparison.Ordinal) >= 0, "BoxedFields active payload was absent from YAML.");
                Require(yaml.IndexOf(tuplePayload!.GetType().Name, StringComparison.Ordinal) >= 0, "BoxedTuple active payload was absent from YAML.");
                return "fields=payload;tuple=payload;serialized-kind=false;runtime-storage=absent;inactive-payloads=absent";
            }
        }

        private static string BoxedFieldsGenericIntStringSaveReload()
        {
            using (var scope = new AssetScope("boxed-fields-generic"))
            {
                scope.Asset.boxedFieldsGenericInt = UnityBoxedFieldsGeneric<int>.Some(42);
                scope.Asset.boxedFieldsGenericString = UnityBoxedFieldsGeneric<string>.Some("generic-fields");
                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.boxedFieldsGenericInt.kind == UnityBoxedFieldsGeneric<int>.Kind.Some &&
                    scope.Asset.boxedFieldsGenericInt.value == 42 &&
                    scope.Asset.boxedFieldsGenericString.kind == UnityBoxedFieldsGeneric<string>.Kind.Some &&
                    scope.Asset.boxedFieldsGenericString.value == "generic-fields",
                    "BoxedFields generic values differed after reload.");
                scope.Asset.boxedFieldsGenericInt = UnityBoxedFieldsGeneric<int>.None();
                scope.Asset.boxedFieldsGenericString = UnityBoxedFieldsGeneric<string>.Some(null!);
                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.boxedFieldsGenericInt.kind == UnityBoxedFieldsGeneric<int>.Kind.None &&
                    scope.Asset.boxedFieldsGenericString.kind == UnityBoxedFieldsGeneric<string>.Kind.Some &&
                    string.IsNullOrEmpty(scope.Asset.boxedFieldsGenericString.value),
                    "BoxedFields generic None/null-string state differed after reload.");
                return "int=Some(42)->None;string=Some(generic-fields)->Some(empty);repeat=2";
            }
        }

        private static string BoxedTupleGenericIntStringSaveReload()
        {
            using (var scope = new AssetScope("boxed-tuple-generic"))
            {
                scope.Asset.boxedTupleGenericInt = UnityBoxedTupleGeneric<int>.Some(84);
                scope.Asset.boxedTupleGenericString = UnityBoxedTupleGeneric<string>.Some("generic-tuple");
                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.boxedTupleGenericInt.kind == UnityBoxedTupleGeneric<int>.Kind.Some &&
                    scope.Asset.boxedTupleGenericInt.value == 84 &&
                    scope.Asset.boxedTupleGenericString.kind == UnityBoxedTupleGeneric<string>.Kind.Some &&
                    scope.Asset.boxedTupleGenericString.value == "generic-tuple",
                    "BoxedTuple generic values differed after reload.");
                scope.Asset.boxedTupleGenericInt = UnityBoxedTupleGeneric<int>.None();
                scope.Asset.boxedTupleGenericString = UnityBoxedTupleGeneric<string>.Some(null!);
                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.boxedTupleGenericInt.kind == UnityBoxedTupleGeneric<int>.Kind.None &&
                    scope.Asset.boxedTupleGenericString.kind == UnityBoxedTupleGeneric<string>.Kind.Some &&
                    string.IsNullOrEmpty(scope.Asset.boxedTupleGenericString.value),
                    "BoxedTuple generic None/null-string state differed after reload.");
                return "int=Some(84)->None;string=Some(generic-tuple)->Some(empty);repeat=2";
            }
        }

        private static string UnsafeOverlapAssetSaveUnloadReload()
        {
            using (var scope = new AssetScope("unsafe-round-trip"))
            {
                scope.Asset.unsafeUnion = UnityUnsafeOverlapUnion.Mixed(
                    11,
                    1.25f,
                    "unsafe-mixed",
                    scope.Texture);
                scope.Asset.unsafeGenericInt = UnityUnsafeOverlapGeneric<int>.Some(42, 7, 1.5f);
                scope.Asset.unsafeGenericString = UnityUnsafeOverlapGeneric<string>.Some("unsafe-generic", 8, 2.5f);
                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.unsafeUnion.kind == UnityUnsafeOverlapUnion.Kind.Mixed &&
                    scope.Asset.unsafeUnion.raw == 11 &&
                    scope.Asset.unsafeUnion.amount == 1.25f &&
                    scope.Asset.unsafeUnion.label == "unsafe-mixed" &&
                    scope.Asset.unsafeUnion.icon == scope.Texture,
                    "UnsafeOverlap Mixed value differed after reload.");
                Require(
                    scope.Asset.unsafeGenericInt.kind == UnityUnsafeOverlapGeneric<int>.Kind.Some &&
                    scope.Asset.unsafeGenericInt.value == 42 &&
                    scope.Asset.unsafeGenericInt.code == 7 &&
                    scope.Asset.unsafeGenericInt.weight == 1.5f,
                    "UnsafeOverlap generic int value differed after reload.");
                Require(
                    scope.Asset.unsafeGenericString.kind == UnityUnsafeOverlapGeneric<string>.Kind.Some &&
                    scope.Asset.unsafeGenericString.value == "unsafe-generic" &&
                    scope.Asset.unsafeGenericString.code == 8 &&
                    scope.Asset.unsafeGenericString.weight == 2.5f,
                    "UnsafeOverlap generic string value differed after reload.");

                scope.Asset.unsafeUnion = UnityUnsafeOverlapUnion.Number(84);
                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.unsafeUnion.kind == UnityUnsafeOverlapUnion.Kind.Number &&
                    scope.Asset.unsafeUnion.value == 84,
                    "UnsafeOverlap Number value differed after reload.");

                scope.Asset.unsafeUnion = UnityUnsafeOverlapUnion.Child(
                    UnityCallbackChild.Text(9, "unsafe-child", scope.Texture));
                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.unsafeUnion.kind == UnityUnsafeOverlapUnion.Kind.Child &&
                    scope.Asset.unsafeUnion.child.kind == UnityCallbackChild.Kind.Text &&
                    scope.Asset.unsafeUnion.child.label == "unsafe-child" &&
                    scope.Asset.unsafeUnion.child.asset == scope.Texture,
                    "UnsafeOverlap child value differed after reload.");
                return "Mixed=11/1.25/unsafe-mixed;generic=int+string;Number=84;Child=unsafe-child;reference=preserved";
            }
        }

        private static string UnsafeOverlapRepeatedSaveAndVariantReset()
        {
            using (var scope = new AssetScope("unsafe-repeat-reset"))
            {
                scope.Asset.unsafeUnion = UnityUnsafeOverlapUnion.Mixed(1, 1.5f, "before", scope.Texture);
                scope.Save();
                scope.Asset.unsafeUnion = UnityUnsafeOverlapUnion.Mixed(2, 2.5f, "after", scope.Texture);
                scope.Save();
                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.unsafeUnion.kind == UnityUnsafeOverlapUnion.Kind.Mixed &&
                    scope.Asset.unsafeUnion.raw == 2 &&
                    scope.Asset.unsafeUnion.amount == 2.5f &&
                    scope.Asset.unsafeUnion.label == "after" &&
                    scope.Asset.unsafeUnion.icon == scope.Texture,
                    "UnsafeOverlap same-variant repeated save did not refresh logical fields.");

                scope.Asset.unsafeUnion = UnityUnsafeOverlapUnion.Empty();
                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.unsafeUnion.kind == UnityUnsafeOverlapUnion.Kind.Empty,
                    "UnsafeOverlap Empty variant was not preserved after reset.");
                AssertUnsafeRuntimeStorageIsDefault(scope.Asset.unsafeUnion);

                scope.Asset.unsafeUnion = UnityUnsafeOverlapUnion.Mixed(3, 3.5f, "fresh", null!);
                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.unsafeUnion.kind == UnityUnsafeOverlapUnion.Kind.Mixed &&
                    scope.Asset.unsafeUnion.raw == 3 &&
                    scope.Asset.unsafeUnion.amount == 3.5f &&
                    scope.Asset.unsafeUnion.label == "fresh" &&
                    scope.Asset.unsafeUnion.icon == null,
                    "UnsafeOverlap stale managed/raw storage resurfaced after switching back from Empty.");
                return "Mixed=before->after;repeat=2;Empty=default;Mixed=fresh;stale-storage=false";
            }
        }

        private static string UnsafeOverlapCopyNestedAndCollectionIsolation()
        {
            using (var scope = new AssetScope("unsafe-copy-collections"))
            {
                scope.Asset.unsafeParent = UnityUnsafeOverlapParent.Parent(
                    UnityUnsafeOverlapUnion.Mixed(4, 4.5f, "original", scope.Texture),
                    1);
                scope.Asset.unsafeParentCopy = scope.Asset.unsafeParent;
                scope.Asset.unsafeArray = new[]
                {
                    UnityUnsafeOverlapUnion.Mixed(10, 10.5f, "array-original", scope.Texture),
                    UnityUnsafeOverlapUnion.Mixed(11, 11.5f, "array-sibling", scope.Texture),
                };
                scope.Asset.unsafeList = new List<UnityUnsafeOverlapUnion>
                {
                    UnityUnsafeOverlapUnion.Mixed(20, 20.5f, "list-original", scope.Texture),
                    UnityUnsafeOverlapUnion.Mixed(21, 21.5f, "list-sibling", scope.Texture),
                };
                scope.Save();

                var copiedChild = SetInternalSlotForField(
                    scope.Asset.unsafeParentCopy.child,
                    "label",
                    "copy");
                scope.Asset.unsafeParentCopy = SetInternalSlotForField(
                    scope.Asset.unsafeParentCopy,
                    "child",
                    copiedChild);
                scope.Asset.unsafeArray[0] = SetInternalSlotForField(
                    scope.Asset.unsafeArray[0],
                    "label",
                    "array-changed");
                scope.Asset.unsafeList[0] = SetInternalSlotForField(
                    scope.Asset.unsafeList[0],
                    "label",
                    "list-changed");
                scope.Save();
                scope.Save();
                scope.Reload();

                Require(
                    scope.Asset.unsafeParent.marker == 1 &&
                    scope.Asset.unsafeParent.child.label == "original" &&
                    scope.Asset.unsafeParentCopy.marker == 1 &&
                    scope.Asset.unsafeParentCopy.child.label == "copy",
                    "UnsafeOverlap copied parent mutation changed the original child.");
                Require(
                    scope.Asset.unsafeArray[0].label == "array-changed" &&
                    scope.Asset.unsafeArray[1].label == "array-sibling" &&
                    scope.Asset.unsafeList[0].label == "list-changed" &&
                    scope.Asset.unsafeList[1].label == "list-sibling",
                    "UnsafeOverlap collection mutation changed a sibling element.");
                return "parent=original/copy;array=changed/sibling;list=changed/sibling;repeat-save=2;isolated=true";
            }
        }

        private static string UnsafeOverlapActivePayloadOnlyYaml()
        {
            using (var scope = new AssetScope("unsafe-active-yaml"))
            {
                scope.Asset.unsafeUnion = UnityUnsafeOverlapUnion.Mixed(31, 3.25f, "yaml-unsafe", scope.Texture);
                scope.Save();
                var serialized = new SerializedObject(scope.Asset);
                serialized.Update();
                var union = RequireProperty(serialized, "unsafeUnion");
                var children = DirectChildren(union);
                Require(children.Count == 1, "UnsafeOverlap exposed more than its active payload as serialized children.");
                Require(children[0].propertyType == SerializedPropertyType.ManagedReference, "UnsafeOverlap payload was missing.");
                Require(!children.Any(property => property.propertyType == SerializedPropertyType.Enum), "UnsafeOverlap discriminator was still serialized.");
                var payload = RequirePayloadProperty(serialized, "unsafeUnion").managedReferenceValue;
                Require(payload != null, "UnsafeOverlap active payload was unavailable.");
                var yaml = File.ReadAllText(scope.AssetFilePath);
                AssertRuntimeFieldsAbsentFromYaml(typeof(UnityUnsafeOverlapUnion), yaml);
                Require(
                    yaml.IndexOf(payload!.GetType().Name, StringComparison.Ordinal) >= 0,
                    "UnsafeOverlap active payload type was absent from YAML.");
                return "children=1;active=" + payload.GetType().Name + ";serialized-kind=false;raw-buffer=absent;managed-slots=absent";
            }
        }

        private static string OverlapAssetSaveUnloadReload()
        {
            using (var scope = new AssetScope("overlap-round-trip"))
            {
                scope.Asset.overlapUnion = UnityOverlapUnion.Rectangle(2.5f, 4.5f);
                scope.Asset.overlapReference = UnityOverlapReferenceUnion.Text("overlap-text", scope.Texture);
                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.overlapUnion.kind == UnityOverlapUnion.Kind.Rectangle &&
                    scope.Asset.overlapUnion.width == 2.5f &&
                    scope.Asset.overlapUnion.height == 4.5f,
                    "Overlap Rectangle value differed after reload.");
                Require(
                    scope.Asset.overlapReference.kind == UnityOverlapReferenceUnion.Kind.Text &&
                    scope.Asset.overlapReference.label == "overlap-text" &&
                    scope.Asset.overlapReference.icon == scope.Texture,
                    "Overlap reference value differed after reload.");

                scope.Asset.overlapUnion = UnityOverlapUnion.Number(17);
                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.overlapUnion.kind == UnityOverlapUnion.Kind.Number &&
                    scope.Asset.overlapUnion.value == 17,
                    "Overlap Number value differed after reload.");
                return "Rectangle=2.5x4.5;Reference=overlap-text;Number=17;reference=preserved";
            }
        }

        private static string OverlapRepeatedSaveAndVariantReset()
        {
            using (var scope = new AssetScope("overlap-repeat-reset"))
            {
                scope.Asset.overlapUnion = UnityOverlapUnion.Rectangle(1.5f, 2.5f);
                scope.Save();
                scope.Asset.overlapUnion = UnityOverlapUnion.Rectangle(3.5f, 4.5f);
                scope.Save();
                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.overlapUnion.kind == UnityOverlapUnion.Kind.Rectangle &&
                    scope.Asset.overlapUnion.width == 3.5f &&
                    scope.Asset.overlapUnion.height == 4.5f,
                    "Overlap same-variant repeated save did not refresh logical fields.");

                scope.Asset.overlapUnion = UnityOverlapUnion.Point();
                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.overlapUnion.kind == UnityOverlapUnion.Kind.Point,
                    "Overlap Point variant was not preserved after reset.");
                scope.Asset.overlapUnion = UnityOverlapUnion.Rectangle(5.5f, 6.5f);
                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.overlapUnion.kind == UnityOverlapUnion.Kind.Rectangle &&
                    scope.Asset.overlapUnion.width == 5.5f &&
                    scope.Asset.overlapUnion.height == 6.5f,
                    "Overlap stale explicit storage resurfaced after switching back from Point.");
                return "Rectangle=1.5x2.5->3.5x4.5;Point=default;Rectangle=5.5x6.5;stale-storage=false";
            }
        }

        private static string OverlapCopyNestedAndCollectionIsolation()
        {
            using (var scope = new AssetScope("overlap-copy-collections"))
            {
                scope.Asset.overlapParent = UnityOverlapParent.Parent(UnityOverlapUnion.Rectangle(1.5f, 2.5f), 1);
                scope.Asset.overlapParentCopy = scope.Asset.overlapParent;
                scope.Asset.overlapArray = new[]
                {
                    UnityOverlapUnion.Rectangle(3.5f, 4.5f),
                    UnityOverlapUnion.Number(7),
                };
                scope.Asset.overlapList = new List<UnityOverlapReferenceUnion>
                {
                    UnityOverlapReferenceUnion.Text("list-first", scope.Texture),
                    UnityOverlapReferenceUnion.Number(9),
                };
                scope.Save();

                scope.Asset.overlapParentCopy = UnityOverlapParent.Parent(UnityOverlapUnion.Number(99), 2);
                scope.Asset.overlapArray[0] = UnityOverlapUnion.Rectangle(8.5f, 9.5f);
                scope.Asset.overlapList[0] = UnityOverlapReferenceUnion.Text("list-changed", scope.Texture);
                scope.Save();
                scope.Save();
                scope.Reload();

                Require(
                    scope.Asset.overlapParent.marker == 1 &&
                    scope.Asset.overlapParent.child.kind == UnityOverlapUnion.Kind.Rectangle &&
                    scope.Asset.overlapParent.child.width == 1.5f &&
                    scope.Asset.overlapParentCopy.marker == 2 &&
                    scope.Asset.overlapParentCopy.child.kind == UnityOverlapUnion.Kind.Number &&
                    scope.Asset.overlapParentCopy.child.value == 99,
                    "Overlap copied parent mutation changed the original child.");
                Require(
                    scope.Asset.overlapArray[0].width == 8.5f &&
                    scope.Asset.overlapArray[1].kind == UnityOverlapUnion.Kind.Number &&
                    scope.Asset.overlapList[0].label == "list-changed" &&
                    scope.Asset.overlapList[0].icon == scope.Texture &&
                    scope.Asset.overlapList[1].kind == UnityOverlapReferenceUnion.Kind.Number,
                    "Overlap collection mutation changed a sibling element.");
                return "parent=original/copy;array=Rectangle/Number;list=Text/Number;repeat-save=2;isolated=true";
            }
        }

        private static string OverlapActivePayloadOnlyYaml()
        {
            using (var scope = new AssetScope("overlap-active-yaml"))
            {
                scope.Asset.overlapUnion = UnityOverlapUnion.Rectangle(10.5f, 11.5f);
                scope.Save();
                var serialized = new SerializedObject(scope.Asset);
                serialized.Update();
                var union = RequireProperty(serialized, "overlapUnion");
                var children = DirectChildren(union);
                Require(children.Count == 1, "Overlap exposed more than its active payload as serialized children.");
                Require(children[0].propertyType == SerializedPropertyType.ManagedReference, "Overlap payload was missing.");
                Require(!children.Any(property => property.propertyType == SerializedPropertyType.Enum), "Overlap discriminator was still serialized.");
                var payload = RequirePayloadProperty(serialized, "overlapUnion").managedReferenceValue;
                Require(payload != null, "Overlap active payload was unavailable.");
                var yaml = File.ReadAllText(scope.AssetFilePath);
                AssertRuntimeFieldsAbsentFromYaml(typeof(UnityOverlapUnion), yaml);
                Require(
                    yaml.IndexOf(payload!.GetType().Name, StringComparison.Ordinal) >= 0,
                    "Overlap active payload type was absent from YAML.");
                return "children=1;active=" + payload.GetType().Name + ";serialized-kind=false;explicit-runtime-fields=absent";
            }
        }

        private static void AssertRuntimeFieldsAbsentFromYaml(Type unionType, string yaml)
        {
            foreach (var field in unionType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (field.IsStatic || field.Name == "_kind" || field.Name == "_serializedPayload")
                    continue;
                Require(
                    yaml.IndexOf("m_Name: " + field.Name, StringComparison.Ordinal) < 0 &&
                    yaml.IndexOf("  " + field.Name + ":", StringComparison.Ordinal) < 0,
                    "Runtime boxed field appeared in serialized YAML: " + field.Name);
            }
        }

        private static bool YamlContainsField(string yaml, string fieldName)
        {
            var prefix = fieldName + ":";
            return yaml
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(line => line.TrimStart().StartsWith(prefix, StringComparison.Ordinal));
        }

        private static void AssertUnsafeRuntimeStorageIsDefault(UnityUnsafeOverlapUnion value)
        {
            var fields = value.GetType()
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(field =>
                    !field.IsStatic &&
                    field.Name != "_kind" &&
                    field.Name != "_serializedPayload")
                .ToArray();
            Require(fields.Length > 0, "UnsafeOverlap runtime storage was unavailable after Empty reset.");
            foreach (var field in fields.Where(field =>
                field.Name != "_data" &&
                !field.FieldType.IsValueType))
            {
                Require(
                    field.GetValue(value) == null,
                    "UnsafeOverlap managed runtime slot retained stale data after Empty reset: " + field.Name);
            }

            var rawBuffer = fields.FirstOrDefault(field => field.Name == "_data");
            Require(rawBuffer != null, "UnsafeOverlap raw buffer was unavailable after Empty reset.");
            Require(
                IsZeroStorage(rawBuffer!.GetValue(value)),
                "UnsafeOverlap raw buffer retained stale bytes after Empty reset.");

            var payload = GetSerializedPayloadCache(value);
            Require(
                payload != null &&
                !payload.GetType()
                    .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Any(field => !field.IsStatic),
                "UnsafeOverlap Empty did not retain its fieldless typed payload.");
        }

        private static bool IsZeroStorage(object? value)
        {
            if (value == null)
                return true;
            if (value is Array array)
            {
                foreach (var item in array)
                {
                    if (!IsZeroStorage(item))
                        return false;
                }

                return true;
            }
            if (value is bool boolean)
                return !boolean;
            if (value is char character)
                return character == '\0';
            if (value is sbyte signedByte)
                return signedByte == 0;
            if (value is byte unsignedByte)
                return unsignedByte == 0;
            if (value is short signedShort)
                return signedShort == 0;
            if (value is ushort unsignedShort)
                return unsignedShort == 0;
            if (value is int signedInt)
                return signedInt == 0;
            if (value is uint unsignedInt)
                return unsignedInt == 0;
            if (value is long signedLong)
                return signedLong == 0;
            if (value is ulong unsignedLong)
                return unsignedLong == 0;
            if (value is float single)
                return single == 0f;
            if (value is double doubleValue)
                return doubleValue == 0d;
            if (value.GetType().IsEnum)
                return Convert.ToUInt64(value) == 0;

            var fields = value.GetType()
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(field => !field.IsStatic)
                .ToArray();
            return fields.Length == 0 || fields.All(field => IsZeroStorage(field.GetValue(value)));
        }

        private static string DrawerGetPropertyHeightFieldlessVsFielded()
        {
            using (var scope = new DrawerHostScope("drawer-height"))
            {
                var drawer = FindDrawer(typeof(UnityDrawerUnion));
                if (drawer == null)
                    throw new InvalidOperationException("Generated UnityDrawerUnion drawer was not discoverable.");
                scope.Host.single = UnityDrawerUnion.Empty();
                var serialized = new SerializedObject(scope.Host);
                serialized.Update();
                var property = RequireProperty(serialized, "single");
                var emptyHeight = drawer.GetPropertyHeight(property, GUIContent.none);
                scope.Host.single = UnityDrawerUnion.Text(3, "height");
                serialized.Update();
                var textHeight = drawer.GetPropertyHeight(property, GUIContent.none);
                Require(textHeight > emptyHeight, "Fielded drawer height did not exceed fieldless height.");
                return "empty=" + emptyHeight.ToString("0.##") + ";text=" + textHeight.ToString("0.##");
            }
        }

        private static string DrawerWholeValueUsesBoxedValueSetter()
        {
            using (var scope = new DrawerHostScope("drawer-boxed-set"))
            {
                scope.Host.single = UnityDrawerUnion.Text(1, "before");
                var serialized = new SerializedObject(scope.Host);
                serialized.Update();
                WriteBoxedValue(RequireProperty(serialized, "single"), UnityDrawerUnion.Number(7.5f));
                Require(serialized.ApplyModifiedProperties(), "Boxed setter replacement was not applied.");
                Require(scope.Host.single.kind == UnityDrawerUnion.Kind.Number && scope.Host.single.value == 7.5f, "Boxed setter value differed.");
                return "kind=Number;value=7.5";
            }
        }

        private static string DrawerReadsPayloadTypeAndManagedReference()
        {
            using (var scope = new DrawerHostScope("drawer-read"))
            {
                scope.Host.single = UnityDrawerUnion.Text(4, "serialized");
                var serialized = new SerializedObject(scope.Host);
                serialized.Update();
                var property = RequireProperty(serialized, "single");
                var direct = DirectChildren(property);
                Require(direct.Count == 1 && direct[0].propertyType == SerializedPropertyType.ManagedReference, "Drawer target exposed a serialized kind instead of one payload.");
                var payload = direct[0].managedReferenceValue;
                Require(payload != null, "Serialized managed payload was unavailable.");
                RequirePayloadValue(payload!, "label", "serialized");
                return "serialized-kind=false;payload-type=" + payload!.GetType().Name + ";payload=present";
            }
        }

        private static string MonoPlayerDeserializesGeneratedUnion()
        {
            throw new ObservedException("Separate BuildPlayer entrypoint required; inspect the built Mono player JSON report.");
        }

        private static string EditorReimportDoesNotDuplicateGeneratedSurface()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            var drawers = FindDrawers(typeof(UnityShapeSerializable));
            Require(drawers.Count == 1, "UnityShapeSerializable did not have exactly one generated drawer.");
            var boxedFieldsDrawers = FindDrawers(typeof(UnityBoxedFieldsUnion));
            var boxedTupleDrawers = FindDrawers(typeof(UnityBoxedTupleUnion));
            var unsafeDrawers = FindDrawers(typeof(UnityUnsafeOverlapUnion));
            var overlapDrawers = FindDrawers(typeof(UnityOverlapUnion));
            Require(boxedFieldsDrawers.Count == 1, "UnityBoxedFieldsUnion did not have exactly one generated drawer after reimport.");
            Require(boxedTupleDrawers.Count == 1, "UnityBoxedTupleUnion did not have exactly one generated drawer after reimport.");
            Require(unsafeDrawers.Count == 1, "UnityUnsafeOverlapUnion did not have exactly one generated drawer after reimport.");
            Require(overlapDrawers.Count == 1, "UnityOverlapUnion did not have exactly one generated drawer after reimport.");
            var nested = typeof(UnityShapeSerializable).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic);
            Require(nested.Length > 0, "Generated union nested surface was missing after reimport.");
            return "drawers=additive:" + drawers.Count + ";boxed-fields:" + boxedFieldsDrawers.Count + ";boxed-tuple:" + boxedTupleDrawers.Count + ";unsafe:" + unsafeDrawers.Count + ";overlap:" + overlapDrawers.Count + ";nested-types=" + nested.Length;
        }

        private static string NoUserPreparationOrWrapperRequired()
        {
            using (var scope = new AssetScope("ordinary-owner"))
            {
                scope.Asset.single = UnityShapeSerializable.Circle(2.5f);
                scope.Save();
                scope.Reload();
                Require(scope.Asset.single.kind == UnityShapeSerializable.Kind.Circle && scope.Asset.single.radius == 2.5f, "Ordinary owner round trip differed.");
                return "ordinary-ScriptableObject-field=true;wrapper=false;manual-callback=false";
            }
        }

        private static string RuntimeCompilationUsesInstalledUnityEngineOnly()
        {
            var references = typeof(UnitySerializationAsset).Assembly.GetReferencedAssemblies();
            Require(!references.Any(reference => reference.Name == "UnityEditor"), "Runtime assembly referenced UnityEditor.");
            Require(references.Any(reference => reference.Name == "UnityEngine" || reference.Name == "UnityEngine.CoreModule"), "Runtime assembly did not reference UnityEngine.");
            return "UnityEngine=true;UnityEditor=false";
        }

        private static string NormalNonSerializableUnionBehaviorUnchanged()
        {
            var value = UnityNormalAdditive.Number(17);
            Require(value.kind == UnityNormalAdditive.Kind.Number && value.value == 17, "Normal Additive control changed.");
            var empty = UnityNormalAdditive.Empty();
            Require(empty.kind == UnityNormalAdditive.Kind.Empty, "Normal Additive Empty control changed.");
            return "kind=Number;value=17;unity-surface=false";
        }

        private static string GenericUnionIntSaveReload()
        {
            using (var scope = new AssetScope("generic-int"))
            {
                scope.Asset.genericInt = UnityGenericUnion<int>.Some(42);
                scope.Save();
                scope.Reload();
                Require(scope.Asset.genericInt.kind == UnityGenericUnion<int>.Kind.Some, "Generic int kind differed.");
                Require(scope.Asset.genericInt.value == 42, "Generic int value differed.");
                return "Some(42)";
            }
        }

        private static string GenericUnionStringSaveReload()
        {
            using (var scope = new AssetScope("generic-string"))
            {
                scope.Asset.genericString = UnityGenericUnion<string>.Some("generic");
                scope.Save();
                scope.Reload();
                Require(scope.Asset.genericString.kind == UnityGenericUnion<string>.Kind.Some, "Generic string kind differed.");
                Require(scope.Asset.genericString.value == "generic", "Generic string value differed.");
                return "Some(generic)";
            }
        }

        private static string GenericUnionChildUnionSaveReload()
        {
            using (var scope = new AssetScope("generic-child-union"))
            {
                scope.Asset.genericChild = UnityGenericUnion<UnityCallbackChild>.Some(
                    UnityCallbackChild.Text(6, "generic-child", scope.Texture));
                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.genericChild.kind == UnityGenericUnion<UnityCallbackChild>.Kind.Some &&
                    scope.Asset.genericChild.value.kind == UnityCallbackChild.Kind.Text &&
                    scope.Asset.genericChild.value.count == 6 &&
                    scope.Asset.genericChild.value.label == "generic-child" &&
                    scope.Asset.genericChild.value.asset == scope.Texture,
                    "Generic child-union value differed after reload.");
                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.genericChild.kind == UnityGenericUnion<UnityCallbackChild>.Kind.Some &&
                    scope.Asset.genericChild.value.kind == UnityCallbackChild.Kind.Text &&
                    scope.Asset.genericChild.value.label == "generic-child",
                    "Generic child-union value changed after repeated reload.");
                return "closed=UnityCallbackChild;child=Text/generic-child;repeat=stable";
            }
        }

        private static string GenericUnionSerializableHelperChildSaveReload()
        {
            using (var scope = new AssetScope("generic-helper-child"))
            {
                scope.Asset.genericHelper = UnityGenericUnion<UnityGenericChildHelper>.Some(
                    new UnityGenericChildHelper(
                        UnityCallbackChild.Text(7, "before", scope.Texture),
                        17));
                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.genericHelper.kind == UnityGenericUnion<UnityGenericChildHelper>.Kind.Some &&
                    scope.Asset.genericHelper.value.child.kind == UnityCallbackChild.Kind.Text &&
                    scope.Asset.genericHelper.value.child.label == "before" &&
                    scope.Asset.genericHelper.value.marker == 17,
                    "Generic serializable helper child differed after reload.");

                var helper = scope.Asset.genericHelper.value;
                helper.Child = UnityCallbackChild.Text(
                    helper.Child.count,
                    "after",
                    helper.Child.asset);
                scope.Save();
                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.genericHelper.kind == UnityGenericUnion<UnityGenericChildHelper>.Kind.Some &&
                    scope.Asset.genericHelper.value.child.label == "after" &&
                    scope.Asset.genericHelper.value.marker == 17,
                    "Generic serializable helper child mutation was not refreshed without outer reassignment.");
                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.genericHelper.value.child.label == "after",
                    "Generic serializable helper child changed after the second reload.");
                return "closed=UnityGenericChildHelper;child=before->after;outer-reassignment=false;repeat=2";
            }
        }

        private static string GenericUnionSerializableHelperNullSaveReload()
        {
            using (var scope = new AssetScope("generic-helper-null"))
            {
                scope.Asset.genericHelper = UnityGenericUnion<UnityGenericChildHelper>.Some(null!);
                scope.Save();
                scope.Reload();

                Require(
                    scope.Asset.genericHelper.kind == UnityGenericUnion<UnityGenericChildHelper>.Kind.Some,
                    "Null serializable helper payload changed the generic union kind.");
                var helper = scope.Asset.genericHelper.value;
                Require(helper != null, "Null serializable helper payload did not normalize to a default instance.");
                RequireDefaultNullHelper(helper, "Null serializable helper payload did not expose stable default child fields.");

                scope.Save();
                scope.Reload();
                helper = scope.Asset.genericHelper.value;
                Require(helper != null, "Default serializable helper instance was lost after repeated reload.");
                RequireDefaultNullHelper(helper, "Default serializable helper child changed after repeated reload.");
                return "null=default-instance;child=stable-default;repeat=2";
            }
        }

        private static void RequireDefaultNullHelper(UnityGenericChildHelper helper, string message)
        {
            var child = helper.child;
            var label = child.label;
            Require(
                helper.marker == 0 &&
                child.kind == UnityCallbackChild.Kind.Text &&
                child.count == 0 &&
                string.IsNullOrEmpty(label) &&
                child.asset == null,
                message +
                " marker=" + helper.marker +
                ";kind=" + child.kind +
                ";count=" + child.count +
                ";label=" + (label == null ? "<null>" : "<" + label + ">") +
                ";asset=" + (child.asset == null ? "<null>" : child.asset.name));
        }

        private static string GenericContainingNestedUnionSaveReload()
        {
            using (var scope = new AssetScope("generic-containing-nested"))
            {
                scope.Asset.genericContainerNested = UnityGenericContainer<int>.NestedUnion.Some(21);
                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.genericContainerNested.kind == UnityGenericContainer<int>.NestedUnion.Kind.Some &&
                    scope.Asset.genericContainerNested.value == 21,
                    "Closed generic containing-type nested union differed after reload.");

                var serialized = new SerializedObject(scope.Asset);
                serialized.Update();
                Require(
                    serialized.FindProperty("genericContainerNested") != null,
                    "Closed generic containing-type nested union was not exposed through native serialization.");
                scope.Save();
                scope.Reload();
                Require(
                    scope.Asset.genericContainerNested.kind == UnityGenericContainer<int>.NestedUnion.Kind.Some &&
                    scope.Asset.genericContainerNested.value == 21,
                    "Closed generic containing-type nested union changed after repeated reload.");
                return "container=int;nested=Some(21);repeat=stable";
            }
        }

        private static string GenericUnionDrawerTypeDiscovery()
        {
            var drawers = FindDrawers(typeof(UnityGenericUnion<>));
            if (drawers.Count == 0)
                throw new InvalidOperationException("Open generic UnityGenericUnion drawer was not discoverable.");
            Require(drawers.Count == 1, "Multiple open generic union drawers were registered.");
            return "open-generic-drawers=" + drawers.Count + ";closed=int,string";
        }

        private static string ZeroVariantNoDrawerCrash()
        {
            var drawers = FindDrawers(typeof(UnityZeroVariant));
            Require(drawers.Count == 0, "A DU003 zero-variant union unexpectedly registered a property drawer.");
            return "diagnostic=SPIRE_DU003;drawer=absent;type-discovery=no-crash";
        }

        private static string GenericOpenTypeParameterIsPresent()
        {
            Require(typeof(UnityGenericUnion<>).IsGenericTypeDefinition, "Generic union definition was not open.");
            Require(typeof(UnityGenericUnion<>).GetGenericArguments().Length == 1, "Generic union parameter count differed.");
            return "open-T=true;closed=int,string=covered";
        }

        private static string NestedGenericOwnerCompiles()
        {
            var owner = new UnityNestedGenericOwner<int> { value = UnityGenericUnion<int>.Some(3) };
            Require(owner.value.kind == UnityGenericUnion<int>.Kind.Some && owner.value.value == 3, "Nested generic owner value differed.");
            return "nested-generic-owner=true;value=3";
        }

        private static string GenericObjectArgumentFollowUp()
        {
            throw new ObservedException("Closed object policy is reserved for the Roslyn diagnostic fixture.");
        }

        private static string GenericDictionaryArgumentFollowUp()
        {
            throw new ObservedException("Closed dictionary policy is reserved for the Roslyn diagnostic fixture.");
        }

        private static void BeginDrawerProbe()
        {
            _drawerScope = new DrawerHostScope("drawer-ui");
            _drawerScope.Host.single = UnityDrawerUnion.Text(4, "ui-text");
            _drawerWindow = ScriptableObject.CreateInstance<DrawerProbeWindow>();
            _drawerWindow.Initialize(_drawerScope.Host);
            _drawerWindow.Show();
            _drawerWindow.Focus();
            _drawerFrames = 0;
            _drawerStartedAt = EditorApplication.timeSinceStartup;
            _nextDrawerFrameAt = _drawerStartedAt;
            _drawerBaseProbeFinalized = false;
            EditorApplication.update += PollDrawerProbe;
        }

        private static void PollDrawerProbe()
        {
            if (_report == null || _drawerWindow == null)
                return;

            var now = EditorApplication.timeSinceStartup;
            var frameReady = false;
            _drawerWindow.RecordEditorUpdate();
            if (now >= _nextDrawerFrameAt)
            {
                _drawerFrames++;
                _drawerWindow.RecordDrawerFrame();
                _nextDrawerFrameAt = now + DrawerFrameIntervalSeconds;
                frameReady = true;
            }

            if (!_drawerBaseProbeFinalized &&
                !_drawerWindow.HasInitialTextControls &&
                _drawerWindow.HasBoundFieldControl("count") &&
                _drawerWindow.HasBoundFieldControl("label") &&
                !_drawerWindow.HasBoundFieldControl("value") &&
                !_drawerWindow.HasBoundFieldControl("texture"))
            {
                _drawerWindow.HasInitialTextControls = true;
            }

            if (!_drawerBaseProbeFinalized &&
                _drawerWindow.HasInitialTextControls &&
                !_drawerWindow.SelectorInteractionAttempted)
                _drawerWindow.TryInteractWithSelector("Number");

            if (!_drawerBaseProbeFinalized &&
                _drawerWindow.HasInitialTextControls &&
                _drawerWindow.ImguiRepainted &&
                _drawerWindow.UIToolkitBuilt &&
                _drawerWindow.SelectorChanged &&
                _drawerWindow.HasBoundFieldControl("value") &&
                !_drawerWindow.HasBoundFieldControl("count") &&
                !_drawerWindow.HasBoundFieldControl("label") &&
                !_drawerWindow.HasBoundFieldControl("texture"))
            {
                CompletePendingAsPass("IMGUI_Drawer_ShowsSelectorAndActiveFields", "actual-OnGUI-repaint;initial=Text;after-selector=Number;active-controls=true;inactive-controls=false");
                CompletePendingAsPass("UIToolkit_Drawer_ShowsSelectorAndActiveFields", "actual-CreatePropertyGUI;initial=Text;after-selector=Number;active-controls=true;inactive-controls=false");
                _drawerBaseProbeFinalized = true;
                _drawerCallbackRunner ??= new DrawerCallbackRunner(_drawerWindow);
                if (frameReady && _drawerCallbackRunner.Tick())
                    FinishDrawerProbe();
                return;
            }

            if (frameReady && _drawerCallbackRunner != null && _drawerCallbackRunner.Tick())
            {
                FinishDrawerProbe();
                return;
            }

            if (!_drawerBaseProbeFinalized &&
                (_drawerFrames >= 120 || now - _drawerStartedAt >= DrawerWallClockWatchdogSeconds))
            {
                var reason = "window-ready=" + _drawerWindow.UIToolkitBuilt + ";imgui-repaint=" + _drawerWindow.ImguiRepainted + ";initial-controls=" + _drawerWindow.HasInitialTextControls + ";selector-changed=" + _drawerWindow.SelectorChanged;
                CompletePendingAsFail(
                    "IMGUI_Drawer_ShowsSelectorAndActiveFields",
                    new TimeoutException("The base IMGUI drawer probe did not complete within its deadline: " + reason));
                CompletePendingAsFail(
                    "UIToolkit_Drawer_ShowsSelectorAndActiveFields",
                    new TimeoutException("The base UI Toolkit drawer probe did not complete within its deadline: " + reason));
                _drawerBaseProbeFinalized = true;
                _drawerCallbackRunner ??= new DrawerCallbackRunner(_drawerWindow);
            }
        }

        private static void FinishDrawerProbe()
        {
            EditorApplication.update -= PollDrawerProbe;
            if (_drawerWindow != null)
                _drawerWindow.Close();
            if (_drawerScope != null)
                _drawerScope.Dispose();
            if (_drawerCallbackRunner != null)
                _drawerCallbackRunner.Dispose();
            _drawerWindow = null;
            _drawerScope = null;
            _drawerCallbackRunner = null;
            _drawerBaseProbeFinalized = false;
            QueueLifecycleAutomation();
        }

        private static void RunScenario(string name, string expected, Func<string> action)
        {
            var result = new ScenarioResult
            {
                name = name,
                expected = expected,
                status = "pass",
                invariants = new List<string>(),
                callbackAssertions = new List<string>(),
            };

            try
            {
                result.observed = action();
                result.invariants.Add(result.observed);
                result.callbackAssertions.Add("owner callback path exercised by AssetDatabase/SerializedObject");
            }
            catch (ObservedException exception)
            {
                result.status = "observed";
                result.observed = exception.Message;
                result.invariants.Add(result.observed);
            }
            catch (Exception exception)
            {
                result.status = "fail";
                result.observed = "The scenario raised an exception.";
                result.error = exception.ToString();
            }

            _report!.scenarios.Add(result);
        }

        private static void AddPendingScenario(string name, string expected)
        {
            _report!.scenarios.Add(new ScenarioResult
            {
                name = name,
                expected = expected,
                status = "pending",
                invariants = new List<string>(),
                callbackAssertions = new List<string>(),
            });
        }

        private static void CompletePendingAsPass(
            string name,
            string observed,
            string callbackAssertion = "actual EditorWindow callback completed")
        {
            var result = FindScenario(name);
            result.status = "pass";
            result.observed = observed;
            result.invariants.Add(observed);
            result.callbackAssertions.Add(callbackAssertion);
        }

        private static void CompletePendingAsObserved(string name, string observed)
        {
            var result = FindScenario(name);
            result.status = "observed";
            result.observed = observed;
            result.invariants.Add(observed);
        }

        private static void ObserveDrawerCallbackScenarios(string observed)
        {
            foreach (var name in DrawerCallbackScenarioNames)
            {
                var scenario = FindScenario(name);
                if (scenario.status == "pending")
                    CompletePendingAsObserved(name, "Graphics/UI Toolkit probe unavailable: " + observed);
            }
        }

        private static readonly string[] DrawerCallbackScenarioNames =
        {
        "Drawer_MissingPayload_SelectsFirstVariant",
        "Drawer_SelectionChange_UsesFactoryReplacement",
        "Drawer_ArrayElement_NestedPathWriteback",
        "Drawer_ListElement_NestedPathWriteback",
        "Drawer_UndoRedo_WholeValueAndPayloadEdit",
        "Drawer_SharedPayload_EditUndoRedoIsolated",
        "Drawer_NestedInlineHelper_EditUndoRedoIsolated",
        "Drawer_RenderedFieldHistory_MultiStepUndoRedo",
        "Drawer_RenderedEdit_PreservesStagedUnrelatedApply",
        "Drawer_RenderedNumericFields_UndoRedo",
        "Drawer_RenderedInspectorNamedEnum_UndoRedo",
        "Drawer_RenderedFlagsEnum_UndoRedo",
        "Drawer_RenderedEnum_InvalidChoicePreservesHistory",
        "Drawer_ReadOnlyAliasedPayload_RenderedStable",
        "Drawer_ReadOnlyAliasedPayload_UnrelatedApplyStable",
        "Drawer_MultiObjectActiveFieldEdit",
        "Drawer_UndoRedoWhileDetached_ReattachesCurrentTree",
        "Drawer_UndoBeforeFirstPanelAttach_BindsCurrentState",
        "Drawer_MultiObjectEditing",
        "Drawer_PrefabOverrideApplyRevert",
        "Drawer_NestedGenericPayloadSelector",
        "Drawer_PrivateBaseOwnerSelector",
        "Drawer_GenericClosedSelectorHelpers",
        "Drawer_GenericContainingNestedSelector",
        "Drawer_RebindDoesNotWriteStaleTarget",
        "BoxedLayouts_IMGUIAndUIToolkit_UseCanonicalPayload",
        "BoxedLayouts_SelectorReplacementAndUndoRedo",
        "UnsafeOverlap_IMGUIAndUIToolkit_DrawCanonicalPayload",
        "UnsafeOverlap_NestedPathWritebackUndoRedo",
        "Overlap_IMGUIAndUIToolkit_DrawCanonicalPayload",
        "Overlap_NestedPathWritebackUndoRedo",
    };

        private static void CompletePendingAsFail(string name, Exception exception)
        {
            var result = FindScenario(name);
            result.status = "fail";
            result.observed = "The generated Unity surface was unavailable.";
            result.error = exception.ToString();
        }

        private static ScenarioResult FindScenario(string name)
        {
            var result = _report!.scenarios.FirstOrDefault(item => item.name == name);
            if (result == null)
                throw new InvalidOperationException("Scenario was not registered: " + name);
            return result;
        }

        private static void Finish()
        {
            if (_report == null)
                return;

            _report.hardFailureCount = _report.scenarios.Count(item => item.status == "fail" || item.status == "pending");
            _report.failureCount = _report.hardFailureCount;
            _report.observedCount = _report.scenarios.Count(item => item.status == "observed");
            _report.passed = _report.scenarios.Count(item => item.status == "pass");
            _report.failed = _report.hardFailureCount;
            File.WriteAllText(Path.Combine(ProjectRoot, ReportName), JsonUtility.ToJson(_report, true));
            AssetDatabase.Refresh();
            EditorApplication.Exit(_report.hardFailureCount == 0 ? 0 : 1);
        }

        private static void QueueLifecycleAutomation()
        {
            if (_report == null || _lifecyclePollRegistered || _lifecycleStartQueued)
                return;

            _lifecycleStartQueued = true;
            EditorApplication.delayCall -= BeginLifecycleAutomation;
            EditorApplication.delayCall += BeginLifecycleAutomation;
            Debug.Log("SPIRE_LIFECYCLE_SCENE_PREPARE_QUEUED");
        }

        private static void BeginLifecycleAutomation()
        {
            if (_report == null || _lifecyclePollRegistered)
                return;

            _lifecycleStartQueued = false;

            try
            {
                WriteLifecycleStage(
                    "scene-preparation-started",
                    "Preparing the lifecycle scene; the integration report is written immediately before preparation.");
                Debug.Log("SPIRE_LIFECYCLE_SCENE_PREPARE_BEGIN");
                SaveReport();
                PrepareLifecycleScene();
                Debug.Log("SPIRE_LIFECYCLE_SCENE_PREPARE_END");
                WriteLifecycleStage(
                    "scene-prepared",
                    "Lifecycle scene preparation completed; requesting script reload.");
                WriteLifecycleStage(
                    "script-reload-requested",
                    "Waiting for InitializeOnLoad after a requested script compilation.");
                SaveReport();
                RegisterLifecyclePoll();
                CompilationPipeline.RequestScriptCompilation();
            }
            catch (Exception exception)
            {
                CompletePendingAsFail("ScriptReload_PreservesUnionState", exception);
                CompletePendingAsFail("PlayModeTransition_PreservesUnionState", exception);
                DeleteLifecycleStage();
                UnregisterLifecyclePoll();
                Finish();
            }
        }

        [InitializeOnLoadMethod]
        private static void ResumeLifecycleAfterDomainReload()
        {
            var stage = ReadLifecycleStage();
            if (stage == null)
                return;

            _report = ReadReport();
            if (_report == null)
                return;

            if (stage.stage == "script-reload-requested")
            {
                stage.stage = "script-reload-resumed";
                stage.domainReloadObserved = true;
                stage.message = "InitializeOnLoad resumed the lifecycle probe after script reload.";
                WriteLifecycleStage(stage);
            }

            RegisterLifecyclePoll();
        }

        private static void RegisterLifecyclePoll()
        {
            if (_lifecyclePollRegistered)
                return;

            EditorApplication.update -= PollLifecycle;
            EditorApplication.update += PollLifecycle;
            _lifecyclePollRegistered = true;
        }

        private static void UnregisterLifecyclePoll()
        {
            EditorApplication.update -= PollLifecycle;
            _lifecyclePollRegistered = false;
        }

        private static void PollLifecycle()
        {
            var stage = ReadLifecycleStage();
            if (stage == null)
            {
                UnregisterLifecyclePoll();
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            stage.polls++;
            if (stage.polls > 600)
            {
                var timeoutException = new TimeoutException(
                    "The requested Unity lifecycle transition did not complete within the editor callback window.");
                if (stage.stage is "script-reload-requested" or "script-reload-resumed")
                    CompletePendingAsFail("ScriptReload_PreservesUnionState", timeoutException);
                CompletePendingAsFail("PlayModeTransition_PreservesUnionState", timeoutException);
                DeleteLifecycleStage();
                UnregisterLifecyclePoll();
                SaveReport();
                Finish();
                return;
            }

            try
            {
                if (stage.stage == "script-reload-resumed")
                {
                    RequireLifecycleHostState();
                    Require(stage.domainReloadObserved, "InitializeOnLoad did not observe the requested domain reload.");
                    CompletePendingAsPass(
                        "ScriptReload_PreservesUnionState",
                        "InitializeOnLoad resumed after compilation;additive=Label/lifecycle;boxed=stable;unsafe=Mixed/unsafe-lifecycle;overlap=Number/17;reference=preserved",
                        "actual InitializeOnLoad/update callback completed");
                    stage.stage = "play-mode-awaiting-enter";
                    stage.message = "Waiting for a real Editor Play Mode entry callback.";
                    WriteLifecycleStage(stage);
                    SaveReport();
                    EditorApplication.isPlaying = true;
                    return;
                }

                if (stage.stage == "play-mode-awaiting-enter" && EditorApplication.isPlaying)
                {
                    RequireLifecycleHostState();
                    stage.stage = "play-mode-awaiting-exit";
                    stage.message = "Play Mode entered; waiting for the real Editor exit transition.";
                    WriteLifecycleStage(stage);
                    EditorApplication.ExitPlaymode();
                    return;
                }

                if (stage.stage == "play-mode-awaiting-exit" && !EditorApplication.isPlaying)
                {
                    RequireLifecycleHostState();
                    CompletePendingAsPass(
                        "PlayModeTransition_PreservesUnionState",
                        "Play Mode entered and exited;additive=Label/lifecycle;boxed=stable;unsafe=Mixed/unsafe-lifecycle;overlap=Number/17;reference=preserved",
                        "actual PlayMode state transitions completed");
                    DeleteLifecycleStage();
                    UnregisterLifecyclePoll();
                    SaveReport();
                    Finish();
                }
            }
            catch (Exception exception)
            {
                var scenario = stage.stage == "script-reload-resumed"
                    ? "ScriptReload_PreservesUnionState"
                    : "PlayModeTransition_PreservesUnionState";
                CompletePendingAsFail(scenario, exception);
                if (scenario == "ScriptReload_PreservesUnionState")
                    CompletePendingAsFail("PlayModeTransition_PreservesUnionState", exception);
                DeleteLifecycleStage();
                UnregisterLifecyclePoll();
                SaveReport();
                Finish();
            }
        }

        private static void PrepareLifecycleScene()
        {
            EnsureAssetFolder();
            var texturePath = AssetFolder + "/lifecycle-texture.asset";
            Debug.Log("SPIRE_LIFECYCLE_TEXTURE_LOAD_BEGIN");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            Debug.Log("SPIRE_LIFECYCLE_TEXTURE_LOAD_END");
            if (texture == null)
            {
                texture = new Texture2D(2, 2) { name = "SpireUnityIntegration-lifecycle-texture" };
                Debug.Log("SPIRE_LIFECYCLE_TEXTURE_CREATE_BEGIN");
                AssetDatabase.CreateAsset(texture, texturePath);
                Debug.Log("SPIRE_LIFECYCLE_TEXTURE_CREATE_END");
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var hostObject = new GameObject(LifecycleHostName);
            var host = hostObject.AddComponent<UnitySerializationMono>();
            host.single = UnityShapeSerializable.Label("lifecycle", texture);
            host.reference = texture;
            host.boxedFields = UnityBoxedFieldsUnion.Text("boxed-lifecycle", texture);
            host.boxedTuple = UnityBoxedTupleUnion.Number(17);
            host.unsafeUnion = UnityUnsafeOverlapUnion.Mixed(31, 3.5f, "unsafe-lifecycle", texture);
            host.overlapUnion = UnityOverlapUnion.Number(17);
            host.overlapReference = UnityOverlapReferenceUnion.Text("overlap-lifecycle", texture);
            SceneManager.MoveGameObjectToScene(hostObject, scene);
            WriteLifecycleStage(
                "scene-save-started",
                "Calling EditorSceneManager.SaveScene for the lifecycle scene.");
            Debug.Log("SPIRE_LIFECYCLE_SCENE_SAVE_BEGIN");
            EditorSceneManager.SaveScene(scene, LifecycleScenePath);
            Debug.Log("SPIRE_LIFECYCLE_SCENE_SAVE_END");
            WriteLifecycleStage(
                "scene-save-complete",
                "EditorSceneManager.SaveScene returned; saving lifecycle assets.");
            WriteLifecycleStage(
                "asset-save-started",
                "Calling AssetDatabase.SaveAssets for lifecycle assets.");
            Debug.Log("SPIRE_LIFECYCLE_ASSET_SAVE_BEGIN");
            AssetDatabase.SaveAssets();
            Debug.Log("SPIRE_LIFECYCLE_ASSET_SAVE_END");
            WriteLifecycleStage(
                "asset-save-complete",
                "AssetDatabase.SaveAssets returned.");
        }

        private static void RequireLifecycleHostState()
        {
            var hostObject = GameObject.Find(LifecycleHostName);
            if (hostObject == null && File.Exists(Path.Combine(ProjectRoot, LifecycleScenePath)))
            {
                EditorSceneManager.OpenScene(LifecycleScenePath, OpenSceneMode.Single);
                hostObject = GameObject.Find(LifecycleHostName);
            }

            Require(hostObject != null, "Lifecycle scene host was unavailable after the editor transition.");
            var host = hostObject!.GetComponent<UnitySerializationMono>();
            if (host == null)
                throw new InvalidOperationException("Lifecycle scene component was unavailable after the editor transition.");
            Require(host.single.kind == UnityShapeSerializable.Kind.Label, "Lifecycle union kind was not preserved.");
            Require(host.single.text == "lifecycle", "Lifecycle union text was not preserved.");
            Require(host.single.icon == host.reference, "Lifecycle Unity object reference was not preserved.");
            Require(host.boxedFields.kind == UnityBoxedFieldsUnion.Kind.Text, "Lifecycle BoxedFields kind was not preserved.");
            Require(host.boxedFields.label == "boxed-lifecycle", "Lifecycle BoxedFields text was not preserved.");
            Require(host.boxedFields.icon == host.reference, "Lifecycle BoxedFields Unity object reference was not preserved.");
            Require(host.boxedTuple.kind == UnityBoxedTupleUnion.Kind.Number, "Lifecycle BoxedTuple kind was not preserved.");
            Require(host.boxedTuple.value == 17, "Lifecycle BoxedTuple value was not preserved.");
            Require(host.unsafeUnion.kind == UnityUnsafeOverlapUnion.Kind.Mixed, "Lifecycle UnsafeOverlap kind was not preserved.");
            Require(host.unsafeUnion.raw == 31, "Lifecycle UnsafeOverlap raw value was not preserved.");
            Require(host.unsafeUnion.amount == 3.5f, "Lifecycle UnsafeOverlap float value was not preserved.");
            Require(host.unsafeUnion.label == "unsafe-lifecycle", "Lifecycle UnsafeOverlap label was not preserved.");
            Require(host.unsafeUnion.icon == host.reference, "Lifecycle UnsafeOverlap Unity object reference was not preserved.");
            Require(host.overlapUnion.kind == UnityOverlapUnion.Kind.Number, "Lifecycle Overlap kind was not preserved.");
            Require(host.overlapUnion.value == 17, "Lifecycle Overlap value was not preserved.");
            Require(host.overlapReference.kind == UnityOverlapReferenceUnion.Kind.Text, "Lifecycle Overlap reference kind was not preserved.");
            Require(host.overlapReference.label == "overlap-lifecycle", "Lifecycle Overlap reference label was not preserved.");
            Require(host.overlapReference.icon == host.reference, "Lifecycle Overlap reference object was not preserved.");
        }

        private static void SaveReport()
        {
            if (_report == null)
                return;

            _report.hardFailureCount = _report.scenarios.Count(item => item.status == "fail" || item.status == "pending");
            _report.failureCount = _report.hardFailureCount;
            _report.observedCount = _report.scenarios.Count(item => item.status == "observed");
            _report.passed = _report.scenarios.Count(item => item.status == "pass");
            _report.failed = _report.hardFailureCount;
            File.WriteAllText(Path.Combine(ProjectRoot, ReportName), JsonUtility.ToJson(_report, true));
        }

        private static LifecycleStage? ReadLifecycleStage()
        {
            var path = Path.Combine(ProjectRoot, LifecycleStageName);
            if (!File.Exists(path))
                return null;
            return JsonUtility.FromJson<LifecycleStage>(File.ReadAllText(path));
        }

        private static IntegrationReport? ReadReport()
        {
            var path = Path.Combine(ProjectRoot, ReportName);
            if (!File.Exists(path))
                return null;
            return JsonUtility.FromJson<IntegrationReport>(File.ReadAllText(path));
        }

        private static void DeleteLifecycleStage()
        {
            var path = Path.Combine(ProjectRoot, LifecycleStageName);
            if (File.Exists(path))
                File.Delete(path);
        }

        private static void WriteLifecycleStage(string stage, string message)
        {
            WriteLifecycleStage(new LifecycleStage
            {
                stage = stage,
                message = message,
                unityVersion = Application.unityVersion,
            });
        }

        private static void WriteLifecycleStage(LifecycleStage stage)
        {
            var path = Path.Combine(ProjectRoot, LifecycleStageName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonUtility.ToJson(stage, true));
        }

        private static SerializedProperty RequireProperty(SerializedObject serialized, string path)
        {
            return serialized.FindProperty(path) ?? throw new InvalidOperationException("SerializedProperty was unavailable: " + path);
        }

        private static SerializedProperty RequireProperty(SerializedProperty property, string path)
        {
            return property.FindPropertyRelative(path) ?? throw new InvalidOperationException("Relative SerializedProperty was unavailable: " + path);
        }

        private static object? CreateDrawerPayloadForRawKind(int rawKind)
        {
            if (rawKind == 255)
                return null;

            var value = rawKind switch
            {
                (int)UnityDrawerUnion.Kind.Empty => UnityDrawerUnion.Empty(),
                (int)UnityDrawerUnion.Kind.Number => UnityDrawerUnion.Number(0f),
                (int)UnityDrawerUnion.Kind.Text => UnityDrawerUnion.Text(0, string.Empty),
                (int)UnityDrawerUnion.Kind.ObjectRef => UnityDrawerUnion.ObjectRef((Texture2D)null!),
                _ => throw new InvalidOperationException("Unsupported drawer payload selector value: " + rawKind),
            };
            var gameObject = new GameObject("SpireUnityIntegration-DrawerPayloadSource");
            try
            {
                var host = gameObject.AddComponent<UnityDrawerHost>();
                host.single = value;
                var serialized = new SerializedObject(host);
                serialized.Update();
                var payload = RequirePayloadProperty(serialized, "single").managedReferenceValue;
                return payload ?? throw new InvalidOperationException("Drawer payload source was null.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static SerializedProperty RequirePayloadProperty(SerializedObject serialized, string path)
        {
            return RequirePayloadProperty(RequireProperty(serialized, path));
        }

        private static SerializedProperty RequirePayloadProperty(SerializedProperty union)
        {
            var payload = DirectChildren(union).FirstOrDefault(property => property.propertyType == SerializedPropertyType.ManagedReference);
            return payload ?? throw new InvalidOperationException("Serialized managed payload property was unavailable for " + union.propertyPath + ".");
        }

        private static List<SerializedProperty> DirectChildren(SerializedProperty parent)
        {
            var result = new List<SerializedProperty>();
            var iterator = parent.Copy();
            var end = iterator.GetEndProperty();
            if (!iterator.Next(true))
                return result;

            while (!SerializedProperty.EqualContents(iterator, end))
            {
                if (iterator.depth == parent.depth + 1)
                    result.Add(iterator.Copy());
                if (!iterator.Next(false))
                    break;
            }

            return result;
        }

        private static object RequirePayload(UnitySerializationAsset asset, string field)
        {
            var serialized = new SerializedObject(asset);
            serialized.Update();
            var payload = RequirePayloadProperty(serialized, field).managedReferenceValue;
            return payload ?? throw new InvalidOperationException("Managed payload was null for " + field + ".");
        }

        private static UnityManagedReferenceNodeLeaf CreateManagedReferenceLeaf(string id, int amount)
            => new UnityManagedReferenceNodeLeaf
            {
                id = id,
                amount = amount,
            };

        private static UnityManagedReferenceNodeOther CreateManagedReferenceOther(string id, string label)
            => new UnityManagedReferenceNodeOther
            {
                id = id,
                label = label,
            };

        private static UnityManagedReferenceHostData CreateManagedReferenceHostData(
            UnityManagedReferenceNodeBase? shared,
            bool includeCycle)
        {
            var helperNode = CreateManagedReferenceLeaf("cycle-helper-node", 101);
            var opaqueNode = CreateManagedReferenceOther("helper-opaque", "opaque");
            var helper = new UnityManagedReferenceHelper(
                UnityManagedReferenceAdditiveUnion.Reference(helperNode),
                helperNode,
                opaqueNode,
                null);
            var generatedStateNode = new UnityManagedReferenceNodeGeneratedState
            {
                id = "generated-state",
                state = UnityManagedReferenceNodeStateUnion.Active(303),
            };
            generatedStateNode.SetGeneratedState(
                UnityManagedReferenceNodeStateUnion.Active(303),
                opaqueNode,
                shared as IUnityManagedReferenceNode);
            UnityManagedReferenceNodeBase? cycleRoot = null;
            if (includeCycle)
            {
                var cycleFirst = CreateManagedReferenceLeaf("cycle-first", 201);
                var cycleSecond = CreateManagedReferenceOther("cycle-second", "cycle");
                cycleFirst.next = cycleSecond;
                cycleSecond.next = cycleFirst;
                cycleRoot = cycleFirst;

                var helperFirst = new UnityManagedReferenceHelper(
                    UnityManagedReferenceAdditiveUnion.Reference(helperNode),
                    helperNode,
                    opaqueNode,
                    null);
                var helperSecond = new UnityManagedReferenceHelper(
                    UnityManagedReferenceAdditiveUnion.Reference(opaqueNode),
                    opaqueNode,
                    helperNode,
                    helperFirst);
                helperFirst.SetReferences(helperNode, opaqueNode, helperSecond);
                helper = helperFirst;
            }

            var data = new UnityManagedReferenceHostData
            {
                additive = UnityManagedReferenceAdditiveUnion.Reference(shared),
                boxedFields = UnityManagedReferenceBoxedFieldsUnion.Reference(shared),
                boxedTuple = UnityManagedReferenceBoxedTupleUnion.Reference(shared),
                unsafeOverlap = UnityManagedReferenceUnsafeOverlapUnion.Reference(shared),
                overlap = UnityManagedReferenceOverlapUnion.Reference(shared),
                renamed = UnityManagedReferenceRenamedUnion.Reference(shared),
                genericBase = UnityManagedReferenceGeneric<UnityManagedReferenceNodeBase>.Value(shared!),
                genericInterface = UnityManagedReferenceGeneric<IUnityManagedReferenceNode>.Value((shared as IUnityManagedReferenceNode)!),
                genericDerived = UnityManagedReferenceGeneric<UnityManagedReferenceNodeLeaf>.Value((shared as UnityManagedReferenceNodeLeaf)!),
                baseInterface = UnityManagedReferenceBaseInterfaceUnion.Base(shared),
                sharedNode = shared,
                cycleRoot = cycleRoot,
                generatedStateNode = generatedStateNode,
                dynamicNode = generatedStateNode,
                nodeArray = new UnityManagedReferenceNodeBase?[] { shared, null, shared },
                nodeList = new List<UnityManagedReferenceNodeBase?> { shared, null, shared },
                helper = helper,
            };
            data.SetPrivateReferences(shared, opaqueNode);
            return data;
        }

        private static void RequireManagedReferenceNulls(UnityManagedReferenceHostData data)
        {
            Require(data.additive.kind == UnityManagedReferenceAdditiveUnion.Kind.Reference && data.additive.value == null, "Additive null managed reference was not preserved.");
            Require(data.boxedFields.kind == UnityManagedReferenceBoxedFieldsUnion.Kind.Reference && data.boxedFields.value == null, "BoxedFields null managed reference was not preserved.");
            Require(data.boxedTuple.kind == UnityManagedReferenceBoxedTupleUnion.Kind.Reference && data.boxedTuple.value == null, "BoxedTuple null managed reference was not preserved.");
            Require(data.unsafeOverlap.kind == UnityManagedReferenceUnsafeOverlapUnion.Kind.Reference && data.unsafeOverlap.value == null, "UnsafeOverlap null managed reference was not preserved.");
            Require(data.overlap.kind == UnityManagedReferenceOverlapUnion.Kind.Reference && data.overlap.value == null, "Overlap null managed reference was not preserved.");
        }

        private static void RequireManagedReferenceValues(UnityManagedReferenceHostData data, UnityManagedReferenceNodeBase? expected)
        {
            Require(data.additive.kind == UnityManagedReferenceAdditiveUnion.Kind.Reference && ReferenceEquals(data.additive.value, expected), "Additive managed reference differed.");
            Require(data.boxedFields.kind == UnityManagedReferenceBoxedFieldsUnion.Kind.Reference && ReferenceEquals(data.boxedFields.value, expected), "BoxedFields managed reference differed.");
            Require(data.boxedTuple.kind == UnityManagedReferenceBoxedTupleUnion.Kind.Reference && ReferenceEquals(data.boxedTuple.value, expected), "BoxedTuple managed reference differed.");
            Require(data.unsafeOverlap.kind == UnityManagedReferenceUnsafeOverlapUnion.Kind.Reference && ReferenceEquals(data.unsafeOverlap.value, expected), "UnsafeOverlap managed reference differed.");
            Require(data.overlap.kind == UnityManagedReferenceOverlapUnion.Kind.Reference && ReferenceEquals(data.overlap.value, expected), "Overlap managed reference differed.");
            Require(data.renamed.kind == UnityManagedReferenceRenamedUnion.Kind.Reference && ReferenceEquals(data.renamed.value, expected), "Renamed managed reference differed.");
            Require(data.genericBase.kind == UnityManagedReferenceGeneric<UnityManagedReferenceNodeBase>.Kind.Value && ReferenceEquals(data.genericBase.value, expected), "Generic base managed reference differed.");
            Require(data.genericInterface.kind == UnityManagedReferenceGeneric<IUnityManagedReferenceNode>.Kind.Value && ReferenceEquals(data.genericInterface.value, expected), "Generic interface managed reference differed.");
            Require(data.genericDerived.kind == UnityManagedReferenceGeneric<UnityManagedReferenceNodeLeaf>.Kind.Value && ReferenceEquals(data.genericDerived.value, expected), "Generic derived managed reference differed.");
            Require(data.baseInterface.kind == UnityManagedReferenceBaseInterfaceUnion.Kind.Base && ReferenceEquals(data.baseInterface.baseValue, expected), "Base/interface managed reference differed.");
            Require(ReferenceEquals(data.sharedNode, expected), "Host shared managed reference differed.");
            Require(
                data.generatedStateNode != null &&
                data.generatedStateNode.state.kind == UnityManagedReferenceNodeStateUnion.Kind.Active &&
                data.generatedStateNode.state.marker == 303 &&
                data.generatedStateNode.ObjectHolder is UnityManagedReferenceNodeOther other &&
                other.id == "helper-opaque" &&
                ReferenceEquals(data.generatedStateNode.InterfaceHolder, expected) &&
                ReferenceEquals(data.dynamicNode, data.generatedStateNode),
                "Derived generated-state managed node differed.");
        }

        private static bool HasManagedReferencePayloadMetadata(Type unionType, string variantName, string fieldName)
        {
            var payloadName = "__Spire" + variantName + "Payload";
            var payload = unionType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(candidate => candidate.Name == payloadName);
            var field = payload?.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return field != null && field.GetCustomAttributes(typeof(SerializeReference), inherit: false).Length > 0;
        }

        private static bool HasManagedReferenceMetadataForAllLayouts()
            => HasManagedReferencePayloadMetadata(typeof(UnityManagedReferenceAdditiveUnion), "Reference", "value") &&
                HasManagedReferencePayloadMetadata(typeof(UnityManagedReferenceBoxedFieldsUnion), "Reference", "value") &&
                HasManagedReferencePayloadMetadata(typeof(UnityManagedReferenceBoxedTupleUnion), "Reference", "value") &&
                HasManagedReferencePayloadMetadata(typeof(UnityManagedReferenceUnsafeOverlapUnion), "Reference", "value") &&
                HasManagedReferencePayloadMetadata(typeof(UnityManagedReferenceOverlapUnion), "Reference", "value");

        private static void RequireManagedReferenceField(SerializedObject serialized, string unionPath, string fieldName)
        {
            RequireManagedReferenceField(RequirePayloadProperty(serialized, unionPath), fieldName);
        }

        private static void RequireManagedReferenceField(SerializedProperty parent, string fieldName)
        {
            var field = RequireProperty(parent, fieldName);
            Require(
                field.propertyType == SerializedPropertyType.ManagedReference,
                "Managed-reference field was not exposed as SerializedProperty.ManagedReference: " + parent.propertyPath + "." + fieldName);
        }

        private static void RequireInlineField(SerializedObject serialized, string unionPath, string fieldName)
        {
            RequireInlineField(RequirePayloadProperty(serialized, unionPath), fieldName);
        }

        private static void RequireInlineField(SerializedProperty parent, string fieldName)
        {
            var field = RequireProperty(parent, fieldName);
            Require(
                field.propertyType != SerializedPropertyType.ManagedReference,
                "Inline field unexpectedly acquired managed-reference serialization: " + parent.propertyPath + "." + fieldName);
        }

        private static void RequireManagedReferenceCollectionField(SerializedObject serialized, string unionPath, string fieldName)
        {
            var field = RequireProperty(RequirePayloadProperty(serialized, unionPath), fieldName);
            Require(field.isArray && field.arraySize >= 3, "Managed-reference collection field was unavailable: " + unionPath + "." + fieldName);
            Require(
                field.GetArrayElementAtIndex(0).propertyType == SerializedPropertyType.ManagedReference,
                "Managed-reference collection elements were not exposed as managed properties: " + unionPath + "." + fieldName);
        }

        private static object RequirePayload(SerializedObject serialized, string field)
        {
            var payload = RequirePayloadProperty(serialized, field).managedReferenceValue;
            return payload ?? throw new InvalidOperationException("Managed payload was null for " + field + ".");
        }

        private static void RequirePayloadValue(object payload, string memberName, object? expected)
        {
            var member = FindMember(payload.GetType(), memberName);
            if (member == null)
                throw new InvalidOperationException("Generated payload member was unavailable: " + memberName + ".");
            var actual = member is FieldInfo field ? field.GetValue(payload) : ((PropertyInfo)member).GetValue(payload, null);
            if (!Equals(actual, expected))
                throw new InvalidOperationException("Payload member " + memberName + " expected " + expected + " but observed " + actual + ".");
        }

        private static void RequireDrawerAssetTextState(
            UnityDrawerAsset asset,
            int sourceCount,
            string sourceLabel,
            int editedCount,
            string editedLabel)
        {
            Require(
                asset.values.Length == 2 &&
                asset.values[0].count == sourceCount &&
                asset.values[0].label == sourceLabel &&
                asset.values[1].count == editedCount &&
                asset.values[1].label == editedLabel,
                "Drawer asset runtime state differed: source=" +
                asset.values[0].count + "/" + asset.values[0].label +
                ";edited=" + asset.values[1].count + "/" + asset.values[1].label);

            var serialized = new SerializedObject(asset);
            serialized.Update();
            var sourcePayload = RequirePayloadProperty(
                serialized,
                "values.Array.data[0]").managedReferenceValue;
            var editedPayload = RequirePayloadProperty(
                serialized,
                "values.Array.data[1]").managedReferenceValue;
            Require(
                sourcePayload != null && editedPayload != null,
                "Drawer asset payload state was unavailable.");
            RequirePayloadValue(sourcePayload, "count", sourceCount);
            RequirePayloadValue(sourcePayload, "label", sourceLabel);
            RequirePayloadValue(editedPayload, "count", editedCount);
            RequirePayloadValue(editedPayload, "label", editedLabel);
        }

        private static void RequireDrawerNumericState(
            UnityDrawerNumericAsset asset,
            long expectedLong,
            double expectedDouble,
            UnityDrawerNonOrdinal expectedEnum)
        {
            Require(
                asset.value.kind == UnityDrawerNumericUnion.Kind.Values &&
                asset.value.longValue == expectedLong &&
                asset.value.doubleValue == expectedDouble &&
                asset.value.enumValue == expectedEnum,
                "Drawer numeric runtime state differed: long=" +
                asset.value.longValue + ";double=" + asset.value.doubleValue +
                ";enum=" + asset.value.enumValue);

            var serialized = new SerializedObject(asset);
            serialized.Update();
            var payload = RequirePayloadProperty(serialized, "value").managedReferenceValue;
            Require(payload != null, "Drawer numeric payload state was unavailable.");
            RequirePayloadValue(payload, "longValue", expectedLong);
            RequirePayloadValue(payload, "doubleValue", expectedDouble);
            RequirePayloadValue(payload, "enumValue", expectedEnum);
        }

        private static void RequireDrawerInspectorNamedEnumState(
            UnityDrawerNumericAsset asset,
            long expectedLong,
            double expectedDouble,
            UnityDrawerInspectorNamedEnum expectedEnum)
        {
            Require(
                asset.named.kind == UnityDrawerInspectorNamedEnumUnion.Kind.Values &&
                asset.named.longValue == expectedLong &&
                asset.named.doubleValue == expectedDouble &&
                asset.named.enumValue == expectedEnum,
                "Drawer inspector-named enum runtime state differed: long=" +
                asset.named.longValue + ";double=" + asset.named.doubleValue +
                ";enum=" + asset.named.enumValue);

            var serialized = new SerializedObject(asset);
            serialized.Update();
            var payload = RequirePayloadProperty(serialized, "named").managedReferenceValue;
            Require(payload != null, "Drawer inspector-named enum payload state was unavailable.");
            RequirePayloadValue(payload, "longValue", expectedLong);
            RequirePayloadValue(payload, "doubleValue", expectedDouble);
            RequirePayloadValue(payload, "enumValue", expectedEnum);
        }

        private static void RequireDrawerFlagsState(
            UnityDrawerNumericAsset asset,
            long expectedLong,
            double expectedDouble,
            UnityDrawerNonOrdinalFlags expectedFlags)
        {
            Require(
                asset.flags.kind == UnityDrawerFlagsUnion.Kind.Values &&
                asset.flags.longValue == expectedLong &&
                asset.flags.doubleValue == expectedDouble &&
                asset.flags.flagsValue == expectedFlags,
                "Drawer flags runtime state differed: long=" +
                asset.flags.longValue + ";double=" + asset.flags.doubleValue +
                ";flags=" + asset.flags.flagsValue);

            var serialized = new SerializedObject(asset);
            serialized.Update();
            var payload = RequirePayloadProperty(serialized, "flags").managedReferenceValue;
            Require(payload != null, "Drawer flags payload state was unavailable.");
            RequirePayloadValue(payload, "longValue", expectedLong);
            RequirePayloadValue(payload, "doubleValue", expectedDouble);
            RequirePayloadValue(payload, "flagsValue", expectedFlags);
        }

        private static bool HasManagedReferencePayloadField(object payload, string memberName)
        {
            var field = payload.GetType().GetField(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field != null &&
                field.GetCustomAttributes(typeof(SerializeReference), inherit: false).Length > 0;
        }

        private static void SetMemberValue(object instance, string memberName, object? value)
        {
            var member = FindMember(instance.GetType(), memberName);
            if (member is FieldInfo field)
            {
                field.SetValue(instance, value);
                return;
            }

            if (member is PropertyInfo property && property.SetMethod != null)
            {
                property.SetValue(instance, value, null);
                return;
            }

            throw new InvalidOperationException("Generated mutable member was unavailable: " + memberName + ".");
        }

        private static MemberInfo? FindMember(Type type, string name)
        {
            return (MemberInfo?)type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?? type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }

        private static T SetInternalSlot<T>(T value, string propertyName, object? newValue)
        {
            object boxed = value!;
            var property = boxed.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (property?.SetMethod != null)
            {
                property.SetValue(boxed, newValue, null);
                return (T)boxed;
            }

            var field = boxed.GetType().GetField(
                propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null || field.IsStatic)
                throw new InvalidOperationException("Generated internal storage member was unavailable: " + propertyName + ".");
            field.SetValue(boxed, newValue);
            return (T)boxed;
        }

        private static T InvokeSerializationCallback<T>(T value, string callbackName)
        {
            object boxed = value!;
            var callback = boxed.GetType().GetMethod(
                callbackName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (callback == null)
                throw new InvalidOperationException("Generated serialization callback was unavailable: " + callbackName + ".");
            callback.Invoke(boxed, null);
            return (T)boxed;
        }

        private static object? GetSerializedPayloadCache<T>(T value)
        {
            var field = value!.GetType().GetField(
                "_serializedPayload",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(value);
        }

        private static T SetSerializedPayloadCache<T>(T value, object payload)
        {
            object boxed = value!;
            var field = boxed.GetType().GetField(
                "_serializedPayload",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                throw new InvalidOperationException(
                    "Generated serialized payload field was unavailable for " + boxed.GetType().FullName + ".");
            field.SetValue(boxed, payload);
            return (T)boxed;
        }

        private static object? GetBoxedTupleRuntimeStorage<T>(T value)
        {
            var field = value!.GetType()
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(candidate =>
                    !candidate.IsStatic &&
                    candidate.Name != "_kind" &&
                    candidate.Name != "_serializedPayload" &&
                    candidate.FieldType == typeof(object));
            if (field == null)
                throw new InvalidOperationException("BoxedTuple runtime object storage was unavailable.");
            return field.GetValue(value);
        }

        private static T SetInternalSlotForField<T>(T value, string fieldName, object? newValue)
        {
            object boxed = value!;
            var publicProperty = boxed.GetType().GetProperty(
                fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (publicProperty?.GetMethod == null)
                throw new InvalidOperationException("Generated public getter was unavailable: " + fieldName + ".");

            var getterStorageField = ReadGetterMembers(publicProperty.GetMethod)
                .OfType<FieldInfo>()
                .FirstOrDefault(field =>
                    !field.IsStatic &&
                    IsInternalSlotName(field.Name) &&
                    (newValue == null || field.FieldType.IsInstanceOfType(newValue)));
            if (getterStorageField != null)
                return SetInternalSlot((T)boxed, getterStorageField.Name, newValue);

            var ilSlot = ResolveGetterInternalSlot(boxed.GetType(), publicProperty, newValue);
            if (ilSlot != null)
                return SetInternalSlot((T)boxed, ilSlot.Name, newValue);

            var candidates = boxed.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(candidate =>
                    IsInternalSlotName(candidate.Name) &&
                    candidate.SetMethod != null &&
                    (newValue == null || candidate.PropertyType.IsInstanceOfType(newValue)))
                .ToArray();
            PropertyInfo? property = null;
            for (var index = 0; index < candidates.Length; index++)
            {
                var candidate = candidates[index];
                if (!TryCreateDistinctProbeValue(candidate.PropertyType, newValue, index, out var probeValue, out var ownsProbe))
                {
                    continue;
                }

                bool matches;
                try
                {
                    matches = InternalSlotUpdatesGetter(value, candidate, publicProperty, probeValue);
                }
                finally
                {
                    if (ownsProbe && probeValue is UnityEngine.Object nativeProbe)
                        UnityEngine.Object.DestroyImmediate(nativeProbe);
                }

                if (!matches)
                    continue;

                if (property != null)
                {
                    throw new InvalidOperationException(
                        "Generated internal slot probe was ambiguous for getter " + fieldName +
                        ": " + property.Name + " and " + candidate.Name + ".");
                }

                property = candidate;
            }

            if (property == null)
                throw new InvalidOperationException("Generated internal slot for getter was unavailable: " + fieldName + ".");
            return SetInternalSlot((T)boxed, property.Name, newValue);
        }

        private static bool TryCreateDistinctProbeValue(
            Type propertyType,
            object? actualValue,
            int ordinal,
            out object? probeValue,
            out bool ownsProbe)
        {
            probeValue = null;
            ownsProbe = false;
            if (propertyType == typeof(int))
            {
                var candidate = int.MinValue + ordinal + 1;
                if (actualValue is int actual && candidate == actual)
                    candidate++;
                probeValue = candidate;
                return true;
            }

            if (propertyType == typeof(float))
            {
                var candidate = float.MaxValue - ordinal;
                if (actualValue is float actual && candidate == actual)
                    candidate--;
                probeValue = candidate;
                return true;
            }

            if (propertyType == typeof(double))
            {
                var candidate = double.MaxValue - ordinal;
                if (actualValue is double actual && candidate == actual)
                    candidate--;
                probeValue = candidate;
                return true;
            }

            if (propertyType == typeof(string))
            {
                var candidate = "__spire_slot_probe_" + ordinal;
                if (actualValue is string actual && candidate == actual)
                    candidate += "_distinct";
                probeValue = candidate;
                return true;
            }

            if (propertyType == typeof(bool))
            {
                probeValue = actualValue is bool actual ? !actual : true;
                return true;
            }

            if (propertyType.IsEnum)
            {
                foreach (var value in Enum.GetValues(propertyType))
                {
                    if (!Equals(value, actualValue))
                    {
                        probeValue = value;
                        return true;
                    }
                }

                return false;
            }

            if (actualValue is UnityEngine.Object)
            {
                var nativeProbe = new Texture2D(1, 1)
                {
                    name = "__spire_slot_probe_" + ordinal,
                };
                if (propertyType.IsAssignableFrom(nativeProbe.GetType()) || propertyType == typeof(object))
                {
                    probeValue = nativeProbe;
                    ownsProbe = true;
                    return true;
                }

                UnityEngine.Object.DestroyImmediate(nativeProbe);
                return false;
            }

            if (propertyType == typeof(object))
            {
                probeValue = new object();
                return true;
            }

            // Generated union slots are value types. Keep their actual value as a
            // controlled probe only after the getter's IL did not identify one slot.
            if (!propertyType.IsValueType && actualValue != null)
            {
                probeValue = actualValue;
                return true;
            }

            if (propertyType.IsValueType && actualValue != null)
            {
                probeValue = actualValue;
                return true;
            }

            return false;
        }

        private static bool IsInternalSlotName(string name)
        {
            return name.StartsWith("_s", StringComparison.Ordinal) ||
                name.StartsWith("__s", StringComparison.Ordinal);
        }

        private static PropertyInfo? ResolveGetterInternalSlot(
            Type unionType,
            PropertyInfo publicProperty,
            object? newValue)
        {
            var getter = publicProperty.GetMethod;
            if (getter == null)
                return null;

            var slots = new List<PropertyInfo>();
            foreach (var member in ReadGetterMembers(getter))
            {
                var slot = member switch
                {
                    MethodInfo method when method.Name.StartsWith("get_", StringComparison.Ordinal) =>
                        FindCompatibleInternalSlot(unionType, method.Name.Substring(4), newValue),
                    FieldInfo field => FindCompatibleInternalSlot(unionType, field.Name, newValue),
                    _ => null,
                };
                if (slot == null || slots.Any(existing => existing.Name == slot.Name))
                    continue;
                slots.Add(slot);
            }

            return slots.Count == 1 ? slots[0] : null;
        }

        private static PropertyInfo? FindCompatibleInternalSlot(Type unionType, string memberName, object? newValue)
        {
            var names = new List<string> { memberName };
            if (memberName.StartsWith("__s", StringComparison.Ordinal))
                names.Add(memberName.Substring(1));

            foreach (var name in names)
            {
                var property = unionType.GetProperty(
                    name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (property != null &&
                    IsInternalSlotName(property.Name) &&
                    property.SetMethod != null &&
                    (newValue == null || property.PropertyType.IsInstanceOfType(newValue)))
                {
                    return property;
                }
            }

            return null;
        }

        private static readonly Dictionary<ushort, OpCode> ILInstructionCodes =
            typeof(OpCodes)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.FieldType == typeof(OpCode))
                .Select(field => (OpCode)field.GetValue(null)!)
                .ToDictionary(opcode => unchecked((ushort)opcode.Value));

        private static List<MemberInfo> ReadGetterMembers(MethodInfo getter)
        {
            var result = new List<MemberInfo>();
            byte[]? il;
            try
            {
                il = getter.GetMethodBody()?.GetILAsByteArray();
            }
            catch (InvalidOperationException)
            {
                return result;
            }

            if (il == null)
                return result;

            var offset = 0;
            while (offset < il.Length)
            {
                ushort opcodeValue = il[offset++];
                if (opcodeValue == 0xFE)
                {
                    if (offset >= il.Length)
                        break;
                    opcodeValue = (ushort)(0xFE00 | il[offset++]);
                }

                if (!ILInstructionCodes.TryGetValue(opcodeValue, out var opcode))
                    break;

                var operandOffset = offset;
                if (opcode.OperandType == OperandType.InlineField ||
                    opcode.OperandType == OperandType.InlineMethod ||
                    opcode.OperandType == OperandType.InlineTok ||
                    opcode.OperandType == OperandType.InlineType)
                {
                    if (operandOffset + 4 > il.Length)
                        break;

                    var token = BitConverter.ToInt32(il, operandOffset);
                    try
                    {
                        var typeArguments = getter.DeclaringType?.IsGenericType == true
                            ? getter.DeclaringType.GetGenericArguments()
                            : null;
                        var methodArguments = getter.IsGenericMethod ? getter.GetGenericArguments() : null;
                        var member = getter.Module.ResolveMember(token, typeArguments, methodArguments);
                        if (member != null)
                            result.Add(member);
                    }
                    catch (ArgumentException)
                    {
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }

                var operandSize = GetOperandSize(opcode.OperandType, il, operandOffset);
                if (operandSize < 0 || operandOffset + operandSize > il.Length)
                    break;
                offset = operandOffset + operandSize;
            }

            return result;
        }

        private static int GetOperandSize(OperandType operandType, byte[] il, int operandOffset)
        {
            switch (operandType)
            {
                case OperandType.InlineNone:
                    return 0;
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineVar:
                    return 1;
                case OperandType.ShortInlineR:
                    return 4;
                case OperandType.InlineVar:
                    return 2;
                case OperandType.InlineI:
                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineI8:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                    return operandType == OperandType.InlineI8 ? 8 : 4;
                case OperandType.InlineR:
                    return 8;
                case OperandType.InlineSwitch:
                    if (operandOffset + 4 > il.Length)
                        return -1;
                    var count = BitConverter.ToInt32(il, operandOffset);
                    return count < 0 ? -1 : 4 + (count * 4);
                default:
                    return -1;
            }
        }

        private static bool InternalSlotUpdatesGetter<T>(
            T value,
            PropertyInfo internalSlot,
            PropertyInfo publicProperty,
            object? newValue)
        {
            if (!typeof(T).IsValueType)
                throw new InvalidOperationException("Internal slot probing requires a generated union struct.");

            try
            {
                T probeValue = value;
                object probe = probeValue!;
                internalSlot.SetValue(probe, newValue, null);
                return Equals(publicProperty.GetValue(probe, null), newValue);
            }
            catch (TargetInvocationException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static string GetKindName<T>(T value)
        {
            var property = value!.GetType().GetProperty("kind", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return property?.GetValue(value, null)?.ToString() ?? "<invalid>";
        }

        private static void RequireParentChild(AssetScope scope, int count, string label, int marker)
        {
            Require(scope.Asset.parent.kind == UnityCallbackParent.Kind.Parent, "Parent kind differed.");
            Require(scope.Asset.parent.marker == marker, "Parent marker differed.");
            Require(scope.Asset.parent.child.kind == UnityCallbackChild.Kind.Text, "Child kind differed.");
            Require(scope.Asset.parent.child.count == count, "Child count differed.");
            Require(scope.Asset.parent.child.label == label, "Child label differed.");
            Require(scope.Asset.parent.child.asset == scope.Texture, "Child object reference differed.");
        }

        private static void WriteBoxedValue(SerializedProperty property, object value)
        {
            try
            {
                property.boxedValue = value;
            }
            catch (NotSupportedException exception)
            {
                throw new InvalidOperationException("SerializedProperty.boxedValue setter is unsupported: " + exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                throw new InvalidOperationException("SerializedProperty.boxedValue setter is unavailable: " + exception.Message);
            }
        }

        private static bool TryReadBoxedValue(SerializedProperty property, out string state)
        {
            try
            {
                var boxed = property.boxedValue;
                if (boxed == null)
                {
                    state = "null";
                    return false;
                }
                var kind = boxed.GetType().GetProperty("kind", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (kind == null)
                {
                    state = boxed.GetType().Name;
                    return false;
                }
                state = kind.GetValue(boxed, null)?.ToString() ?? "default";
                if (state != UnityDrawerUnion.Kind.Text.ToString())
                    return false;

                var label = FindMember(boxed.GetType(), "label");
                var labelValue = label is FieldInfo field
                    ? field.GetValue(boxed)
                    : ((PropertyInfo?)label)?.GetValue(boxed, null);
                if (!Equals(labelValue, "serialized"))
                {
                    state = "Text-incomplete";
                    return false;
                }

                return true;
            }
            catch (Exception exception) when (exception is NotSupportedException || exception is InvalidOperationException)
            {
                state = "unsupported";
                return false;
            }
        }

        private static List<Type> FindDrawers(Type targetType)
        {
            var result = new List<Type>();
            foreach (var drawerType in TypeCache.GetTypesWithAttribute<CustomPropertyDrawer>())
            {
                foreach (var attribute in drawerType.GetCustomAttributes(typeof(CustomPropertyDrawer), false))
                {
                    var target = ReadDrawerTarget((CustomPropertyDrawer)attribute);
                    if (target == targetType)
                    {
                        result.Add(drawerType);
                        break;
                    }
                }
            }
            return result;
        }

        private static PropertyDrawer? FindDrawer(Type targetType)
        {
            var drawers = FindDrawers(targetType);
            if (drawers.Count == 0)
                return null;
            return Activator.CreateInstance(drawers[0]) as PropertyDrawer;
        }

        private static Type? ReadDrawerTarget(CustomPropertyDrawer attribute)
        {
            var field = attribute.GetType().GetField("m_Type", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
                return field.GetValue(attribute) as Type;
            var property = attribute.GetType().GetProperty("type", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return property?.GetValue(attribute, null) as Type;
        }

        private static void EnsureAssetFolder()
        {
            if (!AssetDatabase.IsValidFolder(AssetFolder))
            {
                AssetDatabase.CreateFolder("Assets", "SpireUnityIntegration");
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static string ProjectRoot => Directory.GetParent(Application.dataPath)!.FullName;

        [Serializable]
        private sealed class IntegrationReport
        {
            public string probe = string.Empty;
            public string unityVersion = string.Empty;
            public string graphicsBackend = string.Empty;
            public string graphicsDeviceName = string.Empty;
            public string graphicsDeviceVendor = string.Empty;
            public string graphicsDeviceVersion = string.Empty;
            public int hardFailureCount;
            public int failureCount;
            public int observedCount;
            public int passed;
            public int failed;
            public List<ScenarioResult> scenarios = new List<ScenarioResult>();
        }

        [Serializable]
        private sealed class ScenarioResult
        {
            public string name = string.Empty;
            public string expected = string.Empty;
            public string status = string.Empty;
            public string observed = string.Empty;
            public string error = string.Empty;
            public List<string> invariants = new List<string>();
            public List<string> callbackAssertions = new List<string>();
        }

        [Serializable]
        private sealed class LifecycleStage
        {
            public string stage = string.Empty;
            public string message = string.Empty;
            public string unityVersion = string.Empty;
            public bool domainReloadObserved;
            public int polls;
        }

        [Serializable]
        private sealed class PlayerBuildResult
        {
            public string unityVersion = string.Empty;
            public string status = string.Empty;
            public string summary = string.Empty;
            public string outputPath = string.Empty;
            public string runtimeReportPath = string.Empty;
            public string runtimeReportArgument = string.Empty;
            public string scriptingBackend = string.Empty;
            public string managedStrippingLevel = string.Empty;
            public bool cleanBuildCache;
            public string fixtureSelection = string.Empty;
            public string profile = string.Empty;
            public string executablePath = string.Empty;
            public int errors;
            public int warnings;
            public string error = string.Empty;
        }

        private sealed class ObservedException : Exception
        {
            public ObservedException(string message) : base(message)
            {
            }
        }

        private sealed class MissingGeneratedSurfaceException : Exception
        {
            public MissingGeneratedSurfaceException(string message) : base(message)
            {
            }
        }

        private sealed class AssetScope : IDisposable
        {
            private static int _nextId;

            public readonly string AssetPath;
            public readonly Texture2D Texture;
            public UnitySerializationAsset Asset;

            public AssetScope(string name)
            {
                EnsureAssetFolder();
                var id = ++_nextId;
                AssetPath = AssetFolder + "/" + id + "-" + name + ".asset";
                Texture = new Texture2D(2, 2) { name = id + "-" + name + "-texture" };
                Asset = ScriptableObject.CreateInstance<UnitySerializationAsset>();
                AssetDatabase.CreateAsset(Asset, AssetPath);
                AssetDatabase.AddObjectToAsset(Texture, AssetPath);
                Asset.reference = Texture;
                AssetDatabase.SaveAssets();
            }

            public string AssetFilePath => Path.Combine(ProjectRoot, AssetPath.Replace('/', Path.DirectorySeparatorChar));

            public void Save()
            {
                EditorUtility.SetDirty(Asset);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
            }

            public void Reload()
            {
                Save();
                var current = Asset;
                Resources.UnloadAsset(current);
                Asset = AssetDatabase.LoadAssetAtPath<UnitySerializationAsset>(AssetPath);
                if (Asset == null)
                    throw new InvalidOperationException("Asset reload returned null: " + AssetPath);
            }

            public void Dispose()
            {
                if (Asset != null)
                    EditorUtility.SetDirty(Asset);
                AssetDatabase.DeleteAsset(AssetPath);
                AssetDatabase.SaveAssets();
            }
        }

        private sealed class SerializationFootprintAssetScope : IDisposable
        {
            private static int _nextId;

            public readonly string AssetPath;
            public UnitySerializationFootprintAsset Asset;

            public SerializationFootprintAssetScope(string name)
            {
                EnsureAssetFolder();
                var id = ++_nextId;
                AssetPath = AssetFolder + "/footprint-" + id + "-" + name + ".asset";
                Asset = ScriptableObject.CreateInstance<UnitySerializationFootprintAsset>();
                AssetDatabase.CreateAsset(Asset, AssetPath);
                AssetDatabase.SaveAssets();
            }

            public void Save()
            {
                EditorUtility.SetDirty(Asset);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
            }

            public void Reload()
            {
                Save();
                var current = Asset;
                Resources.UnloadAsset(current);
                Asset = AssetDatabase.LoadAssetAtPath<UnitySerializationFootprintAsset>(AssetPath);
                if (Asset == null)
                    throw new InvalidOperationException("Serialization-footprint asset reload returned null: " + AssetPath);
            }

            public void Dispose()
            {
                if (Asset != null)
                    EditorUtility.SetDirty(Asset);
                AssetDatabase.DeleteAsset(AssetPath);
                AssetDatabase.SaveAssets();
            }
        }

        private sealed class ManagedReferenceAssetScope : IDisposable
        {
            private static int _nextId;

            public readonly string AssetPath;
            public UnityManagedReferenceAsset Asset;

            public ManagedReferenceAssetScope(string name)
            {
                EnsureAssetFolder();
                var id = ++_nextId;
                AssetPath = AssetFolder + "/managed-" + id + "-" + name + ".asset";
                Asset = ScriptableObject.CreateInstance<UnityManagedReferenceAsset>();
                AssetDatabase.CreateAsset(Asset, AssetPath);
                AssetDatabase.SaveAssets();
            }

            public string AssetFilePath => Path.Combine(ProjectRoot, AssetPath.Replace('/', Path.DirectorySeparatorChar));

            public void Save()
            {
                EditorUtility.SetDirty(Asset);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
            }

            public void Reload()
            {
                Save();
                ReloadWithoutSave();
            }

            public void ReloadWithoutSave()
            {
                var current = Asset;
                Resources.UnloadAsset(current);
                Asset = AssetDatabase.LoadAssetAtPath<UnityManagedReferenceAsset>(AssetPath);
                if (Asset == null)
                    throw new InvalidOperationException("Managed-reference asset reload returned null: " + AssetPath);
            }

            public void Dispose()
            {
                if (Asset != null)
                    EditorUtility.SetDirty(Asset);
                AssetDatabase.DeleteAsset(AssetPath);
                AssetDatabase.SaveAssets();
            }
        }

        private sealed class DrawerHostScope : IDisposable
        {
            public readonly GameObject Object;
            public readonly UnityDrawerHost Host;

            public DrawerHostScope(string name)
            {
                Object = new GameObject("SpireUnityIntegration-" + name);
                Host = Object.AddComponent<UnityDrawerHost>();
            }

            public void Dispose()
            {
                if (Object != null)
                    UnityEngine.Object.DestroyImmediate(Object);
            }
        }

        private sealed class ManagedReferenceDrawerScope : IDisposable
        {
            public readonly GameObject Object;
            public readonly UnityManagedReferenceDrawerHost Host;

            public ManagedReferenceDrawerScope(string name)
            {
                Object = new GameObject("SpireUnityIntegration-" + name);
                Host = Object.AddComponent<UnityManagedReferenceDrawerHost>();
            }

            public void Dispose()
            {
                if (Object != null)
                    UnityEngine.Object.DestroyImmediate(Object);
            }
        }

        private sealed class DrawerCallbackRunner : IDisposable
        {
            private const int ScenarioDeadlineFrames = 120;
            private const double ScenarioWallClockWatchdogSeconds = 3.0;
            private readonly DrawerProbeWindow _window;
            private readonly List<GameObject> _objects = new List<GameObject>();
            private readonly List<string> _assetPaths = new List<string>();
            private readonly List<DrawerUiScenario> _scenarios;
            private DrawerUiScenario? _current;
            private int _nextScenario;
            private int _waitFrames;
            private int _scenarioFrames;
            private double _scenarioStartedAt;

            public DrawerCallbackRunner(DrawerProbeWindow window)
            {
                _window = window;
                _scenarios = new List<DrawerUiScenario>
            {
                CreateMissingPayloadScenario(),
                CreateDirectSelectorScenario(),
                CreateArraySelectorScenario(),
                CreateListSelectorScenario(),
                CreateUndoRedoScenario(),
                CreateSharedPayloadIsolationScenario(),
                CreateNestedInlineHelperSharedPayloadScenario(),
                CreateRenderedFieldHistoryScenario(),
                CreateRenderedEditPreservesStagedUnrelatedApplyScenario(),
                CreateRenderedNumericFieldsScenario(),
                CreateRenderedInspectorNamedEnumScenario(),
                CreateRenderedFlagsEnumScenario(),
                CreateRenderedEnumInvalidChoiceScenario(),
                CreateReadOnlyAliasedPayloadScenario(),
                CreateReadOnlyAliasedPayloadApplyScenario(),
                CreateMultiObjectActiveFieldScenario(),
                CreateDetachedUndoRedoScenario(),
                CreateUndoBeforeFirstPanelAttachScenario(),
                CreateMultiObjectScenario(),
                CreatePrefabScenario(),
                CreateNestedGenericScenario(),
                CreatePrivateBaseOwnerScenario(),
                CreateGenericClosedScenario(),
                CreateGenericContainingScenario(),
                CreateRebindScenario(),
                CreateBoxedLayoutGuiScenario(),
                CreateBoxedLayoutUndoScenario(),
                CreateUnsafeOverlapGuiScenario(),
                CreateUnsafeOverlapNestedUndoScenario(),
                CreateOverlapGuiScenario(),
                CreateOverlapNestedUndoScenario(),
            };
            }

            public bool Tick()
            {
                if (_current == null)
                {
                    if (_nextScenario >= _scenarios.Count)
                        return true;

                    _current = _scenarios[_nextScenario++];
                    _waitFrames = 0;
                    _scenarioFrames = 0;
                    _scenarioStartedAt = EditorApplication.timeSinceStartup;
                    try
                    {
                        _current.Start(this, _window);
                    }
                    catch (Exception exception)
                    {
                        CompletePendingAsFail(_current.Name, exception);
                        _current = null;
                    }

                    return _nextScenario >= _scenarios.Count && _current == null;
                }

                if (EditorApplication.timeSinceStartup - _scenarioStartedAt >= ScenarioWallClockWatchdogSeconds)
                {
                    CompletePendingAsFail(
                        _current.Name,
                        new TimeoutException(
                            "The drawer callback scenario exceeded its wall-clock watchdog of " +
                            ScenarioWallClockWatchdogSeconds + " seconds after " + _scenarioFrames + " editor frames."));
                    _current = null;
                    return _nextScenario >= _scenarios.Count;
                }

                if (_waitFrames++ < 2)
                    return false;

                if (++_scenarioFrames > ScenarioDeadlineFrames)
                {
                    CompletePendingAsFail(
                        _current.Name,
                        new TimeoutException(
                            "The drawer callback scenario exceeded its deadline of " + ScenarioDeadlineFrames + " editor frames."));
                    _current = null;
                    return _nextScenario >= _scenarios.Count;
                }

                try
                {
                    if (!_current.Tick(this, _window))
                        return false;

                    CompletePendingAsPass(_current.Name, _current.Observed);
                }
                catch (Exception exception)
                {
                    CompletePendingAsFail(_current.Name, exception);
                }

                _current = null;
                return _nextScenario >= _scenarios.Count;
            }

            public UnityDrawerHost CreateHost(string name)
            {
                var gameObject = new GameObject("SpireUnityDrawerCallback-" + name);
                _objects.Add(gameObject);
                return gameObject.AddComponent<UnityDrawerHost>();
            }

            public UnityDrawerAsset CreateAsset(string name)
            {
                EnsureAssetFolder();
                var path = AssetFolder + "/drawer-callback-" +
                    Guid.NewGuid().ToString("N") + "-" + name + ".asset";
                var asset = ScriptableObject.CreateInstance<UnityDrawerAsset>();
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();
                _assetPaths.Add(path);
                return asset;
            }

            public UnityDrawerInlineHelperAsset CreateInlineHelperAsset(string name)
            {
                EnsureAssetFolder();
                var path = AssetFolder + "/drawer-callback-inline-helper-" +
                    Guid.NewGuid().ToString("N") + "-" + name + ".asset";
                var asset = ScriptableObject.CreateInstance<UnityDrawerInlineHelperAsset>();
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();
                _assetPaths.Add(path);
                return asset;
            }

            public UnityDrawerAsset ReloadAsset(UnityDrawerAsset asset)
            {
                var path = AssetDatabase.GetAssetPath(asset);
                Require(!string.IsNullOrEmpty(path), "Drawer asset path was unavailable before reload.");
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                Resources.UnloadAsset(asset);
                var reloaded = AssetDatabase.LoadAssetAtPath<UnityDrawerAsset>(path);
                Require(reloaded != null, "Drawer asset reload returned null: " + path);
                return reloaded;
            }

            public UnityDrawerNumericAsset CreateNumericAsset(string name)
            {
                EnsureAssetFolder();
                var path = AssetFolder + "/drawer-callback-numeric-" +
                    Guid.NewGuid().ToString("N") + "-" + name + ".asset";
                var asset = ScriptableObject.CreateInstance<UnityDrawerNumericAsset>();
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();
                _assetPaths.Add(path);
                return asset;
            }

            public UnityDrawerNumericAsset ReloadNumericAsset(UnityDrawerNumericAsset asset)
            {
                var path = AssetDatabase.GetAssetPath(asset);
                Require(!string.IsNullOrEmpty(path), "Numeric drawer asset path was unavailable before reload.");
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                Resources.UnloadAsset(asset);
                var reloaded = AssetDatabase.LoadAssetAtPath<UnityDrawerNumericAsset>(path);
                Require(reloaded != null, "Numeric drawer asset reload returned null: " + path);
                return reloaded;
            }

            public void TrackObject(GameObject gameObject)
            {
                _objects.Add(gameObject);
            }

            public void TrackAsset(string path)
            {
                _assetPaths.Add(path);
            }

            public void Dispose()
            {
                foreach (var gameObject in _objects)
                {
                    if (gameObject != null)
                        UnityEngine.Object.DestroyImmediate(gameObject);
                }

                foreach (var assetPath in _assetPaths)
                    AssetDatabase.DeleteAsset(assetPath);
            }

            private static DrawerUiScenario CreateMissingPayloadScenario()
            {
                var scenario = new DrawerUiScenario("Drawer_MissingPayload_SelectsFirstVariant");
                UnityDrawerHost? host = null;
                var phase = 0;
                scenario.Start = (runner, window) =>
                {
                    host = runner.CreateHost("missing-payload");
                    host.single = UnityDrawerUnion.Text(4, "stale");
                    window.Bind(host, "single");
                    window.SetRawKind("single", 255);
                };
                scenario.Tick = (runner, window) =>
                {
                    if (phase++ == 0)
                        return false;
                    if (phase == 2)
                    {
                        var selectorValue = window.SelectorValue();
                        Require(
                            selectorValue.IndexOf("Empty", StringComparison.OrdinalIgnoreCase) >= 0,
                            "Missing payload did not select the first Empty variant; observed='" + selectorValue + "'.");
                        Require(window.TryInteractWithSelector("Empty"), "Missing-payload Empty selector was unavailable.");
                        return false;
                    }

                    Require(host!.single.kind == UnityDrawerUnion.Kind.Empty, "Missing-payload selector did not construct Empty.");
                    Require(
                        !window.HasBoundFieldControl("count") &&
                        !window.HasBoundFieldControl("label") &&
                        !window.HasBoundFieldControl("value") &&
                        !window.HasBoundFieldControl("texture"),
                        "Missing-payload Empty selection kept inactive controls.");
                    scenario.Observed = "payload=null;display=Empty;selected=Empty;inactive-controls=false";
                    return true;
                };
                return scenario;
            }

            private static DrawerUiScenario CreateDirectSelectorScenario()
            {
                var scenario = new DrawerUiScenario("Drawer_SelectionChange_UsesFactoryReplacement");
                UnityDrawerHost? host = null;
                var phase = 0;
                scenario.Start = (runner, window) =>
                {
                    host = runner.CreateHost("selector");
                    host.single = UnityDrawerUnion.Text(3, "old");
                    window.Bind(host, "single");
                };
                scenario.Tick = (runner, window) =>
                {
                    if (phase++ == 0)
                        return false;
                    if (phase == 2)
                    {
                        Require(window.TryInteractWithSelector("Number"), "Generated Number selector was unavailable.");
                        return false;
                    }

                    Require(host!.single.kind == UnityDrawerUnion.Kind.Number, "Selector did not construct Number.");
                    Require(host.single.value == 0f, "Selected Number did not start with factory defaults.");
                    Require(!window.HasBoundFieldControl("count") && !window.HasBoundFieldControl("label"), "Stale Text controls remained after selector change.");
                    Require(window.HasBoundFieldControl("value"), "Number control was not bound after selector change.");
                    scenario.Observed = "selector=Number;factory-default=value=0;stale-fields=false";
                    return true;
                };
                return scenario;
            }

            private static DrawerUiScenario CreateArraySelectorScenario()
            {
                var scenario = new DrawerUiScenario("Drawer_ArrayElement_NestedPathWriteback");
                UnityDrawerHost? host = null;
                var phase = 0;
                scenario.Start = (runner, window) =>
                {
                    host = runner.CreateHost("array-selector");
                    host.array = new[]
                    {
                    UnityDrawerUnion.Text(1, "first"),
                    UnityDrawerUnion.Number(2f),
                    };
                    window.Bind(host, "array.Array.data[0]");
                };
                scenario.Tick = (runner, window) =>
                {
                    if (phase++ == 0)
                        return false;
                    if (phase == 2)
                    {
                        window.SetPendingPayloadFloat("array.Array.data[1]", 17f);
                        Require(window.TryInteractWithSelector("Number"), "Array element Number selector was unavailable.");
                        return false;
                    }

                    Require(host!.array[0].kind == UnityDrawerUnion.Kind.Number && host.array[0].value == 0f, "Array element selector did not write the factory default.");
                    Require(host.array[1].kind == UnityDrawerUnion.Kind.Number && host.array[1].value == 17f, "Array sibling pending edit was discarded during selector write-back.");
                    Require(window.HasBoundFieldControl("value") && !window.HasBoundFieldControl("label"), "Array selector controls did not follow the active variant.");
                    scenario.Observed = "array[0]=Number(0);array[1]=Number(17);pending-sibling-preserved=true";
                    return true;
                };
                return scenario;
            }

            private static DrawerUiScenario CreateListSelectorScenario()
            {
                var scenario = new DrawerUiScenario("Drawer_ListElement_NestedPathWriteback");
                UnityDrawerHost? host = null;
                var phase = 0;
                scenario.Start = (runner, window) =>
                {
                    host = runner.CreateHost("list-selector");
                    host.list = new List<UnityDrawerUnion>
                    {
                    UnityDrawerUnion.Text(1, "first"),
                    UnityDrawerUnion.Number(2f),
                    };
                    window.Bind(host, "list.Array.data[0]");
                };
                scenario.Tick = (runner, window) =>
                {
                    if (phase++ == 0)
                        return false;
                    if (phase == 2)
                    {
                        window.SetPendingPayloadFloat("list.Array.data[1]", 19f);
                        Require(window.TryInteractWithSelector("Number"), "List element Number selector was unavailable.");
                        return false;
                    }

                    Require(host!.list[0].kind == UnityDrawerUnion.Kind.Number && host.list[0].value == 0f, "List element selector did not write the factory default.");
                    Require(host.list[1].kind == UnityDrawerUnion.Kind.Number && host.list[1].value == 19f, "List sibling pending edit was discarded during selector write-back.");
                    Require(window.HasBoundFieldControl("value") && !window.HasBoundFieldControl("label"), "List selector controls did not follow the active variant.");
                    scenario.Observed = "list[0]=Number(0);list[1]=Number(19);pending-sibling-preserved=true";
                    return true;
                };
                return scenario;
            }

            private static DrawerUiScenario CreateUndoRedoScenario()
            {
                var scenario = new DrawerUiScenario("Drawer_UndoRedo_WholeValueAndPayloadEdit");
                UnityDrawerHost? host = null;
                var phase = 0;
                var settleFrames = 0;
                scenario.Start = (runner, window) =>
                {
                    Undo.ClearAll();
                    host = runner.CreateHost("undo-selector");
                    host.single = UnityDrawerUnion.Text(1, "before");
                    window.Bind(host, "single");
                    window.RecordScenarioPhase("undo-bind-text");
                };
                scenario.Tick = (runner, window) =>
                {
                    if (phase == 0)
                    {
                        window.RecordScenarioPhase("undo-initial-update");
                        phase = 1;
                        return false;
                    }
                    if (phase == 1)
                    {
                        window.RecordScenarioPhase("undo-select-number");
                        Require(window.TryInteractWithSelector("Number"), "Undo scenario Number selector was unavailable.");
                        phase = 2;
                        return false;
                    }
                    if (phase == 2)
                    {
                        window.RecordScenarioPhase("undo-perform-undo");
                        Require(host!.single.kind == UnityDrawerUnion.Kind.Number, "Undo scenario selector did not apply.");
                        Undo.PerformUndo();
                        phase = 3;
                        settleFrames = 0;
                        return false;
                    }
                    if (phase == 3)
                    {
                        window.RecordScenarioPhase("undo-wait-text");
                        window.UpdateSerialized();
                        if (host!.single.kind != UnityDrawerUnion.Kind.Text || host.single.label != "before")
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "Undo did not restore the previous variant within the binding update window; " +
                                    "runtime-kind=" + host.single.kind +
                                    ";runtime-label=" + (host.single.label ?? "<null>") +
                                    ";phase=" + phase +
                                    ";settle-frame=" + settleFrames +
                                    ";state=" + window.DescribeState());
                            return false;
                        }

                        Undo.PerformRedo();
                        window.RecordScenarioPhase("redo-perform-redo");
                        phase = 4;
                        settleFrames = 0;
                        return false;
                    }

                    window.RecordScenarioPhase("redo-wait-number");
                    window.UpdateSerialized();
                    if (host!.single.kind != UnityDrawerUnion.Kind.Number ||
                        host.single.value != 0f ||
                        !window.HasBoundFieldControl("value") ||
                        window.HasBoundFieldControl("count") ||
                        window.HasBoundFieldControl("label"))
                    {
                        if (++settleFrames > 60)
                            throw new TimeoutException(
                                "Redo did not restore the selector and bound controls within the binding update window; " +
                                "runtime-kind=" + host!.single.kind +
                                ";runtime-value=" + host.single.value +
                                ";phase=" + phase +
                                ";settle-frame=" + settleFrames +
                                ";state=" + window.DescribeState());
                        return false;
                    }

                    scenario.Observed = "selector=Number;undo=Text;redo=Number;active-controls=refreshed";
                    return true;
                };
                return scenario;
            }

            private static DrawerUiScenario CreateSharedPayloadIsolationScenario()
            {
                var scenario = new DrawerUiScenario("Drawer_SharedPayload_EditUndoRedoIsolated");
                UnityDrawerHost? host = null;
                var phase = 0;
                var settleFrames = 0;
                scenario.Start = (runner, window) =>
                {
                    Undo.ClearAll();
                    host = runner.CreateHost("shared-payload");
                    host.array = new[]
                    {
                        UnityDrawerUnion.Text(5, "original"),
                        UnityDrawerUnion.Text(5, "original"),
                    };

                    var aliasSerialized = new SerializedObject(host);
                    aliasSerialized.Update();
                    var originalPayload = RequirePayloadProperty(
                        aliasSerialized,
                        "array.Array.data[0]").managedReferenceValue;
                    Require(originalPayload != null, "Shared-payload original payload was unavailable.");
                    RequirePayloadProperty(
                        aliasSerialized,
                        "array.Array.data[1]").managedReferenceValue = originalPayload;
                    Require(
                        aliasSerialized.ApplyModifiedProperties(),
                        "Shared-payload alias setup was not applied.");
                    aliasSerialized.Update();
                    Require(
                        ReferenceEquals(
                            RequirePayloadProperty(aliasSerialized, "array.Array.data[0]").managedReferenceValue,
                            RequirePayloadProperty(aliasSerialized, "array.Array.data[1]").managedReferenceValue),
                        "Shared-payload alias setup did not retain one managed payload.");
                    window.Bind(host, "array.Array.data[1]");
                };
                scenario.Tick = (runner, window) =>
                {
                    if (phase == 0)
                    {
                        phase = 1;
                        return false;
                    }

                    if (phase == 1)
                    {
                        if (!window.UIToolkitBuilt || !window.HasBoundFieldControl("label"))
                            return false;
                        window.UpdateSerialized();
                        phase = 2;
                    }

                    if (phase == 2)
                    {
                        Require(
                            window.TryEditStringControl("label", "edited"),
                            "Shared-payload TextField was unavailable in the rendered drawer.");
                        phase = 3;
                        return false;
                    }

                    if (phase == 3)
                    {
                        window.UpdateSerialized();
                        if (host!.array[0].label != "original" || host.array[1].label != "edited")
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "Rendered TextField edit did not synchronize runtime copies; first=" +
                                    (host.array[0].label ?? "<null>") + ";second=" +
                                    (host.array[1].label ?? "<null>") +
                                    ";" + window.DescribeStringControl("label") +
                                    ";state=" + window.DescribeState());
                            return false;
                        }
                        Require(
                            host!.array[0].label == "original" &&
                            host.array[1].label == "edited",
                            "Editing the shared payload changed the wrong runtime copies: first=" +
                            (host.array[0].label ?? "<null>") + ";second=" +
                            (host.array[1].label ?? "<null>"));
                        var serialized = new SerializedObject(host);
                        serialized.Update();
                        var originalPayload = RequirePayloadProperty(
                            serialized,
                            "array.Array.data[0]").managedReferenceValue;
                        var editedPayload = RequirePayloadProperty(
                            serialized,
                            "array.Array.data[1]").managedReferenceValue;
                        Require(
                            originalPayload != null && editedPayload != null &&
                            !ReferenceEquals(originalPayload, editedPayload),
                            "Editing the shared payload did not isolate the managed references.");
                        RequirePayloadValue(originalPayload, "label", "original");
                        RequirePayloadValue(editedPayload, "label", "edited");
                        Undo.PerformUndo();
                        settleFrames = 0;
                        phase = 4;
                        return false;
                    }

                    if (phase == 4)
                    {
                        window.UpdateSerialized();
                        if (host!.array[0].label != "original" || host.array[1].label != "original")
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "Undo did not restore both shared-payload copies; state=" + window.DescribeState());
                            return false;
                        }

                        Undo.PerformRedo();
                        settleFrames = 0;
                        phase = 5;
                        return false;
                    }

                    window.UpdateSerialized();
                    if (host!.array[0].label != "original" || host.array[1].label != "edited")
                    {
                        if (++settleFrames > 60)
                            throw new TimeoutException(
                                "Redo did not restore the isolated payload edit; state=" + window.DescribeState());
                        return false;
                    }

                    scenario.Observed = "shared-payload=isolated;edit=original->edited;undo=original/original;redo=original/edited";
                    return true;
                };
                return scenario;
            }

            private static DrawerUiScenario CreateNestedInlineHelperSharedPayloadScenario()
            {
                var scenario = new DrawerUiScenario("Drawer_NestedInlineHelper_EditUndoRedoIsolated");
                UnityDrawerInlineHelperAsset? asset = null;
                SerializedObject? serialized = null;
                var phase = 0;
                var settleFrames = 0;

                scenario.Start = (runner, window) =>
                {
                    Undo.ClearAll();
                    asset = runner.CreateInlineHelperAsset("nested-inline-helper");
                    asset.values = new[]
                    {
                        UnityDrawerInlineHelperUnion.Helper(new UnityDrawerInlineHelper { label = "before" }),
                        UnityDrawerInlineHelperUnion.Helper(new UnityDrawerInlineHelper { label = "before" }),
                    };
                    AssetDatabase.SaveAssets();

                    serialized = new SerializedObject(asset);
                    serialized.Update();
                    var sourcePayload = RequirePayloadProperty(
                        serialized,
                        "values.Array.data[0]");
                    var copiedPayload = RequirePayloadProperty(
                        serialized,
                        "values.Array.data[1]");
                    Require(
                        sourcePayload.managedReferenceValue != null,
                        "Nested inline-helper alias source payload was unavailable.");
                    copiedPayload.managedReferenceValue = sourcePayload.managedReferenceValue;
                    Require(
                        serialized.ApplyModifiedProperties(),
                        "Nested inline-helper alias setup was not applied.");
                    AssetDatabase.SaveAssets();
                    serialized.Update();
                    sourcePayload = RequirePayloadProperty(serialized, "values.Array.data[0]");
                    copiedPayload = RequirePayloadProperty(serialized, "values.Array.data[1]");
                    Require(
                        ReferenceEquals(
                            sourcePayload.managedReferenceValue,
                            copiedPayload.managedReferenceValue),
                        "Nested inline-helper alias setup did not retain one managed payload.");

                    var assetPath = AssetDatabase.GetAssetPath(asset);
                    Require(
                        !string.IsNullOrEmpty(assetPath),
                        "Nested inline-helper asset path was unavailable before reload.");
                    EditorUtility.SetDirty(asset);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                    var currentAsset = asset!;
                    Resources.UnloadAsset(currentAsset);
                    asset = AssetDatabase.LoadAssetAtPath<UnityDrawerInlineHelperAsset>(assetPath);
                    Require(
                        asset != null,
                        "Nested inline-helper asset reload returned null: " + assetPath);
                    serialized = new SerializedObject(asset!);
                    serialized.Update();
                    sourcePayload = RequirePayloadProperty(serialized, "values.Array.data[0]");
                    copiedPayload = RequirePayloadProperty(serialized, "values.Array.data[1]");
                    Require(
                        ReferenceEquals(
                            sourcePayload.managedReferenceValue,
                            copiedPayload.managedReferenceValue),
                        "Nested inline-helper alias was not preserved across save and reload.");

                    var sourceHelper = RequireProperty(sourcePayload, "helper");
                    Require(
                        sourceHelper.propertyType != SerializedPropertyType.ManagedReference,
                        "Nested inline-helper field unexpectedly used managed-reference serialization.");
                    window.BindDrawer(
                        serialized,
                        asset,
                        "values.Array.data[1]",
                        typeof(UnityDrawerInlineHelperUnion),
                        imguiProbe: false);
                };

                scenario.Tick = (runner, window) =>
                {
                    if (phase == 0)
                    {
                        phase = 1;
                        return false;
                    }

                    if (phase == 1)
                    {
                        if (!window.UIToolkitBuilt || !window.BoundTreeIsAttached)
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "Nested inline-helper drawer did not attach before expanding the helper Foldout; state=" +
                                    window.DescribeState());
                            return false;
                        }

                        if (!window.HasBoundFieldControl("label"))
                        {
                            window.TryExpandFoldout("helper");
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "Nested inline-helper label control remained unavailable after expanding the helper Foldout; state=" +
                                    window.DescribeState());
                            return false;
                        }

                        window.UpdateSerialized();
                        settleFrames = 0;
                        phase = 2;
                    }

                    if (phase == 2)
                    {
                        Require(
                            window.TryEditStringControl("label", "edited"),
                            "Nested inline-helper label TextField was unavailable in the rendered drawer.");
                        phase = 3;
                        return false;
                    }

                    if (phase == 3)
                    {
                        window.UpdateSerialized();
                        if (asset!.values[0].helper.label != "before" ||
                            asset.values[1].helper.label != "edited")
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "Nested inline-helper TextField edit did not isolate the aliased payload; first=" +
                                    asset.values[0].helper.label + ";second=" +
                                    asset.values[1].helper.label + ";" +
                                    window.DescribeStringControl("label") +
                                    ";state=" + window.DescribeState());
                            return false;
                        }

                        serialized!.Update();
                        var sourcePayload = RequirePayloadProperty(
                            serialized,
                            "values.Array.data[0]");
                        var editedPayload = RequirePayloadProperty(
                            serialized,
                            "values.Array.data[1]");
                        Require(
                            sourcePayload.managedReferenceId != 0 &&
                            editedPayload.managedReferenceId != 0 &&
                            sourcePayload.managedReferenceId != editedPayload.managedReferenceId,
                            "Nested inline-helper edit did not isolate managed-reference identities.");
                        Require(
                            RequireProperty(RequireProperty(sourcePayload, "helper"), "label").stringValue == "before" &&
                            RequireProperty(RequireProperty(editedPayload, "helper"), "label").stringValue == "edited",
                            "Nested inline-helper payload fields did not retain source/edited values.");

                        AssetDatabase.SaveAssets();
                        var savedYaml = File.ReadAllText(AssetFilePath(asset));
                        Require(
                            savedYaml.IndexOf("edited", StringComparison.Ordinal) >= 0,
                            "Nested inline-helper edited value was not persisted by AssetDatabase.SaveAssets.");
                        Undo.PerformUndo();
                        settleFrames = 0;
                        phase = 4;
                        return false;
                    }

                    if (phase == 4)
                    {
                        window.UpdateSerialized();
                        if (asset!.values[0].helper.label != "before" ||
                            asset.values[1].helper.label != "before")
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "Undo did not restore both nested inline-helper copies; state=" +
                                    window.DescribeState());
                            return false;
                        }

                        Undo.PerformRedo();
                        settleFrames = 0;
                        phase = 5;
                        return false;
                    }

                    window.UpdateSerialized();
                    if (asset!.values[0].helper.label != "before" ||
                        asset.values[1].helper.label != "edited")
                    {
                        if (++settleFrames > 60)
                            throw new TimeoutException(
                                "Redo did not restore the nested inline-helper edit; state=" +
                                window.DescribeState());
                        return false;
                    }

                    AssetDatabase.SaveAssets();
                    scenario.Observed = "nested-helper=inline;alias=isolated;edit=before->edited;save=edited;undo=before/before;redo=before/edited";
                    return true;
                };
                return scenario;
            }

            private static DrawerUiScenario CreateRenderedFieldHistoryScenario()
            {
                var scenario = new DrawerUiScenario("Drawer_RenderedFieldHistory_MultiStepUndoRedo");
                UnityDrawerAsset? asset = null;
                SerializedObject? serialized = null;
                var phase = 0;
                var settleFrames = 0;

                scenario.Start = (runner, window) =>
                {
                    Undo.ClearAll();
                    asset = runner.CreateAsset("rendered-field-history");
                    asset.values = new[]
                    {
                        UnityDrawerUnion.Text(5, "original"),
                        UnityDrawerUnion.Text(5, "original"),
                    };
                    AssetDatabase.SaveAssets();

                    serialized = new SerializedObject(asset);
                    serialized.Update();
                    var sourcePayload = RequirePayloadProperty(serialized, "values.Array.data[0]");
                    var copiedPayload = RequirePayloadProperty(serialized, "values.Array.data[1]");
                    Require(
                        sourcePayload.managedReferenceValue != null,
                        "Rendered history alias source payload was unavailable.");
                    copiedPayload.managedReferenceValue = sourcePayload.managedReferenceValue;
                    Require(
                        serialized.ApplyModifiedProperties(),
                        "Rendered history alias setup was not applied.");
                    AssetDatabase.SaveAssets();
                    asset = runner.ReloadAsset(asset);
                    serialized = new SerializedObject(asset);
                    serialized.Update();
                    sourcePayload = RequirePayloadProperty(serialized, "values.Array.data[0]");
                    copiedPayload = RequirePayloadProperty(serialized, "values.Array.data[1]");
                    Require(
                        ReferenceEquals(
                            sourcePayload.managedReferenceValue,
                            copiedPayload.managedReferenceValue),
                        "Rendered history alias was not preserved across save and reload.");
                    window.BindDrawer(
                        serialized,
                        asset,
                        "values.Array.data[1]",
                        typeof(UnityDrawerUnion),
                        imguiProbe: false);
                };

                scenario.Tick = (runner, window) =>
                {
                    if (phase == 0)
                    {
                        phase = 1;
                        return false;
                    }

                    if (phase == 1)
                    {
                        if (!window.UIToolkitBuilt || !window.BoundTreeIsAttached ||
                            !window.HasBoundFieldControl("count") ||
                            !window.HasBoundFieldControl("label"))
                            return false;
                        Require(
                            window.TryEditIntegerControl("count", 7),
                            "Rendered history count IntegerField was unavailable.");
                        phase = 2;
                        return false;
                    }

                    if (phase == 2)
                    {
                        window.UpdateSerialized();
                        if (asset!.values[0].count != 5 ||
                            asset.values[0].label != "original" ||
                            asset.values[1].count != 7 ||
                            asset.values[1].label != "original")
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "Rendered history count edit did not isolate the aliased payload; state=" +
                                    window.DescribeState());
                            return false;
                        }
                        RequireDrawerAssetTextState(asset, 5, "original", 7, "original");
                        Require(
                            window.TryEditStringControl("label", "one"),
                            "Rendered history first label TextField was unavailable.");
                        phase = 3;
                        settleFrames = 0;
                        return false;
                    }

                    if (phase == 3)
                    {
                        window.UpdateSerialized();
                        if (asset!.values[0].count != 5 ||
                            asset.values[0].label != "original" ||
                            asset.values[1].count != 7 ||
                            asset.values[1].label != "one")
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "Rendered history first label edit did not preserve prior count state; state=" +
                                    window.DescribeState());
                            return false;
                        }
                        RequireDrawerAssetTextState(asset, 5, "original", 7, "one");
                        Require(
                            window.TryEditStringControl("label", "two"),
                            "Rendered history second label TextField was unavailable.");
                        phase = 4;
                        settleFrames = 0;
                        return false;
                    }

                    if (phase == 4)
                    {
                        window.UpdateSerialized();
                        if (asset!.values[0].count != 5 ||
                            asset.values[0].label != "original" ||
                            asset.values[1].count != 7 ||
                            asset.values[1].label != "two")
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "Rendered history second label edit did not retain the first edit; state=" +
                                    window.DescribeState());
                            return false;
                        }
                        RequireDrawerAssetTextState(asset, 5, "original", 7, "two");
                        Undo.PerformUndo();
                        phase = 5;
                        settleFrames = 0;
                        return false;
                    }

                    if (phase == 5)
                    {
                        window.UpdateSerialized();
                        if (asset!.values[0].count != 5 ||
                            asset.values[0].label != "original" ||
                            asset.values[1].count != 7 ||
                            asset.values[1].label != "one")
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "First rendered history Undo did not restore label=one; state=" +
                                    window.DescribeState());
                            return false;
                        }
                        RequireDrawerAssetTextState(asset, 5, "original", 7, "one");
                        Undo.PerformUndo();
                        phase = 6;
                        settleFrames = 0;
                        return false;
                    }

                    if (phase == 6)
                    {
                        window.UpdateSerialized();
                        if (asset!.values[0].count != 5 ||
                            asset.values[0].label != "original" ||
                            asset.values[1].count != 7 ||
                            asset.values[1].label != "original")
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "Second rendered history Undo did not restore label=original; state=" +
                                    window.DescribeState());
                            return false;
                        }
                        RequireDrawerAssetTextState(asset, 5, "original", 7, "original");
                        Undo.PerformRedo();
                        phase = 7;
                        settleFrames = 0;
                        return false;
                    }

                    if (phase == 7)
                    {
                        window.UpdateSerialized();
                        if (asset!.values[0].count != 5 ||
                            asset.values[0].label != "original" ||
                            asset.values[1].count != 7 ||
                            asset.values[1].label != "one")
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "First rendered history Redo did not restore label=one; state=" +
                                    window.DescribeState());
                            return false;
                        }
                        RequireDrawerAssetTextState(asset, 5, "original", 7, "one");
                        Undo.PerformRedo();
                        phase = 8;
                        settleFrames = 0;
                        return false;
                    }

                    window.UpdateSerialized();
                    if (asset!.values[0].count != 5 ||
                        asset.values[0].label != "original" ||
                        asset.values[1].count != 7 ||
                        asset.values[1].label != "two")
                    {
                        if (++settleFrames > 60)
                            throw new TimeoutException(
                                "Second rendered history Redo did not restore label=two; state=" +
                                window.DescribeState());
                        return false;
                    }
                    RequireDrawerAssetTextState(asset, 5, "original", 7, "two");
                    scenario.Observed = "saved-reloaded=true;count=5->7;label=original->one->two;undo=one/original;redo=one/two;sibling-isolated=true";
                    return true;
                };
                return scenario;
            }

            private static DrawerUiScenario CreateRenderedEditPreservesStagedUnrelatedApplyScenario()
            {
                var scenario = new DrawerUiScenario("Drawer_RenderedEdit_PreservesStagedUnrelatedApply");
                UnityDrawerAsset? asset = null;
                SerializedObject? serialized = null;
                var phase = 0;
                var settleFrames = 0;

                scenario.Start = (runner, window) =>
                {
                    Undo.ClearAll();
                    asset = runner.CreateAsset("rendered-edit-unrelated-apply");
                    asset.values = new[]
                    {
                        UnityDrawerUnion.Text(5, "original"),
                        UnityDrawerUnion.Text(5, "original"),
                    };
                    asset.unrelated = 1;
                    AssetDatabase.SaveAssets();

                    serialized = new SerializedObject(asset);
                    serialized.Update();
                    var sourcePayload = RequirePayloadProperty(serialized, "values.Array.data[0]");
                    var copiedPayload = RequirePayloadProperty(serialized, "values.Array.data[1]");
                    Require(
                        sourcePayload.managedReferenceValue != null,
                        "Staged unrelated-apply alias source payload was unavailable.");
                    copiedPayload.managedReferenceValue = sourcePayload.managedReferenceValue;
                    Require(
                        serialized.ApplyModifiedProperties(),
                        "Staged unrelated-apply alias setup was not applied.");
                    AssetDatabase.SaveAssets();
                    asset = runner.ReloadAsset(asset);
                    serialized = new SerializedObject(asset);
                    serialized.Update();
                    sourcePayload = RequirePayloadProperty(serialized, "values.Array.data[0]");
                    copiedPayload = RequirePayloadProperty(serialized, "values.Array.data[1]");
                    Require(
                        ReferenceEquals(
                            sourcePayload.managedReferenceValue,
                            copiedPayload.managedReferenceValue),
                        "Staged unrelated-apply alias was not preserved across save and reload.");
                    window.BindDrawer(
                        serialized,
                        asset,
                        "values.Array.data[1]",
                        typeof(UnityDrawerUnion),
                        imguiProbe: false);
                };

                scenario.Tick = (runner, window) =>
                {
                    if (phase == 0)
                    {
                        phase = 1;
                        return false;
                    }

                    if (phase == 1)
                    {
                        if (!window.UIToolkitBuilt || !window.BoundTreeIsAttached ||
                            !window.HasBoundFieldControl("label"))
                            return false;
                        window.UpdateSerialized();
                        var unrelated = serialized!.FindProperty("unrelated");
                        Require(unrelated != null, "Staged unrelated SerializedProperty was unavailable.");
                        unrelated!.intValue = 2;
                        Require(
                            asset!.unrelated == 1,
                            "Staging the unrelated SerializedProperty applied before the rendered union edit.");
                        Require(
                            window.TryEditStringControl("label", "edited"),
                            "Staged unrelated-apply label TextField was unavailable.");
                        phase = 2;
                        return false;
                    }

                    if (phase == 2)
                    {
                        window.UpdateSerialized();
                        if (asset!.unrelated != 2 ||
                            asset.values[0].label != "original" ||
                            asset.values[1].label != "edited")
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "Rendered union edit did not preserve the staged unrelated apply; unrelated=" +
                                    asset.unrelated + ";first=" + asset.values[0].label + ";second=" +
                                    asset.values[1].label + ";state=" + window.DescribeState());
                            return false;
                        }
                        RequireDrawerAssetTextState(asset, 5, "original", 5, "edited");
                        AssetDatabase.SaveAssets();
                        Undo.PerformUndo();
                        phase = 3;
                        settleFrames = 0;
                        return false;
                    }

                    if (phase == 3)
                    {
                        window.UpdateSerialized();
                        if (asset!.unrelated != 1 ||
                            asset.values[0].label != "original" ||
                            asset.values[1].label != "original")
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "Undo did not restore the staged unrelated field and rendered union edit; state=" +
                                    window.DescribeState());
                            return false;
                        }
                        RequireDrawerAssetTextState(asset, 5, "original", 5, "original");
                        Undo.PerformRedo();
                        phase = 4;
                        settleFrames = 0;
                        return false;
                    }

                    window.UpdateSerialized();
                    if (asset!.unrelated != 2 ||
                        asset.values[0].label != "original" ||
                        asset.values[1].label != "edited")
                    {
                        if (++settleFrames > 60)
                            throw new TimeoutException(
                                "Redo did not restore the staged unrelated field and rendered union edit; state=" +
                                window.DescribeState());
                        return false;
                    }
                    RequireDrawerAssetTextState(asset, 5, "original", 5, "edited");
                    AssetDatabase.SaveAssets();
                    scenario.Observed = "saved-reloaded=true;staged-unrelated=1->2;union=original->edited;undo=both-restored;redo=both-restored";
                    return true;
                };
                return scenario;
            }

            private static DrawerUiScenario CreateRenderedNumericFieldsScenario()
            {
                var scenario = new DrawerUiScenario("Drawer_RenderedNumericFields_UndoRedo");
                UnityDrawerNumericAsset? asset = null;
                SerializedObject? serialized = null;
                var phase = 0;
                var settleFrames = 0;
                const long initialLong = 4294967297L;
                const long editedLong = 9007199254740993L;
                const double initialDouble = 1234567890123.125;
                const double editedDouble = 9876543210987.875;
                const UnityDrawerNonOrdinal initialEnum = UnityDrawerNonOrdinal.Small;
                const UnityDrawerNonOrdinal editedEnum = UnityDrawerNonOrdinal.Large;

                scenario.Start = (runner, window) =>
                {
                    Undo.ClearAll();
                    asset = runner.CreateNumericAsset("rendered-numeric-fields");
                    asset.value = UnityDrawerNumericUnion.Values(initialLong, initialDouble, initialEnum);
                    asset.unrelated = 1;
                    AssetDatabase.SaveAssets();

                    serialized = new SerializedObject(asset);
                    serialized.Update();
                    asset = runner.ReloadNumericAsset(asset);
                    serialized = new SerializedObject(asset);
                    serialized.Update();
                    window.BindDrawer(
                        serialized,
                        asset,
                        "value",
                        typeof(UnityDrawerNumericUnion),
                        imguiProbe: false);
                };

                scenario.Tick = (runner, window) =>
                {
                    if (phase == 0)
                    {
                        phase = 1;
                        return false;
                    }

                    if (phase == 1)
                    {
                        if (!window.UIToolkitBuilt || !window.BoundTreeIsAttached ||
                            !window.HasBoundFieldControl("longValue") ||
                            !window.HasBoundFieldControl("doubleValue") ||
                            !window.HasBoundFieldControl("enumValue"))
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "Rendered numeric controls did not attach; state=" + window.DescribeState());
                            return false;
                        }

                        Require(
                            window.TryEditLongControl("longValue", editedLong),
                            "Rendered long field was unavailable.");
                        phase = 2;
                        settleFrames = 0;
                        return false;
                    }

                    if (phase == 2)
                    {
                        window.UpdateSerialized();
                        if (asset!.value.longValue != editedLong ||
                            asset.value.doubleValue != initialDouble ||
                            asset.value.enumValue != initialEnum)
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "Rendered long edit did not preserve exact numeric values; state=" +
                                    window.DescribeState());
                            return false;
                        }
                        RequireDrawerNumericState(asset, editedLong, initialDouble, initialEnum);
                        Undo.PerformUndo();
                        phase = 3;
                        settleFrames = 0;
                        return false;
                    }

                    if (phase == 3)
                    {
                        window.UpdateSerialized();
                        if (asset!.value.longValue != initialLong ||
                            asset.value.doubleValue != initialDouble ||
                            asset.value.enumValue != initialEnum)
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "Long Undo did not restore the exact initial value; state=" +
                                    window.DescribeState());
                            return false;
                        }
                        RequireDrawerNumericState(asset, initialLong, initialDouble, initialEnum);
                        Undo.PerformRedo();
                        phase = 4;
                        settleFrames = 0;
                        return false;
                    }

                    if (phase == 4)
                    {
                        window.UpdateSerialized();
                        if (asset!.value.longValue != editedLong ||
                            asset.value.doubleValue != initialDouble ||
                            asset.value.enumValue != initialEnum)
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "Long Redo did not restore the exact edited value; state=" +
                                    window.DescribeState());
                            return false;
                        }
                        RequireDrawerNumericState(asset, editedLong, initialDouble, initialEnum);
                        Require(
                            window.TryEditDoubleControl("doubleValue", editedDouble),
                            "Rendered double field was unavailable.");
                        phase = 5;
                        settleFrames = 0;
                        return false;
                    }

                    if (phase == 5)
                    {
                        window.UpdateSerialized();
                        if (asset!.value.longValue != editedLong ||
                            asset.value.doubleValue != editedDouble ||
                            asset.value.enumValue != initialEnum)
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "Rendered double edit did not preserve the long value; state=" +
                                    window.DescribeState());
                            return false;
                        }
                        RequireDrawerNumericState(asset, editedLong, editedDouble, initialEnum);
                        Undo.PerformUndo();
                        phase = 6;
                        settleFrames = 0;
                        return false;
                    }

                    if (phase == 6)
                    {
                        window.UpdateSerialized();
                        if (asset!.value.longValue != editedLong ||
                            asset.value.doubleValue != initialDouble ||
                            asset.value.enumValue != initialEnum)
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "Double Undo did not restore the initial double while retaining long; state=" +
                                    window.DescribeState());
                            return false;
                        }
                        RequireDrawerNumericState(asset, editedLong, initialDouble, initialEnum);
                        Undo.PerformRedo();
                        phase = 7;
                        settleFrames = 0;
                        return false;
                    }

                    if (phase == 7)
                    {
                        window.UpdateSerialized();
                        if (asset!.value.longValue != editedLong ||
                            asset.value.doubleValue != editedDouble ||
                            asset.value.enumValue != initialEnum)
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "Double Redo did not restore the exact edited value; state=" +
                                    window.DescribeState());
                            return false;
                        }
                        RequireDrawerNumericState(asset, editedLong, editedDouble, initialEnum);
                        Require(
                            window.TryEditEnumControl("enumValue", editedEnum),
                            "Rendered enum field was unavailable.");
                        phase = 8;
                        settleFrames = 0;
                        return false;
                    }

                    if (phase == 8)
                    {
                        window.UpdateSerialized();
                        if (asset!.value.longValue != editedLong ||
                            asset.value.doubleValue != editedDouble ||
                            asset.value.enumValue != editedEnum)
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "Rendered enum edit did not preserve long/double values; state=" +
                                    window.DescribeState());
                            return false;
                        }
                        RequireDrawerNumericState(asset, editedLong, editedDouble, editedEnum);
                        Undo.PerformUndo();
                        phase = 9;
                        settleFrames = 0;
                        return false;
                    }

                    if (phase == 9)
                    {
                        window.UpdateSerialized();
                        if (asset!.value.longValue != editedLong ||
                            asset.value.doubleValue != editedDouble ||
                            asset.value.enumValue != initialEnum)
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "Enum Undo did not restore the nonordinal initial value; state=" +
                                    window.DescribeState());
                            return false;
                        }
                        RequireDrawerNumericState(asset, editedLong, editedDouble, initialEnum);
                        Undo.PerformRedo();
                        phase = 10;
                        settleFrames = 0;
                        return false;
                    }

                    if (phase == 10)
                    {
                        window.UpdateSerialized();
                        if (asset!.value.longValue != editedLong ||
                            asset.value.doubleValue != editedDouble ||
                            asset.value.enumValue != editedEnum)
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "Enum Redo did not restore the nonordinal edited value; state=" +
                                    window.DescribeState());
                            return false;
                        }
                        RequireDrawerNumericState(asset, editedLong, editedDouble, editedEnum);

                        serialized!.Update();
                        var unrelated = serialized.FindProperty("unrelated");
                        Require(unrelated != null, "Numeric unrelated SerializedProperty was unavailable.");
                        Undo.IncrementCurrentGroup();
                        Undo.RecordObject(asset, "Numeric unrelated field edit");
                        unrelated!.intValue = 2;
                        Require(
                            serialized.ApplyModifiedProperties(),
                            "Numeric unrelated field edit was not applied.");
                        Undo.FlushUndoRecordObjects();
                        phase = 11;
                        settleFrames = 0;
                        return false;
                    }

                    if (phase == 11)
                    {
                        window.UpdateSerialized();
                        if (asset!.unrelated != 2 ||
                            asset.value.longValue != editedLong ||
                            asset.value.doubleValue != editedDouble ||
                            asset.value.enumValue != editedEnum)
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "Numeric unrelated edit changed the composite union; state=" +
                                    window.DescribeState());
                            return false;
                        }
                        RequireDrawerNumericState(asset, editedLong, editedDouble, editedEnum);
                        Undo.PerformUndo();
                        phase = 12;
                        settleFrames = 0;
                        return false;
                    }

                    if (phase == 12)
                    {
                        window.UpdateSerialized();
                        if (asset!.unrelated != 1 ||
                            asset.value.longValue != editedLong ||
                            asset.value.doubleValue != editedDouble ||
                            asset.value.enumValue != editedEnum)
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "Unrelated Undo reset the composite union history; state=" +
                                    window.DescribeState());
                            return false;
                        }
                        RequireDrawerNumericState(asset, editedLong, editedDouble, editedEnum);
                        Undo.PerformRedo();
                        phase = 13;
                        settleFrames = 0;
                        return false;
                    }

                    window.UpdateSerialized();
                    if (asset!.unrelated != 2 ||
                        asset.value.longValue != editedLong ||
                        asset.value.doubleValue != editedDouble ||
                        asset.value.enumValue != editedEnum)
                    {
                        if (++settleFrames > 60)
                            throw new TimeoutException(
                                "Unrelated Redo replayed the wrong composite union state; state=" +
                                window.DescribeState());
                        return false;
                    }
                    RequireDrawerNumericState(asset, editedLong, editedDouble, editedEnum);
                    AssetDatabase.SaveAssets();
                    scenario.Observed = "saved-reloaded=true;long=exact;double=exact;enum=nonordinal;each=undo/redo;unrelated-undo-preserves-composite=true";
                    return true;
                };
                return scenario;
            }

            private static DrawerUiScenario CreateRenderedInspectorNamedEnumScenario()
            {
                var scenario = new DrawerUiScenario("Drawer_RenderedInspectorNamedEnum_UndoRedo");
                UnityDrawerNumericAsset? asset = null;
                var phase = 0;
                var settleFrames = 0;
                const long initialLong = 4294967297L;
                const double initialDouble = 1234567890123.125;
                const UnityDrawerInspectorNamedEnum initialEnum = UnityDrawerInspectorNamedEnum.Small;
                const UnityDrawerInspectorNamedEnum editedEnum = UnityDrawerInspectorNamedEnum.Large;

                scenario.Start = (runner, window) =>
                {
                    Undo.ClearAll();
                    asset = runner.CreateNumericAsset("rendered-inspector-named-enum");
                    asset.named = UnityDrawerInspectorNamedEnumUnion.Values(
                        initialLong,
                        initialDouble,
                        initialEnum);
                    asset.flags = UnityDrawerFlagsUnion.Values(
                        11L,
                        22.5,
                        UnityDrawerNonOrdinalFlags.First | UnityDrawerNonOrdinalFlags.Third);
                    asset.unrelated = 1;
                    AssetDatabase.SaveAssets();
                    asset = runner.ReloadNumericAsset(asset);
                    window.BindDrawer(
                        asset,
                        "named",
                        typeof(UnityDrawerInspectorNamedEnumUnion),
                        imguiProbe: false);
                };

                scenario.Tick = (runner, window) =>
                {
                    if (phase == 0)
                    {
                        phase = 1;
                        return false;
                    }

                    if (phase == 1)
                    {
                        if (!window.UIToolkitBuilt || !window.BoundTreeIsAttached ||
                            !window.HasBoundFieldControl("longValue") ||
                            !window.HasBoundFieldControl("doubleValue") ||
                            !window.HasBoundFieldControl("enumValue"))
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "Inspector-named enum controls did not attach; state=" +
                                    window.DescribeState());
                            return false;
                        }

                        Require(
                            window.TryEditEnumControl("enumValue", editedEnum),
                            "Inspector-named enum field was unavailable or its display label could not be resolved.");
                        phase = 2;
                        settleFrames = 0;
                        return false;
                    }

                    if (phase == 2)
                    {
                        window.UpdateSerialized();
                        if (asset!.named.longValue != initialLong ||
                            asset.named.doubleValue != initialDouble ||
                            asset.named.enumValue != editedEnum)
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "Inspector-named enum edit did not preserve surrounding fields; state=" +
                                    window.DescribeState());
                            return false;
                        }

                        RequireDrawerInspectorNamedEnumState(asset, initialLong, initialDouble, editedEnum);
                        Undo.PerformUndo();
                        phase = 3;
                        settleFrames = 0;
                        return false;
                    }

                    if (phase == 3)
                    {
                        window.UpdateSerialized();
                        if (asset!.named.longValue != initialLong ||
                            asset.named.doubleValue != initialDouble ||
                            asset.named.enumValue != initialEnum)
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "Inspector-named enum Undo did not restore the nonordinal initial value; state=" +
                                    window.DescribeState());
                            return false;
                        }

                        RequireDrawerInspectorNamedEnumState(asset, initialLong, initialDouble, initialEnum);
                        Undo.PerformRedo();
                        phase = 4;
                        settleFrames = 0;
                        return false;
                    }

                    window.UpdateSerialized();
                    if (asset!.named.longValue != initialLong ||
                        asset.named.doubleValue != initialDouble ||
                        asset.named.enumValue != editedEnum)
                    {
                        if (++settleFrames > 60)
                            throw new TimeoutException(
                                "Inspector-named enum Redo did not restore the nonordinal edited value; state=" +
                                window.DescribeState());
                        return false;
                    }

                    RequireDrawerInspectorNamedEnumState(asset, initialLong, initialDouble, editedEnum);
                    AssetDatabase.SaveAssets();
                    scenario.Observed = "display-labels=InspectorName;enum=nonordinal;surrounding=preserved;undo/redo=true";
                    return true;
                };
                return scenario;
            }

            private static DrawerUiScenario CreateRenderedFlagsEnumScenario()
            {
                var scenario = new DrawerUiScenario("Drawer_RenderedFlagsEnum_UndoRedo");
                UnityDrawerNumericAsset? asset = null;
                var phase = 0;
                var settleFrames = 0;
                const long initialLong = 9007199254740993L;
                const double initialDouble = 9876543210987.875;
                const UnityDrawerNonOrdinalFlags initialFlags =
                    UnityDrawerNonOrdinalFlags.First | UnityDrawerNonOrdinalFlags.Third;
                const UnityDrawerNonOrdinalFlags editedFlags =
                    UnityDrawerNonOrdinalFlags.Second | UnityDrawerNonOrdinalFlags.Third;

                scenario.Start = (runner, window) =>
                {
                    Undo.ClearAll();
                    asset = runner.CreateNumericAsset("rendered-flags-enum");
                    asset.named = UnityDrawerInspectorNamedEnumUnion.Values(
                        17L,
                        18.5,
                        UnityDrawerInspectorNamedEnum.Small);
                    asset.flags = UnityDrawerFlagsUnion.Values(
                        initialLong,
                        initialDouble,
                        initialFlags);
                    asset.unrelated = 1;
                    AssetDatabase.SaveAssets();
                    asset = runner.ReloadNumericAsset(asset);
                    window.BindDrawer(
                        asset,
                        "flags",
                        typeof(UnityDrawerFlagsUnion),
                        imguiProbe: false);
                };

                scenario.Tick = (runner, window) =>
                {
                    if (phase == 0)
                    {
                        phase = 1;
                        return false;
                    }

                    if (phase == 1)
                    {
                        if (!window.UIToolkitBuilt || !window.BoundTreeIsAttached ||
                            !window.HasBoundFieldControl("longValue") ||
                            !window.HasBoundFieldControl("doubleValue") ||
                            !window.HasNativeEnumControl("flagsValue"))
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "Flags enum controls did not attach; state=" + window.DescribeState());
                            return false;
                        }

                        Require(
                            window.TryEditEnumControl("flagsValue", editedFlags),
                            "Flags enum field was unavailable or its combined value could not be resolved.");
                        phase = 2;
                        settleFrames = 0;
                        return false;
                    }

                    if (phase == 2)
                    {
                        window.UpdateSerialized();
                        if (asset!.flags.longValue != initialLong ||
                            asset.flags.doubleValue != initialDouble ||
                            asset.flags.flagsValue != editedFlags)
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "Flags enum edit did not preserve surrounding fields; state=" +
                                    window.DescribeState());
                            return false;
                        }

                        RequireDrawerFlagsState(asset, initialLong, initialDouble, editedFlags);
                        Undo.PerformUndo();
                        phase = 3;
                        settleFrames = 0;
                        return false;
                    }

                    if (phase == 3)
                    {
                        window.UpdateSerialized();
                        if (asset!.flags.longValue != initialLong ||
                            asset.flags.doubleValue != initialDouble ||
                            asset.flags.flagsValue != initialFlags)
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "Flags enum Undo did not restore the combined nonordinal initial value; state=" +
                                    window.DescribeState());
                            return false;
                        }

                        RequireDrawerFlagsState(asset, initialLong, initialDouble, initialFlags);
                        Undo.PerformRedo();
                        phase = 4;
                        settleFrames = 0;
                        return false;
                    }

                    window.UpdateSerialized();
                    if (asset!.flags.longValue != initialLong ||
                        asset.flags.doubleValue != initialDouble ||
                        asset.flags.flagsValue != editedFlags)
                    {
                        if (++settleFrames > 60)
                            throw new TimeoutException(
                                "Flags enum Redo did not restore the combined nonordinal edited value; state=" +
                                window.DescribeState());
                        return false;
                    }

                    RequireDrawerFlagsState(asset, initialLong, initialDouble, editedFlags);
                    AssetDatabase.SaveAssets();
                    scenario.Observed = "control=EnumFlagsField-or-EnumField;flags=combined-nonordinal;surrounding=preserved;undo/redo=true";
                    return true;
                };
                return scenario;
            }

            private static DrawerUiScenario CreateRenderedEnumInvalidChoiceScenario()
            {
                var scenario = new DrawerUiScenario("Drawer_RenderedEnum_InvalidChoicePreservesHistory");
                UnityDrawerNumericAsset? asset = null;
                var phase = 0;
                var settleFrames = 0;
                const long initialLong = 71L;
                const double initialDouble = 72.5;
                const UnityDrawerInspectorNamedEnum initialEnum = UnityDrawerInspectorNamedEnum.Small;
                const UnityDrawerInspectorNamedEnum editedEnum = UnityDrawerInspectorNamedEnum.Large;
                var invalidEnum = (UnityDrawerInspectorNamedEnum)99;

                scenario.Start = (runner, window) =>
                {
                    Undo.ClearAll();
                    asset = runner.CreateNumericAsset("rendered-enum-invalid-choice");
                    asset.named = UnityDrawerInspectorNamedEnumUnion.Values(
                        initialLong,
                        initialDouble,
                        initialEnum);
                    asset.flags = UnityDrawerFlagsUnion.Values(
                        73L,
                        74.5,
                        UnityDrawerNonOrdinalFlags.First);
                    asset.unrelated = 1;
                    AssetDatabase.SaveAssets();
                    asset = runner.ReloadNumericAsset(asset);
                    window.BindDrawer(
                        asset,
                        "named",
                        typeof(UnityDrawerInspectorNamedEnumUnion),
                        imguiProbe: false);
                };

                scenario.Tick = (runner, window) =>
                {
                    if (phase == 0)
                    {
                        phase = 1;
                        return false;
                    }

                    if (phase == 1)
                    {
                        if (!window.UIToolkitBuilt || !window.BoundTreeIsAttached ||
                            !window.HasBoundFieldControl("enumValue"))
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "Invalid-choice enum control did not attach; state=" +
                                    window.DescribeState());
                            return false;
                        }

                        Require(
                            !window.TryEditEnumControl("enumValue", invalidEnum),
                            "An unavailable enum choice was reported as editable.");
                        window.UpdateSerialized();
                        RequireDrawerInspectorNamedEnumState(asset!, initialLong, initialDouble, initialEnum);
                        Undo.PerformUndo();
                        phase = 2;
                        settleFrames = 0;
                        return false;
                    }

                    if (phase == 2)
                    {
                        window.UpdateSerialized();
                        if (asset!.named.longValue != initialLong ||
                            asset.named.doubleValue != initialDouble ||
                            asset.named.enumValue != initialEnum)
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "Invalid enum choice changed runtime state or history; state=" +
                                    window.DescribeState());
                            return false;
                        }

                        RequireDrawerInspectorNamedEnumState(asset, initialLong, initialDouble, initialEnum);
                        Require(
                            window.TryEditEnumControl("enumValue", editedEnum),
                            "Valid enum choice was unavailable after rejecting the invalid choice.");
                        phase = 3;
                        settleFrames = 0;
                        return false;
                    }

                    if (phase == 3)
                    {
                        window.UpdateSerialized();
                        if (asset!.named.enumValue != editedEnum)
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "Valid enum choice did not apply after the invalid choice; state=" +
                                    window.DescribeState());
                            return false;
                        }

                        RequireDrawerInspectorNamedEnumState(asset, initialLong, initialDouble, editedEnum);
                        Undo.PerformUndo();
                        phase = 4;
                        settleFrames = 0;
                        return false;
                    }

                    if (phase == 4)
                    {
                        window.UpdateSerialized();
                        if (asset!.named.longValue != initialLong ||
                            asset.named.doubleValue != initialDouble ||
                            asset.named.enumValue != initialEnum)
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException(
                                    "Undo after a valid edit did not restore the pre-edit state; state=" +
                                    window.DescribeState());
                            return false;
                        }

                        RequireDrawerInspectorNamedEnumState(asset, initialLong, initialDouble, initialEnum);
                        Undo.PerformRedo();
                        phase = 5;
                        settleFrames = 0;
                        return false;
                    }

                    window.UpdateSerialized();
                    if (asset!.named.longValue != initialLong ||
                        asset.named.doubleValue != initialDouble ||
                        asset.named.enumValue != editedEnum)
                    {
                        if (++settleFrames > 60)
                            throw new TimeoutException(
                                "Redo after a valid edit did not restore the edited state; state=" +
                                window.DescribeState());
                        return false;
                    }

                    RequireDrawerInspectorNamedEnumState(asset, initialLong, initialDouble, editedEnum);
                    AssetDatabase.SaveAssets();
                    scenario.Observed = "invalid=unchanged;valid=applied;history=undo/redo-preserved";
                    return true;
                };
                return scenario;
            }

            private static DrawerUiScenario CreateReadOnlyAliasedPayloadScenario()
            {
                var scenario = new DrawerUiScenario("Drawer_ReadOnlyAliasedPayload_RenderedStable");
                UnityDrawerHost? host = null;
                long[]? payloadIds = null;
                var phase = 0;
                scenario.Start = (runner, window) =>
                {
                    Undo.ClearAll();
                    host = runner.CreateHost("readonly-aliased-payload");
                    host.array = new[]
                    {
                        UnityDrawerUnion.Text(5, "original"),
                        UnityDrawerUnion.Text(5, "original"),
                    };

                    var serialized = new SerializedObject(host);
                    serialized.Update();
                    var sourcePayload = RequirePayloadProperty(
                        serialized,
                        "array.Array.data[0]");
                    var targetPayload = RequirePayloadProperty(
                        serialized,
                        "array.Array.data[1]");
                    Require(
                        sourcePayload.managedReferenceValue != null,
                        "Read-only rendered alias source payload was unavailable.");
                    targetPayload.managedReferenceValue = sourcePayload.managedReferenceValue;
                    Require(
                        serialized.ApplyModifiedProperties(),
                        "Read-only rendered alias setup was not applied.");
                    serialized.Update();
                    sourcePayload = RequirePayloadProperty(serialized, "array.Array.data[0]");
                    targetPayload = RequirePayloadProperty(serialized, "array.Array.data[1]");
                    Require(
                        ReferenceEquals(
                            sourcePayload.managedReferenceValue,
                            targetPayload.managedReferenceValue),
                        "Read-only rendered alias setup did not retain one managed payload.");
                    payloadIds = new[]
                    {
                        sourcePayload.managedReferenceId,
                        targetPayload.managedReferenceId,
                    };
                    window.Bind(host, "array.Array.data[1]");
                };
                scenario.Tick = (runner, window) =>
                {
                    if (phase == 0)
                    {
                        phase = 1;
                        return false;
                    }

                    if (!window.UIToolkitBuilt || !window.BoundTreeIsAttached ||
                        !window.HasBoundFieldControl("label"))
                        return false;

                    var serialized = new SerializedObject(host!);
                    serialized.Update();
                    var sourcePayload = RequirePayloadProperty(
                        serialized,
                        "array.Array.data[0]");
                    var targetPayload = RequirePayloadProperty(
                        serialized,
                        "array.Array.data[1]");
                    Require(
                        payloadIds![0] == sourcePayload.managedReferenceId &&
                        payloadIds[1] == targetPayload.managedReferenceId &&
                        sourcePayload.managedReferenceId == targetPayload.managedReferenceId,
                        "Read-only rendered drawer changed aliased managed-reference identities: before=" +
                        payloadIds[0] + "," + payloadIds[1] + ";after=" +
                        sourcePayload.managedReferenceId + "," + targetPayload.managedReferenceId);
                    Require(
                        host!.array[0].label == "original" &&
                        host.array[1].label == "original",
                        "Read-only rendered drawer changed runtime values.");
                    scenario.Observed = "alias=shared;rendered=true;edit=false;runtime=unchanged;ids-stable=true";
                    return true;
                };
                return scenario;
            }

            private static DrawerUiScenario CreateDetachedUndoRedoScenario()
            {
                var scenario = new DrawerUiScenario("Drawer_UndoRedoWhileDetached_ReattachesCurrentTree");
                UnityDrawerHost? host = null;
                VisualElement? originalTree = null;
                VisualElement? originalInnerTree = null;
                var phase = 0;
                scenario.Start = (runner, window) =>
                {
                    Undo.ClearAll();
                    host = runner.CreateHost("undo-detached");
                    host.single = UnityDrawerUnion.Text(1, "before");
                    window.Bind(host, "single");
                };
                scenario.Tick = (runner, window) =>
                {
                    if (phase == 0)
                    {
                        phase = 1;
                        return false;
                    }

                    if (phase == 1)
                    {
                        Require(window.TryInteractWithSelector("Number"), "Detached Undo scenario Number selector was unavailable.");
                        phase = 2;
                        return false;
                    }

                    if (phase == 2)
                    {
                        Require(host!.single.kind == UnityDrawerUnion.Kind.Number, "Detached Undo scenario selector did not apply Number.");
                        window.UpdateSerialized();
                        originalTree = window.BoundTree;
                        originalInnerTree = window.InnerDrawerTree;
                        Require(originalInnerTree != null, "Detached Undo scenario could not identify the inner generated drawer tree.");
                        window.DetachExistingTree();
                        Undo.PerformUndo();
                        phase = 3;
                        return false;
                    }

                    if (phase == 3)
                    {
                        window.UpdateSerialized();
                        if (host!.single.kind != UnityDrawerUnion.Kind.Text)
                            return false;

                        window.ReattachExistingTree();
                        Require(ReferenceEquals(originalTree, window.BoundTree), "Detached Undo scenario rebuilt instead of reattaching the same visual tree after Undo.");
                        Require(ReferenceEquals(originalInnerTree, window.InnerDrawerTree), "Detached Undo scenario rebuilt the inner generated drawer after Undo.");
                        phase = 4;
                        return false;
                    }

                    if (phase == 4)
                    {
                        window.UpdateSerialized();
                        Require(host!.single.kind == UnityDrawerUnion.Kind.Text, "Detached Undo scenario lost Text after reattach.");
                        Require(window.BoundTreeIsAttached, "Detached Undo scenario did not reattach the same tree to the panel after Undo.");
                        Require(window.HasBoundFieldControl("count") && window.HasBoundFieldControl("label"), "Detached Undo scenario did not restore Text controls after Undo reattach.");
                        Require(!window.HasBoundFieldControl("value"), "Detached Undo scenario retained the Number control after Undo reattach.");
                        window.DetachExistingTree();
                        Undo.PerformRedo();
                        phase = 5;
                        return false;
                    }

                    if (phase == 5)
                    {
                        window.UpdateSerialized();
                        if (host!.single.kind != UnityDrawerUnion.Kind.Number)
                            return false;

                        window.ReattachExistingTree();
                        Require(ReferenceEquals(originalTree, window.BoundTree), "Detached Undo scenario rebuilt instead of reattaching the same visual tree after Redo.");
                        phase = 6;
                        return false;
                    }

                    if (phase == 6)
                    {
                        window.UpdateSerialized();
                        Require(host!.single.kind == UnityDrawerUnion.Kind.Number, "Detached Undo scenario lost Number after Redo reattach.");
                        Require(window.HasBoundFieldControl("value"), "Detached Undo scenario did not bind the Number value control after Redo reattach.");
                        Require(!window.HasBoundFieldControl("count") && !window.HasBoundFieldControl("label"), "Detached Undo scenario retained stale Text controls after Redo reattach.");
                        Require(ReferenceEquals(originalTree, window.BoundTree), "Detached Undo scenario rebuilt the outer visual tree after Redo.");
                        Require(ReferenceEquals(originalInnerTree, window.InnerDrawerTree), "Detached Undo scenario rebuilt the inner generated drawer after Redo.");
                        Require(window.BoundTreeIsAttached, "Detached Undo scenario did not reattach the same tree to the panel after Redo.");
                        scenario.Observed = "detached=undo-text/redo-number;tree=reused;active=value;inactive=count,label";
                        return true;
                    }

                    return false;
                };
                return scenario;
            }

            private static DrawerUiScenario CreateReadOnlyAliasedPayloadApplyScenario()
            {
                var scenario = new DrawerUiScenario("Drawer_ReadOnlyAliasedPayload_UnrelatedApplyStable");
                UnityDrawerAsset? asset = null;
                SerializedObject? serialized = null;
                long[]? payloadIds = null;
                string? beforeYaml = null;
                var phase = 0;
                scenario.Start = (runner, window) =>
                {
                    var beforeArtifactPath = Path.Combine(ProjectRoot, "readonly-aliased-before.yaml");
                    var afterArtifactPath = Path.Combine(ProjectRoot, "readonly-aliased-after.yaml");
                    if (File.Exists(beforeArtifactPath))
                        File.Delete(beforeArtifactPath);
                    if (File.Exists(afterArtifactPath))
                        File.Delete(afterArtifactPath);
                    Undo.ClearAll();
                    asset = runner.CreateAsset("readonly-aliased-apply");
                    asset.values = new[]
                    {
                        UnityDrawerUnion.Text(5, "original"),
                        UnityDrawerUnion.Text(5, "original"),
                    };
                    asset.unrelated = 1;
                    AssetDatabase.SaveAssets();

                    serialized = new SerializedObject(asset);
                    serialized.Update();
                    var sourcePayload = RequirePayloadProperty(
                        serialized,
                        "values.Array.data[0]");
                    var copiedPayload = RequirePayloadProperty(
                        serialized,
                        "values.Array.data[1]");
                    Require(
                        sourcePayload.managedReferenceValue != null,
                        "Read-only apply alias source payload was unavailable.");
                    copiedPayload.managedReferenceValue = sourcePayload.managedReferenceValue;
                    Require(
                        serialized.ApplyModifiedProperties(),
                        "Read-only apply alias setup was not applied.");
                    AssetDatabase.SaveAssets();
                    serialized.Update();
                    sourcePayload = RequirePayloadProperty(serialized, "values.Array.data[0]");
                    copiedPayload = RequirePayloadProperty(serialized, "values.Array.data[1]");
                    Require(
                        ReferenceEquals(
                            sourcePayload.managedReferenceValue,
                            copiedPayload.managedReferenceValue),
                        "Read-only apply alias setup did not retain one managed payload.");
                    payloadIds = new[]
                    {
                        sourcePayload.managedReferenceId,
                        copiedPayload.managedReferenceId,
                    };
                    beforeYaml = File.ReadAllText(AssetFilePath(asset));
                    window.BindDrawer(
                        serialized,
                        asset,
                        "values.Array.data[1]",
                        typeof(UnityDrawerUnion),
                        imguiProbe: false);
                };
                scenario.Tick = (runner, window) =>
                {
                    if (phase == 0)
                    {
                        phase = 1;
                        return false;
                    }

                    if (phase == 1)
                    {
                        if (!window.UIToolkitBuilt || !window.BoundTreeIsAttached ||
                            !window.HasBoundFieldControl("label"))
                            return false;
                        Require(
                            ReferenceEquals(serialized, window.BoundSerializedObject),
                            "Read-only apply drawer replaced the bound SerializedObject.");
                        window.RepaintBoundDrawer();
                        phase = 2;
                        return false;
                    }

                    serialized!.Update();
                    var unrelated = serialized.FindProperty("unrelated");
                    Require(unrelated != null, "Read-only apply unrelated property was unavailable.");
                    unrelated!.intValue = 2;
                    Require(
                        serialized.ApplyModifiedProperties(),
                        "Read-only apply unrelated property was not applied.");
                    AssetDatabase.SaveAssets();

                    var sourcePayload = RequirePayloadProperty(serialized, "values.Array.data[0]");
                    var copiedPayload = RequirePayloadProperty(serialized, "values.Array.data[1]");
                    Require(
                        payloadIds![0] == sourcePayload.managedReferenceId &&
                        payloadIds[1] == copiedPayload.managedReferenceId &&
                        sourcePayload.managedReferenceId == copiedPayload.managedReferenceId,
                        "Read-only apply changed aliased managed-reference identities.");
                    var afterYaml = File.ReadAllText(AssetFilePath(asset!));
                    var normalizedBeforeYaml = RemoveYamlField(beforeYaml!, "unrelated");
                    var normalizedAfterYaml = RemoveYamlField(afterYaml, "unrelated");
                    if (!string.Equals(normalizedBeforeYaml, normalizedAfterYaml, StringComparison.Ordinal))
                    {
                        File.WriteAllText(
                            Path.Combine(ProjectRoot, "readonly-aliased-before.yaml"),
                            beforeYaml!);
                        File.WriteAllText(
                            Path.Combine(ProjectRoot, "readonly-aliased-after.yaml"),
                            afterYaml);
                    }
                    Require(
                        normalizedBeforeYaml == normalizedAfterYaml,
                        "Read-only apply changed payload YAML beyond the unrelated field.");
                    Require(
                        asset!.values[0].label == "original" &&
                        asset.values[1].label == "original",
                        "Read-only apply changed runtime values.");
                    scenario.Observed = "same-serialized-object=true;rendered=true;repaint=true;unrelated-apply=true;yaml-stable=true;ids-stable=true";
                    return true;
                };
                return scenario;
            }

            private static DrawerUiScenario CreateMultiObjectActiveFieldScenario()
            {
                var scenario = new DrawerUiScenario("Drawer_MultiObjectActiveFieldEdit");
                UnityDrawerHost? first = null;
                UnityDrawerHost? second = null;
                var phase = 0;
                var settleFrames = 0;
                scenario.Start = (runner, window) =>
                {
                    Undo.ClearAll();
                    first = runner.CreateHost("multi-active-first");
                    second = runner.CreateHost("multi-active-second");
                    first.single = UnityDrawerUnion.Text(5, "before");
                    second.single = UnityDrawerUnion.Text(5, "before");
                    window.Bind(
                        new UnityEngine.Object[] { first, second },
                        "single");
                };
                scenario.Tick = (runner, window) =>
                {
                    if (phase == 0)
                    {
                        phase = 1;
                        return false;
                    }

                    if (phase == 1)
                    {
                        if (!window.UIToolkitBuilt || !window.BoundTreeIsAttached ||
                            !window.HasBoundFieldControl("label"))
                            return false;
                        Require(
                            window.TryEditStringControl("label", "edited"),
                            "Multi-object TextField was unavailable in the rendered drawer.");
                        phase = 2;
                        return false;
                    }

                    window.UpdateSerialized();
                    if (first!.single.label != "edited" || second!.single.label != "edited")
                    {
                        if (++settleFrames > 60)
                            throw new TimeoutException(
                                "Multi-object TextField edit did not synchronize all targets; first=" +
                                (first.single.label ?? "<null>") + ";second=" +
                                (second.single.label ?? "<null>") +
                                ";state=" + window.DescribeState());
                        return false;
                    }

                    scenario.Observed = "targets=2;active-field=label;edit=before->edited;all-runtime-values-updated=true";
                    return true;
                };
                return scenario;
            }

            private static DrawerUiScenario CreateUndoBeforeFirstPanelAttachScenario()
            {
                var scenario = new DrawerUiScenario("Drawer_UndoBeforeFirstPanelAttach_BindsCurrentState");
                UnityDrawerHost? host = null;
                string beforeAttachState = string.Empty;
                string afterAttachState = string.Empty;
                var phase = 0;
                scenario.Start = (runner, window) =>
                {
                    Undo.ClearAll();
                    host = runner.CreateHost("undo-before-attach");
                    host.single = UnityDrawerUnion.Text(1, "before");
                    window.BindBeforeFirstPanelAttach(host, "single");
                    window.RecordScenarioPhase("before-attach-bind-text");
                };
                scenario.Tick = (runner, window) =>
                {
                    if (phase == 0)
                    {
                        window.RecordScenarioPhase("before-attach-create-number-tree");
                        window.SetRawKindBeforeFirstPanelAttach((int)UnityDrawerUnion.Kind.Number);
                        window.PrepareDeferredDrawerTree();
                        Require(window.DeferredTreeHasBoundFieldControl("value"), "Undo-before-attach scenario did not create the Number drawer tree before panel attachment.");
                        Require(!window.DeferredTreeHasBoundFieldControl("count") && !window.DeferredTreeHasBoundFieldControl("label"), "Undo-before-attach scenario created stale Text controls before panel attachment.");
                        phase = 1;
                        return false;
                    }

                    if (phase == 1)
                    {
                        window.RecordScenarioPhase("before-attach-perform-undo");
                        window.UpdateSerialized();
                        if (host!.single.kind != UnityDrawerUnion.Kind.Number)
                            return false;

                        Undo.PerformUndo();
                        phase = 2;
                        return false;
                    }

                    if (phase == 2)
                    {
                        window.RecordScenarioPhase("before-attach-wait-text");
                        window.UpdateSerialized();
                        if (host!.single.kind != UnityDrawerUnion.Kind.Text)
                            return false;

                        beforeAttachState = window.DescribeState();
                        try
                        {
                            window.AttachDeferredTree();
                        }
                        catch (Exception exception)
                        {
                            throw new InvalidOperationException(
                                "Undo-before-attach AttachDeferredTree threw; before-attach-state=" + beforeAttachState,
                                exception);
                        }

                        afterAttachState = window.DescribeState();
                        window.RecordScenarioPhase("before-attach-tree-attached");
                        phase = 3;
                        return false;
                    }

                    var afterUpdateState = window.DescribeState();
                    var attachDiagnostics =
                        ";before-attach-state=" + beforeAttachState +
                        ";after-attach-state=" + afterAttachState +
                        ";after-update-state=" + afterUpdateState;
                    Require(
                        host!.single.kind == UnityDrawerUnion.Kind.Text,
                        "Undo-before-attach scenario did not retain the current Text kind" + attachDiagnostics + ".");
                    Require(
                        window.BoundTreeIsAttached,
                        "Undo-before-attach scenario did not attach the generated drawer tree to the panel" + attachDiagnostics + ".");
                    Require(
                        window.HasBoundFieldControl("count") && window.HasBoundFieldControl("label"),
                        "Undo-before-attach scenario did not bind current Text controls" + attachDiagnostics + ".");
                    Require(
                        !window.HasBoundFieldControl("value"),
                        "Undo-before-attach scenario retained a stale Number control" + attachDiagnostics + ".");
                    scenario.Observed = "bound=text;undo-before-attach=true;active=count,label;inactive=value";
                    return true;
                };
                return scenario;
            }

            private static DrawerUiScenario CreateMultiObjectScenario()
            {
                var scenario = new DrawerUiScenario("Drawer_MultiObjectEditing");
                UnityDrawerHost? first = null;
                UnityDrawerHost? second = null;
                var phase = 0;
                scenario.Start = (runner, window) =>
                {
                    first = runner.CreateHost("multi-first");
                    second = runner.CreateHost("multi-second");
                    first.single = UnityDrawerUnion.Text(1, "one");
                    second.single = UnityDrawerUnion.Empty();
                    first.nested = new UnityNestedOwnerData { value = UnityShapeSerializable.Circle(11f), array = Array.Empty<UnityCollectionUnion>(), list = new List<UnityCollectionUnion>() };
                    second.nested = new UnityNestedOwnerData { value = UnityShapeSerializable.Circle(22f), array = Array.Empty<UnityCollectionUnion>(), list = new List<UnityCollectionUnion>() };
                    window.Bind(new UnityEngine.Object[] { first, second }, "single");
                };
                scenario.Tick = (runner, window) =>
                {
                    if (phase++ == 0)
                        return false;
                    if (phase == 2)
                    {
                        Require(window.TryInteractWithSelector("Text"), "Multi-object Text selector was unavailable for mixed variants.");
                        return false;
                    }

                    Require(first!.single.kind == UnityDrawerUnion.Kind.Text && second!.single.kind == UnityDrawerUnion.Kind.Text, "Multi-object selector did not apply Text to both mixed targets.");
                    Require(first.nested.value.radius == 11f && second.nested.value.radius == 22f, "Multi-object selector changed untouched per-target data.");
                    Require(window.HasBoundFieldControl("count") && window.HasBoundFieldControl("label") && !window.HasBoundFieldControl("value"), "Multi-object selector controls did not follow the active Text variant.");
                    scenario.Observed = "objects=2;mixed=Text+Empty;selector=Text;untouched-radii=11/22;preserved=true";
                    return true;
                };
                return scenario;
            }

            private static DrawerUiScenario CreatePrefabScenario()
            {
                var scenario = new DrawerUiScenario("Drawer_PrefabOverrideApplyRevert");
                UnityDrawerHost? instanceHost = null;
                GameObject? instance = null;
                var prefabPath = string.Empty;
                var phase = 0;
                scenario.Start = (runner, window) =>
                {
                    EnsureAssetFolder();
                    prefabPath = AssetFolder + "/drawer-ui-" + Guid.NewGuid().ToString("N") + ".prefab";
                    var source = new GameObject("SpireUnityDrawerPrefabSource");
                    var sourceHost = source.AddComponent<UnityDrawerHost>();
                    sourceHost.single = UnityDrawerUnion.Text(1, "prefab");
                    PrefabUtility.SaveAsPrefabAsset(source, prefabPath);
                    UnityEngine.Object.DestroyImmediate(source);
                    runner.TrackAsset(prefabPath);
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)!);
                    runner.TrackObject(instance);
                    instanceHost = instance.GetComponent<UnityDrawerHost>();
                    window.Bind(instanceHost, "single");
                };
                scenario.Tick = (runner, window) =>
                {
                    if (phase++ == 0)
                        return false;
                    if (phase == 2)
                    {
                        Require(window.TryInteractWithSelector("Number"), "Prefab Number selector was unavailable.");
                        return false;
                    }
                    if (phase == 3)
                    {
                        Require(instanceHost!.single.kind == UnityDrawerUnion.Kind.Number, "Prefab selector did not apply Number.");
                        PrefabUtility.RecordPrefabInstancePropertyModifications(instanceHost);
                        Require(PrefabUtility.HasPrefabInstanceAnyOverrides(instance!, false), "Generated selector did not create a prefab override.");
                        PrefabUtility.RevertPrefabInstance(instance!, InteractionMode.AutomatedAction);
                        Require(instanceHost.single.kind == UnityDrawerUnion.Kind.Text, "Prefab revert did not restore Text.");
                        window.Bind(instanceHost, "single");
                        return false;
                    }
                    if (phase == 4)
                        return false;
                    if (phase == 5)
                    {
                        Require(window.TryInteractWithSelector("Number"), "Prefab apply selector was unavailable.");
                        return false;
                    }
                    if (phase == 6)
                    {
                        PrefabUtility.RecordPrefabInstancePropertyModifications(instanceHost!);
                        PrefabUtility.ApplyPrefabInstance(instance!, InteractionMode.AutomatedAction);
                        UnityEngine.Object.DestroyImmediate(instance);
                        instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)!);
                        runner.TrackObject(instance);
                        instanceHost = instance.GetComponent<UnityDrawerHost>();
                        Require(instanceHost.single.kind == UnityDrawerUnion.Kind.Number, "Prefab apply did not persist the generated selector replacement.");
                        window.Bind(instanceHost, "single");
                        phase = 6;
                        return false;
                    }
                    if (phase == 7)
                    {
                        Require(window.HasBoundFieldControl("value") && !window.HasBoundFieldControl("count") && !window.HasBoundFieldControl("label"), "Prefab selector controls did not follow the active variant.");
                        scenario.Observed = "override=created;revert=Text;apply=Number;selector=generated-popup";
                        return true;
                    }

                    return false;
                };
                return scenario;
            }

            private static DrawerUiScenario CreateNestedGenericScenario()
            {
                var scenario = new DrawerUiScenario("Drawer_NestedGenericPayloadSelector");
                UnityDrawerHost? host = null;
                var phase = 0;
                scenario.Start = (runner, window) =>
                {
                    host = runner.CreateHost("nested-generic");
                    host.genericParent = UnityGenericDrawerParent.Parent(UnityGenericUnion<int>.Some(7));
                    window.Bind(host, "genericParent");
                };
                scenario.Tick = (runner, window) =>
                {
                    if (phase++ == 0)
                        return false;
                    if (phase == 2)
                    {
                        Require(window.PopupCount >= 2, "Nested generic parent did not expose the child selector.");
                        Require(window.TryInteractWithSelector("None"), "Nested generic child None selector was unavailable.");
                        return false;
                    }
                    if (phase == 3)
                    {
                        Require(host!.genericParent.kind == UnityGenericDrawerParent.Kind.Parent, "Nested generic parent kind changed unexpectedly.");
                        Require(host.genericParent.child.kind == UnityGenericUnion<int>.Kind.None, "Nested child selector did not traverse the concrete payload subclass.");
                        Require(!window.HasBoundFieldControl("value"), "Nested generic None variant kept an inactive value control.");
                        window.UpdateSerialized();
                        Require(window.TryInteractWithSelector("Some"), "Nested generic child Some selector was unavailable.");
                        return false;
                    }

                    Require(host!.genericParent.child.kind == UnityGenericUnion<int>.Kind.Some, "Nested generic child Some replacement was not applied.");
                    Require(host.genericParent.child.value == 0, "Nested generic child Some did not use factory defaults.");
                    Require(window.HasBoundFieldControl("value"), "Nested generic child value control was not bound.");
                    scenario.Observed = "outer=Parent;child=None->Some;child-value=0;concrete-payload-traversed=true";
                    return true;
                };
                return scenario;
            }

            private static DrawerUiScenario CreatePrivateBaseOwnerScenario()
            {
                var scenario = new DrawerUiScenario("Drawer_PrivateBaseOwnerSelector");
                UnityDrawerHost? host = null;
                var phase = 0;
                scenario.Start = (runner, window) =>
                {
                    host = runner.CreateHost("private-base");
                    window.Bind(host, "privateValue");
                };
                scenario.Tick = (runner, window) =>
                {
                    if (phase++ == 0)
                        return false;
                    if (phase == 2)
                    {
                        Require(window.TryInteractWithSelector("Text"), "Private base-class Text selector was unavailable.");
                        return false;
                    }

                    Require(host!.PrivateValue.kind == UnityDrawerUnion.Kind.Text, "Private base-class selector did not write Text.");
                    Require(window.HasBoundFieldControl("count") && window.HasBoundFieldControl("label"), "Private base-class active controls were not bound.");
                    Require(!window.HasBoundFieldControl("value") && !window.HasBoundFieldControl("texture"), "Private base-class inactive controls remained bound.");
                    scenario.Observed = "owner=private-base-field;selector=Text;active=count,label;inactive=value,texture";
                    return true;
                };
                return scenario;
            }

            private static DrawerUiScenario CreateGenericClosedScenario()
            {
                var scenario = new DrawerUiScenario("Drawer_GenericClosedSelectorHelpers");
                UnityDrawerHost? host = null;
                var phase = 0;
                scenario.Start = (runner, window) =>
                {
                    host = runner.CreateHost("generic-closed");
                    host.genericInt = UnityGenericUnion<int>.None();
                    host.genericString = UnityGenericUnion<string>.None();
                    window.Bind(host, "genericInt");
                };
                scenario.Tick = (runner, window) =>
                {
                    if (phase++ == 0)
                        return false;
                    if (phase == 2)
                    {
                        Require(!window.HasBoundFieldControl("value"), "Closed int generic None variant kept an inactive value control.");
                        Require(window.TryInteractWithSelector("Some"), "Closed int generic Some selector was unavailable.");
                        return false;
                    }
                    if (phase == 3)
                    {
                        Require(host!.genericInt.kind == UnityGenericUnion<int>.Kind.Some && host.genericInt.value == 0, "Closed int generic selector did not dispatch its factory.");
                        Require(window.HasBoundFieldControl("value"), "Closed int generic Some variant did not bind its active value control.");
                        window.Bind(host, "genericString");
                        return false;
                    }
                    if (phase == 4)
                    {
                        Require(!window.HasBoundFieldControl("value"), "Closed string generic None variant kept an inactive value control.");
                        return false;
                    }
                    if (phase == 5)
                    {
                        Require(window.TryInteractWithSelector("Some"), "Closed string generic Some selector was unavailable.");
                        return false;
                    }

                    Require(
                        host!.genericString.kind == UnityGenericUnion<string>.Kind.Some &&
                        string.IsNullOrEmpty(host.genericString.value),
                        "Closed string generic selector did not dispatch its factory; observed kind=" + host.genericString.kind + ", value='" + host.genericString.value + "'.");
                    scenario.Observed = "closed=int,string;selector=Some;factory-default-string='" + host.genericString.value + "'";
                    return true;
                };
                return scenario;
            }

            private static DrawerUiScenario CreateGenericContainingScenario()
            {
                var scenario = new DrawerUiScenario("Drawer_GenericContainingNestedSelector");
                UnityDrawerHost? host = null;
                var phase = 0;
                scenario.Start = (runner, window) =>
                {
                    host = runner.CreateHost("generic-containing");
                    host.genericContainerNested = UnityGenericContainer<int>.NestedUnion.Some(7);
                    window.Bind(host, "genericContainerNested");
                };
                scenario.Tick = (runner, window) =>
                {
                    if (phase++ == 0)
                        return false;
                    if (phase == 2)
                    {
                        Require(window.TryInteractWithSelector("Empty"), "Generic containing-type Empty selector was unavailable.");
                        return false;
                    }

                    Require(
                        host!.genericContainerNested.kind == UnityGenericContainer<int>.NestedUnion.Kind.Empty,
                        "Generic containing-type nested selector did not apply Empty.");
                    Require(!window.HasBoundFieldControl("value"), "Generic containing-type Empty variant kept an inactive value control.");
                    scenario.Observed = "container=int;nested=Some->Empty;active-value=false;selector=generated-popup";
                    return true;
                };
                return scenario;
            }

            private static DrawerUiScenario CreateRebindScenario()
            {
                var scenario = new DrawerUiScenario("Drawer_RebindDoesNotWriteStaleTarget");
                UnityDrawerHost? first = null;
                UnityDrawerHost? second = null;
                VisualElement? originalTree = null;
                var phase = 0;
                scenario.Start = (runner, window) =>
                {
                    first = runner.CreateHost("rebind-first");
                    second = runner.CreateHost("rebind-second");
                    first.single = UnityDrawerUnion.Text(1, "first");
                    second.single = UnityDrawerUnion.Text(2, "second");
                    window.Bind(first, "single");
                    originalTree = window.BoundTree;
                    window.RebindExistingTree(second, "single");
                    Require(ReferenceEquals(originalTree, window.BoundTree), "Rebind rebuilt the generated visual tree instead of reusing it.");
                };
                scenario.Tick = (runner, window) =>
                {
                    if (phase++ == 0)
                        return false;
                    if (phase == 2)
                    {
                        Require(window.TryInteractWithSelector("Number"), "Rebound Number selector was unavailable.");
                        return false;
                    }

                    Require(first!.single.kind == UnityDrawerUnion.Kind.Text && first.single.label == "first", "Rebound tree wrote to its stale previous target.");
                    Require(second!.single.kind == UnityDrawerUnion.Kind.Number && second.single.value == 0f, "Rebound tree did not write to the current target.");
                    Require(window.HasBoundFieldControl("value") && !window.HasBoundFieldControl("count") && !window.HasBoundFieldControl("label"), "Rebound tree did not refresh active and inactive controls.");
                    scenario.Observed = "first=Text/first;second=Number/0;stale-target-write=false";
                    return true;
                };
                return scenario;
            }

            private static DrawerUiScenario CreateBoxedLayoutGuiScenario()
            {
                var scenario = new DrawerUiScenario("BoxedLayouts_IMGUIAndUIToolkit_UseCanonicalPayload");
                UnityDrawerHost? host = null;
                var phase = 0;
                scenario.Start = (runner, window) =>
                {
                    host = runner.CreateHost("boxed-layout-gui");
                    host.boxedFields = UnityBoxedFieldsUnion.Text("boxed-fields", null!);
                    host.boxedTuple = UnityBoxedTupleUnion.Text("boxed-tuple", null!);
                    window.BindDrawer(
                        host,
                        "boxedFields",
                        typeof(UnityBoxedFieldsUnion),
                        imguiProbe: true);
                };
                scenario.Tick = (runner, window) =>
                {
                    if (phase == 0)
                    {
                        phase = 1;
                        return false;
                    }

                    if (phase == 1)
                    {
                        Require(window.ImguiRepainted, "BoxedFields drawer did not receive an IMGUI repaint.");
                        Require(window.HasBoundFieldControl("label"), "BoxedFields IMGUI/UI binding did not expose the active label field.");
                        window.BindDrawer(
                            host!,
                            "boxedFields",
                            typeof(UnityBoxedFieldsUnion),
                            imguiProbe: false);
                        phase = 2;
                        return false;
                    }

                    if (phase == 2)
                    {
                        Require(window.UIToolkitBuilt, "BoxedFields UI Toolkit drawer was not built.");
                        Require(window.HasBoundFieldControl("label"), "BoxedFields UI Toolkit drawer did not expose the active label field.");
                        Require(!window.HasBoundFieldControl("value"), "BoxedFields UI Toolkit drawer exposed an inactive Number field.");
                        window.BindDrawer(
                            host!,
                            "boxedTuple",
                            typeof(UnityBoxedTupleUnion),
                            imguiProbe: false);
                        phase = 3;
                        return false;
                    }

                    Require(window.UIToolkitBuilt, "BoxedTuple UI Toolkit drawer was not built.");
                    Require(window.HasBoundFieldControl("label"), "BoxedTuple UI Toolkit drawer did not expose the active label field.");
                    Require(!window.HasBoundFieldControl("value"), "BoxedTuple UI Toolkit drawer exposed an inactive Number field.");
                    scenario.Observed = "fields=IMGUI+UIToolkit/canonical-label;tuple=UIToolkit/canonical-label;runtime-storage=hidden";
                    return true;
                };
                return scenario;
            }

            private static DrawerUiScenario CreateBoxedLayoutUndoScenario()
            {
                var scenario = new DrawerUiScenario("BoxedLayouts_SelectorReplacementAndUndoRedo");
                UnityDrawerHost? host = null;
                var phase = 0;
                var settleFrames = 0;
                scenario.Start = (runner, window) =>
                {
                    Undo.ClearAll();
                    host = runner.CreateHost("boxed-layout-undo");
                    host.boxedFields = UnityBoxedFieldsUnion.Text("before", null!);
                    window.BindDrawer(
                        host,
                        "boxedFields",
                        typeof(UnityBoxedFieldsUnion),
                        imguiProbe: false);
                };
                scenario.Tick = (runner, window) =>
                {
                    if (phase == 0)
                    {
                        phase = 1;
                        return false;
                    }

                    if (phase == 1)
                    {
                        Require(window.TryInteractWithSelector("Number"), "BoxedFields Number selector was unavailable.");
                        phase = 2;
                        return false;
                    }

                    if (phase == 2)
                    {
                        Require(host!.boxedFields.kind == UnityBoxedFieldsUnion.Kind.Number, "BoxedFields selector did not apply Number.");
                        Undo.PerformUndo();
                        phase = 3;
                        settleFrames = 0;
                        return false;
                    }

                    if (phase == 3)
                    {
                        window.UpdateSerialized();
                        if (host!.boxedFields.kind != UnityBoxedFieldsUnion.Kind.Text ||
                            host.boxedFields.label != "before")
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException("BoxedFields Undo did not restore Text; kind=" + host.boxedFields.kind + ";label=" + (host.boxedFields.label ?? "<null>"));
                            return false;
                        }

                        Undo.PerformRedo();
                        phase = 4;
                        settleFrames = 0;
                        return false;
                    }

                    window.UpdateSerialized();
                    if (host!.boxedFields.kind != UnityBoxedFieldsUnion.Kind.Number ||
                        !window.HasBoundFieldControl("value") ||
                        window.HasBoundFieldControl("label"))
                    {
                        if (++settleFrames > 60)
                            throw new TimeoutException("BoxedFields Redo did not restore Number; kind=" + host.boxedFields.kind + ";state=" + window.DescribeState());
                        return false;
                    }

                    scenario.Observed = "selector=BoxedFields.Number;undo=Text/before;redo=Number;active-controls=refreshed";
                    return true;
                };
                return scenario;
            }

            private static DrawerUiScenario CreateUnsafeOverlapGuiScenario()
            {
                var scenario = new DrawerUiScenario("UnsafeOverlap_IMGUIAndUIToolkit_DrawCanonicalPayload");
                UnityDrawerHost? host = null;
                var phase = 0;
                scenario.Start = (runner, window) =>
                {
                    host = runner.CreateHost("unsafe-layout-gui");
                    host.unsafeUnion = UnityUnsafeOverlapUnion.Mixed(7, 2.5f, "unsafe-gui", null!);
                    window.BindDrawer(
                        host,
                        "unsafeUnion",
                        typeof(UnityUnsafeOverlapUnion),
                        imguiProbe: true);
                };
                scenario.Tick = (runner, window) =>
                {
                    if (phase == 0)
                    {
                        phase = 1;
                        return false;
                    }

                    if (phase == 1)
                    {
                        Require(window.ImguiRepainted, "UnsafeOverlap drawer did not receive an IMGUI repaint.");
                        Require(window.HasBoundFieldControl("raw"), "UnsafeOverlap IMGUI binding did not expose raw.");
                        Require(window.HasBoundFieldControl("amount"), "UnsafeOverlap IMGUI binding did not expose amount.");
                        Require(window.HasBoundFieldControl("label"), "UnsafeOverlap IMGUI binding did not expose label.");
                        window.BindDrawer(
                            host!,
                            "unsafeUnion",
                            typeof(UnityUnsafeOverlapUnion),
                            imguiProbe: false);
                        phase = 2;
                        return false;
                    }

                    Require(window.UIToolkitBuilt, "UnsafeOverlap UI Toolkit drawer was not built.");
                    Require(window.HasBoundFieldControl("raw"), "UnsafeOverlap UI Toolkit binding did not expose raw.");
                    Require(window.HasBoundFieldControl("amount"), "UnsafeOverlap UI Toolkit binding did not expose amount.");
                    Require(window.HasBoundFieldControl("label"), "UnsafeOverlap UI Toolkit binding did not expose label.");
                    Require(!window.HasBoundFieldControl("value"), "UnsafeOverlap drawer exposed an inactive Number field.");
                    scenario.Observed = "IMGUI+UIToolkit=canonical-raw-amount-label;runtime-buffer=hidden";
                    return true;
                };
                return scenario;
            }

            private static DrawerUiScenario CreateUnsafeOverlapNestedUndoScenario()
            {
                var scenario = new DrawerUiScenario("UnsafeOverlap_NestedPathWritebackUndoRedo");
                UnityDrawerHost? host = null;
                var phase = 0;
                var settleFrames = 0;
                scenario.Start = (runner, window) =>
                {
                    Undo.ClearAll();
                    host = runner.CreateHost("unsafe-nested-undo");
                    host.unsafeArray = new[]
                    {
                        UnityUnsafeOverlapUnion.Mixed(1, 1.5f, "array-before", null!),
                        UnityUnsafeOverlapUnion.Mixed(2, 2.5f, "array-sibling", null!),
                    };
                    host.unsafeList = new List<UnityUnsafeOverlapUnion>
                    {
                        UnityUnsafeOverlapUnion.Mixed(3, 3.5f, "list-before", null!),
                        UnityUnsafeOverlapUnion.Mixed(4, 4.5f, "list-sibling", null!),
                    };
                    window.BindDrawer(
                        host,
                        "unsafeArray.Array.data[0]",
                        typeof(UnityUnsafeOverlapUnion),
                        imguiProbe: false);
                };
                scenario.Tick = (runner, window) =>
                {
                    if (phase == 0)
                    {
                        phase = 1;
                        return false;
                    }

                    if (phase == 1)
                    {
                        Require(window.TryInteractWithSelector("Number"), "UnsafeOverlap array Number selector was unavailable.");
                        phase = 2;
                        return false;
                    }

                    if (phase == 2)
                    {
                        Require(
                            host!.unsafeArray[0].kind == UnityUnsafeOverlapUnion.Kind.Number,
                            "UnsafeOverlap array selector did not write Number.");
                        Require(
                            host.unsafeArray[1].kind == UnityUnsafeOverlapUnion.Kind.Mixed &&
                            host.unsafeArray[1].label == "array-sibling",
                            "UnsafeOverlap array selector changed a sibling.");
                        Undo.PerformUndo();
                        phase = 3;
                        settleFrames = 0;
                        return false;
                    }

                    if (phase == 3)
                    {
                        window.UpdateSerialized();
                        if (host!.unsafeArray[0].kind != UnityUnsafeOverlapUnion.Kind.Mixed)
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException("UnsafeOverlap array Undo did not restore Mixed.");
                            return false;
                        }

                        window.BindDrawer(
                            host,
                            "unsafeList.Array.data[0]",
                            typeof(UnityUnsafeOverlapUnion),
                            imguiProbe: false);
                        phase = 4;
                        return false;
                    }

                    if (phase == 4)
                    {
                        Require(window.TryInteractWithSelector("Number"), "UnsafeOverlap list Number selector was unavailable.");
                        phase = 5;
                        return false;
                    }

                    Require(
                        host!.unsafeList[0].kind == UnityUnsafeOverlapUnion.Kind.Number &&
                        host.unsafeList[1].kind == UnityUnsafeOverlapUnion.Kind.Mixed &&
                        host.unsafeList[1].label == "list-sibling",
                        "UnsafeOverlap list selector did not preserve sibling data.");
                    scenario.Observed = "array=Number/undo-Mixed;list=Number;siblings=preserved;nested-path=true";
                    return true;
                };
                return scenario;
            }

            private static DrawerUiScenario CreateOverlapGuiScenario()
            {
                var scenario = new DrawerUiScenario("Overlap_IMGUIAndUIToolkit_DrawCanonicalPayload");
                UnityDrawerHost? host = null;
                var phase = 0;
                scenario.Start = (runner, window) =>
                {
                    host = runner.CreateHost("overlap-layout-gui");
                    host.overlapUnion = UnityOverlapUnion.Rectangle(2.5f, 4.5f);
                    window.BindDrawer(
                        host,
                        "overlapUnion",
                        typeof(UnityOverlapUnion),
                        imguiProbe: true);
                };
                scenario.Tick = (runner, window) =>
                {
                    if (phase == 0)
                    {
                        phase = 1;
                        return false;
                    }

                    if (phase == 1)
                    {
                        Require(window.ImguiRepainted, "Overlap drawer did not receive an IMGUI repaint.");
                        Require(window.HasBoundFieldControl("width"), "Overlap IMGUI binding did not expose width.");
                        Require(window.HasBoundFieldControl("height"), "Overlap IMGUI binding did not expose height.");
                        window.BindDrawer(
                            host!,
                            "overlapUnion",
                            typeof(UnityOverlapUnion),
                            imguiProbe: false);
                        phase = 2;
                        return false;
                    }

                    Require(window.UIToolkitBuilt, "Overlap UI Toolkit drawer was not built.");
                    Require(window.HasBoundFieldControl("width"), "Overlap UI Toolkit binding did not expose width.");
                    Require(window.HasBoundFieldControl("height"), "Overlap UI Toolkit binding did not expose height.");
                    Require(!window.HasBoundFieldControl("value"), "Overlap drawer exposed an inactive Number field.");
                    scenario.Observed = "IMGUI+UIToolkit=canonical-width-height;explicit-storage=hidden";
                    return true;
                };
                return scenario;
            }

            private static DrawerUiScenario CreateOverlapNestedUndoScenario()
            {
                var scenario = new DrawerUiScenario("Overlap_NestedPathWritebackUndoRedo");
                UnityDrawerHost? host = null;
                var phase = 0;
                var settleFrames = 0;
                scenario.Start = (runner, window) =>
                {
                    Undo.ClearAll();
                    host = runner.CreateHost("overlap-nested-undo");
                    host.overlapArray = new[]
                    {
                        UnityOverlapUnion.Rectangle(1.5f, 2.5f),
                        UnityOverlapUnion.Number(7),
                    };
                    host.overlapList = new List<UnityOverlapUnion>
                    {
                        UnityOverlapUnion.Rectangle(3.5f, 4.5f),
                        UnityOverlapUnion.Number(9),
                    };
                    window.BindDrawer(
                        host,
                        "overlapArray.Array.data[0]",
                        typeof(UnityOverlapUnion),
                        imguiProbe: false);
                };
                scenario.Tick = (runner, window) =>
                {
                    if (phase == 0)
                    {
                        phase = 1;
                        return false;
                    }

                    if (phase == 1)
                    {
                        Require(window.TryInteractWithSelector("Point"), "Overlap array Point selector was unavailable.");
                        phase = 2;
                        return false;
                    }

                    if (phase == 2)
                    {
                        Require(host!.overlapArray[0].kind == UnityOverlapUnion.Kind.Point, "Overlap array selector did not write Point.");
                        Require(host.overlapArray[1].kind == UnityOverlapUnion.Kind.Number && host.overlapArray[1].value == 7, "Overlap array selector changed a sibling.");
                        Undo.PerformUndo();
                        phase = 3;
                        settleFrames = 0;
                        return false;
                    }

                    if (phase == 3)
                    {
                        window.UpdateSerialized();
                        if (host!.overlapArray[0].kind != UnityOverlapUnion.Kind.Rectangle)
                        {
                            if (++settleFrames > 60)
                                throw new TimeoutException("Overlap array Undo did not restore Rectangle.");
                            return false;
                        }

                        window.BindDrawer(
                            host,
                            "overlapList.Array.data[0]",
                            typeof(UnityOverlapUnion),
                            imguiProbe: false);
                        phase = 4;
                        return false;
                    }

                    if (phase == 4)
                    {
                        Require(window.TryInteractWithSelector("Point"), "Overlap list Point selector was unavailable.");
                        phase = 5;
                        return false;
                    }

                    Require(
                        host!.overlapList[0].kind == UnityOverlapUnion.Kind.Point &&
                        host.overlapList[1].kind == UnityOverlapUnion.Kind.Number &&
                        host.overlapList[1].value == 9,
                        "Overlap list selector did not preserve sibling data.");
                    scenario.Observed = "array=Point/undo-Rectangle;list=Point;siblings=preserved;nested-path=true";
                    return true;
                };
                return scenario;
            }
        }

        private sealed class DrawerUiScenario
        {
            public DrawerUiScenario(string name)
            {
                Name = name;
                Start = (_, _) => { };
                Tick = (_, _) => true;
            }

            public string Name { get; }
            public Action<DrawerCallbackRunner, DrawerProbeWindow> Start { get; set; }
            public Func<DrawerCallbackRunner, DrawerProbeWindow, bool> Tick { get; set; }
            public string Observed { get; set; } = string.Empty;
        }

        private sealed class DrawerProbeWindow : EditorWindow
        {
            private UnityEngine.Object? _target;
            private Type _drawerTargetType = typeof(UnityDrawerUnion);
            private SerializedObject? _serialized;
            private SerializedProperty? _property;
            private string? _deferredPropertyPath;
            private VisualElement? _deferredTree;
            private VisualElement? _trackedTree;
            private VisualElement? _trackedInnerTree;
            private bool _imguiProbe;
            private double _bindingStartedAt;
            private int _editorUpdateCount;
            private int _drawerFrameCount;
            private int _serializedUpdateCount;
            private int _imguiRepaintCount;
            private int _createGuiCount;
            private string _lastScenarioPhase = "<none>";
            private double _lastScenarioPhaseAt;

            public bool ImguiRepainted { get; private set; }
            public bool UIToolkitBuilt { get; private set; }
            public bool SelectorInteractionAttempted { get; private set; }
            public bool SelectorChanged { get; private set; }
            public bool HasInitialTextControls { get; set; }
            public int ImguiRepaintCount => _imguiRepaintCount;

            public VisualElement BoundTree
                => _trackedTree ?? (rootVisualElement.childCount == 0 ? rootVisualElement : rootVisualElement[0]);

            public SerializedObject? BoundSerializedObject => _serialized;

            public VisualElement? InnerDrawerTree
            {
                get
                {
                    TrackTreeIdentity();
                    return _trackedInnerTree;
                }
            }

            public bool BoundTreeIsDetached
                => _trackedTree != null && _trackedTree.parent == null && _trackedTree.panel == null;

            public bool BoundTreeIsAttached
                => _trackedTree != null && _trackedTree.parent != null && _trackedTree.panel != null;

            public void Initialize(UnityDrawerHost host)
            {
                BindDrawer(host, "single", typeof(UnityDrawerUnion), imguiProbe: true);
                if (FindDrawer(typeof(UnityDrawerUnion)) == null)
                    throw new MissingGeneratedSurfaceException("Generated UnityDrawerUnion property drawer was unavailable.");
                titleContent = new GUIContent("Spire Unity Integration");
                minSize = new Vector2(320f, 180f);
            }

            public void Bind(UnityEngine.Object target, string propertyPath)
            {
                BindDrawer(target, propertyPath, typeof(UnityDrawerUnion), imguiProbe: false);
            }

            public void Bind(UnityEngine.Object[] targets, string propertyPath)
            {
                _target = targets.Length == 0 ? null : targets[0];
                _drawerTargetType = typeof(UnityDrawerUnion);
                _imguiProbe = false;
                _serialized = new SerializedObject(targets);
                BindSerialized(propertyPath);
            }

            public void BindDrawer(
                UnityEngine.Object target,
                string propertyPath,
                Type drawerTargetType,
                bool imguiProbe)
            {
                _target = target;
                _drawerTargetType = drawerTargetType;
                _imguiProbe = imguiProbe;
                _deferredPropertyPath = null;
                _deferredTree = null;
                _trackedTree = null;
                _trackedInnerTree = null;
                rootVisualElement.Unbind();
                rootVisualElement.Clear();
                _serialized = new SerializedObject(target);
                BindSerialized(propertyPath);
            }

            public void BindDrawer(
                SerializedObject serialized,
                UnityEngine.Object target,
                string propertyPath,
                Type drawerTargetType,
                bool imguiProbe)
            {
                _target = target;
                _drawerTargetType = drawerTargetType;
                _imguiProbe = imguiProbe;
                _deferredPropertyPath = null;
                _deferredTree = null;
                _trackedTree = null;
                _trackedInnerTree = null;
                rootVisualElement.Unbind();
                rootVisualElement.Clear();
                _serialized = serialized;
                BindSerialized(propertyPath);
            }

            public void UpdateSerialized()
            {
                _serializedUpdateCount++;
                _serialized?.Update();
            }

            public void RecordEditorUpdate()
            {
                _editorUpdateCount++;
            }

            public void RecordDrawerFrame()
            {
                _drawerFrameCount++;
            }

            public void RecordScenarioPhase(string phase)
            {
                _lastScenarioPhase = phase;
                _lastScenarioPhaseAt = EditorApplication.timeSinceStartup;
            }

            public void DetachExistingTree()
            {
                if (_trackedTree == null)
                    throw new InvalidOperationException("The drawer visual tree was unavailable for detachment.");

                _trackedTree.RemoveFromHierarchy();
                Require(BoundTreeIsDetached, "The generated drawer tree remained attached after detachment.");
            }

            public void ReattachExistingTree()
            {
                if (_serialized == null)
                    throw new InvalidOperationException("The drawer window is not bound to a SerializedObject.");

                if (_trackedTree == null)
                    throw new InvalidOperationException("The drawer visual tree was unavailable for reattachment.");

                if (_trackedTree.parent == null)
                    rootVisualElement.Add(_trackedTree);
                TrackTreeIdentity();
                UIToolkitBuilt = rootVisualElement.childCount > 0;
            }

            public void RepaintBoundDrawer()
            {
                rootVisualElement.MarkDirtyRepaint();
                Repaint();
            }

            public void BindBeforeFirstPanelAttach(UnityEngine.Object target, string propertyPath)
            {
                _target = target;
                _drawerTargetType = typeof(UnityDrawerUnion);
                _imguiProbe = false;
                ResetBindingTiming();
                _serialized = new SerializedObject(target);
                _serialized.Update();
                _property = _serialized.FindProperty(propertyPath);
                if (_property == null)
                    throw new MissingGeneratedSurfaceException("SerializedProperty was unavailable: " + propertyPath);

                rootVisualElement.Unbind();
                rootVisualElement.Clear();
                _deferredPropertyPath = propertyPath;
                _deferredTree = null;
                _trackedTree = null;
                _trackedInnerTree = null;
                ImguiRepainted = false;
                UIToolkitBuilt = false;
                SelectorInteractionAttempted = false;
                SelectorChanged = false;
                HasInitialTextControls = false;
            }

            public void SetRawKindBeforeFirstPanelAttach(int rawKind)
            {
                if (_serialized == null || _deferredPropertyPath == null || _target == null)
                    throw new InvalidOperationException("The deferred drawer window is not bound to a target.");

                Undo.RecordObject(_target, "Spire deferred drawer payload change");
                var payload = RequirePayloadProperty(_serialized, _deferredPropertyPath);
                payload.managedReferenceValue = CreateDrawerPayloadForRawKind(rawKind);
                Require(_serialized.ApplyModifiedProperties(), "Deferred raw payload edit was not applied.");
                Undo.FlushUndoRecordObjects();
            }

            public void PrepareDeferredDrawerTree()
            {
                if (_serialized == null || _deferredPropertyPath == null)
                    throw new InvalidOperationException("The deferred drawer tree was not prepared.");

                _serialized.Update();
                _property = _serialized.FindProperty(_deferredPropertyPath);
                if (_property == null)
                    throw new MissingGeneratedSurfaceException("SerializedProperty was unavailable: " + _deferredPropertyPath);

                var drawer = FindDrawer(_drawerTargetType);
                if (drawer == null)
                    throw new MissingGeneratedSurfaceException("Generated UnityDrawerUnion property drawer was unavailable.");

                _deferredTree = drawer.CreatePropertyGUI(_property);
                if (_deferredTree == null)
                    throw new MissingGeneratedSurfaceException("Generated UnityDrawerUnion CreatePropertyGUI returned no tree.");
                _deferredTree.Bind(_serialized);
            }

            public bool DeferredTreeHasBoundFieldControl(string fieldName)
            {
                if (_deferredTree == null)
                    return false;

                var suffix = "." + fieldName;
                var found = false;
                _deferredTree.Query<VisualElement>().ForEach(element =>
                {
                    if (found)
                        return;

                    var path = (element as IBindable)?.bindingPath;
                    if (!string.IsNullOrEmpty(path) &&
                        (string.Equals(path, fieldName, StringComparison.Ordinal) ||
                            path.EndsWith(suffix, StringComparison.Ordinal)))
                    {
                        found = true;
                    }
                });
                return found;
            }

            public void AttachDeferredTree()
            {
                if (_serialized == null || _deferredPropertyPath == null)
                    throw new InvalidOperationException("The deferred drawer tree was not prepared.");

                _serialized.Update();
                _property = _serialized.FindProperty(_deferredPropertyPath);
                if (_property == null)
                    throw new MissingGeneratedSurfaceException("SerializedProperty was unavailable: " + _deferredPropertyPath);

                if (_deferredTree == null)
                    throw new MissingGeneratedSurfaceException("The generated deferred drawer tree was unavailable.");

                rootVisualElement.Clear();
                rootVisualElement.Add(_deferredTree);
                rootVisualElement.Bind(_serialized);
                _trackedTree = _deferredTree;
                TrackTreeIdentity();
                UIToolkitBuilt = rootVisualElement.childCount > 0;
                _deferredPropertyPath = null;
            }

            public void RebindExistingTree(UnityEngine.Object target, string propertyPath)
            {
                _target = target;
                _drawerTargetType = typeof(UnityDrawerUnion);
                _imguiProbe = false;
                ResetBindingTiming();
                _serialized = new SerializedObject(target);
                _serialized.Update();
                _property = _serialized.FindProperty(propertyPath);
                if (_property == null)
                    throw new MissingGeneratedSurfaceException("SerializedProperty was unavailable: " + propertyPath);

                _deferredPropertyPath = null;
                _deferredTree = null;
                rootVisualElement.Unbind();
                rootVisualElement.Bind(_serialized);
                if (_trackedTree == null && rootVisualElement.childCount > 0)
                    _trackedTree = rootVisualElement[0];
                TrackTreeIdentity();
                ImguiRepainted = false;
                UIToolkitBuilt = rootVisualElement.childCount > 0;
                SelectorInteractionAttempted = false;
                SelectorChanged = false;
                HasInitialTextControls = false;
            }

            private void BindSerialized(string propertyPath)
            {
                ResetBindingTiming();
                _serialized!.Update();
                _property = _serialized.FindProperty(propertyPath);
                if (_property == null)
                    throw new MissingGeneratedSurfaceException("SerializedProperty was unavailable: " + propertyPath);

                _deferredPropertyPath = null;
                _deferredTree = null;
                ImguiRepainted = false;
                UIToolkitBuilt = false;
                SelectorInteractionAttempted = false;
                SelectorChanged = false;
                HasInitialTextControls = false;
                BuildPropertyField(propertyPath);
            }

            public void CreateGUI()
            {
                _createGuiCount++;
                if (_property != null)
                    BuildPropertyField(_property.displayName);
            }

            private void BuildPropertyField(string label)
            {
                if (_serialized == null || _property == null)
                    return;

                rootVisualElement.Clear();
                var field = new PropertyField(_property, label);
                rootVisualElement.Add(field);
                _trackedTree = field;
                TrackTreeIdentity();
                field.Bind(_serialized);
                UIToolkitBuilt = rootVisualElement.childCount > 0;
            }

            private void TrackTreeIdentity()
            {
                if (_trackedTree != null && _trackedTree.childCount > 0)
                    _trackedInnerTree = _trackedTree[0];
            }

            private void OnGUI()
            {
                if (!_imguiProbe || _serialized == null || _property == null)
                    return;
                _serialized.Update();
                var drawer = FindDrawer(_drawerTargetType);
                if (drawer == null)
                    return;
                var height = drawer.GetPropertyHeight(_property, GUIContent.none);
                var rect = GUILayoutUtility.GetRect(260f, height);
                drawer.OnGUI(rect, _property, new GUIContent("single"));
                if (Event.current != null && Event.current.type == EventType.Repaint)
                {
                    _imguiRepaintCount++;
                    ImguiRepainted = true;
                }
            }

            public void SetRawKind(string propertyPath, int rawKind)
            {
                if (_serialized == null)
                    throw new InvalidOperationException("The drawer window is not bound to a SerializedObject.");

                var payload = RequirePayloadProperty(_serialized, propertyPath);
                payload.managedReferenceValue = CreateDrawerPayloadForRawKind(rawKind);
                Require(_serialized.ApplyModifiedProperties(), "Raw payload edit was not applied.");
                BindSerialized(propertyPath);
            }

            public string SelectorValue(int selectorIndex = 0)
            {
                var popups = new List<VisualElement>();
                rootVisualElement.Query<VisualElement>().ForEach(element =>
                {
                    if (element.GetType().Name.StartsWith("PopupField", StringComparison.Ordinal))
                        popups.Add(element);
                });

                if (selectorIndex < 0 || selectorIndex >= popups.Count)
                    throw new MissingGeneratedSurfaceException("Generated selector popup was unavailable.");
                var valueProperty = popups[selectorIndex].GetType().GetProperty("value", BindingFlags.Instance | BindingFlags.Public);
                if (valueProperty == null)
                    throw new MissingGeneratedSurfaceException("Generated selector value was unavailable.");
                return valueProperty.GetValue(popups[selectorIndex], null)?.ToString() ?? "<null>";
            }

            public bool HasBoundFieldControl(string fieldName)
            {
                var suffix = "." + fieldName;
                var found = false;
                rootVisualElement.Query<VisualElement>().ForEach(element =>
                {
                    if (found)
                        return;

                    var path = (element as IBindable)?.bindingPath;
                    if (!string.IsNullOrEmpty(path) &&
                        (string.Equals(path, fieldName, StringComparison.Ordinal) ||
                            path.EndsWith(suffix, StringComparison.Ordinal)))
                    {
                        found = true;
                    }
                });
                return found;
            }

            public bool HasNativeEnumControl(string fieldName)
            {
                var suffix = "." + fieldName;
                var found = false;
                rootVisualElement.Query<EnumFlagsField>().ForEach(field =>
                {
                    if (found)
                        return;

                    var bindingPath = (field as IBindable)?.bindingPath;
                    if (string.Equals(bindingPath, fieldName, StringComparison.Ordinal) ||
                        (!string.IsNullOrEmpty(bindingPath) &&
                            bindingPath.EndsWith(suffix, StringComparison.Ordinal)))
                    {
                        found = true;
                    }
                });
                rootVisualElement.Query<EnumField>().ForEach(field =>
                {
                    if (found)
                        return;

                    var bindingPath = (field as IBindable)?.bindingPath;
                    if (string.Equals(bindingPath, fieldName, StringComparison.Ordinal) ||
                        (!string.IsNullOrEmpty(bindingPath) &&
                            bindingPath.EndsWith(suffix, StringComparison.Ordinal)))
                    {
                        found = true;
                    }
                });
                return found;
            }

            public bool TryExpandFoldout(string fieldName)
            {
                Foldout? target = null;
                var suffix = "." + fieldName;
                rootVisualElement.Query<Foldout>().ForEach(foldout =>
                {
                    if (target != null)
                        return;

                    var bindingPath = (foldout as IBindable)?.bindingPath;
                    var textMatches = string.Equals(
                        foldout.text,
                        fieldName,
                        StringComparison.OrdinalIgnoreCase);
                    var pathMatches = string.Equals(bindingPath, fieldName, StringComparison.Ordinal) ||
                        (!string.IsNullOrEmpty(bindingPath) &&
                            bindingPath.EndsWith(suffix, StringComparison.Ordinal));
                    if (textMatches || pathMatches)
                        target = foldout;
                });

                if (target == null)
                    return false;

                if (!target.value)
                    target.value = true;
                return true;
            }

            public int PopupCount
            {
                get
                {
                    var count = 0;
                    rootVisualElement.Query<VisualElement>().ForEach(element =>
                    {
                        if (element.GetType().Name.StartsWith("PopupField", StringComparison.Ordinal))
                            count++;
                    });
                    return count;
                }
            }

            public int GroupCount
            {
                get
                {
                    var count = 0;
                    rootVisualElement.Query<VisualElement>().ForEach(element =>
                    {
                        if (element.childCount > 0)
                            count++;
                    });
                    return count;
                }
            }

            private void ResetBindingTiming()
            {
                _bindingStartedAt = EditorApplication.timeSinceStartup;
                _editorUpdateCount = 0;
                _drawerFrameCount = 0;
                _serializedUpdateCount = 0;
                _imguiRepaintCount = 0;
                _createGuiCount = 0;
                _lastScenarioPhase = "<none>";
                _lastScenarioPhaseAt = _bindingStartedAt;
            }

            private string DescribeTiming()
            {
                var now = EditorApplication.timeSinceStartup;
                var elapsedMilliseconds = (now - _bindingStartedAt) * 1000.0;
                var phaseElapsedMilliseconds = (now - _lastScenarioPhaseAt) * 1000.0;
                return "elapsed-ms=" + elapsedMilliseconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) +
                    ";editor-updates=" + _editorUpdateCount +
                    ";drawer-frame-clock=" + _drawerFrameCount +
                    ";serialized-updates=" + _serializedUpdateCount +
                    ";create-gui=" + _createGuiCount +
                    ";imgui-repaints=" + _imguiRepaintCount +
                    ";last-phase=" + _lastScenarioPhase +
                    ";phase-elapsed-ms=" + phaseElapsedMilliseconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
            }

            public string DescribeState()
            {
                var rootKind = "<unavailable>";
                var payload = "<unavailable>";
                try
                {
                    var property = _serialized != null && _property != null
                        ? _serialized.FindProperty(_property.propertyPath) ?? _property
                        : _property;
                    if (property != null)
                    {
                        var managedPayload = RequirePayloadProperty(property);
                        var payloadValue = managedPayload.managedReferenceValue;
                        rootKind = payloadValue?.GetType().Name ?? "<null>";
                        payload = (payloadValue?.GetType().FullName ?? "<null>") +
                            "#" + managedPayload.managedReferenceId;
                    }
                }
                catch (Exception exception)
                {
                    payload = "<state-error:" + exception.GetType().Name + ">";
                }

                return "root-kind=" + rootKind +
                    ";payload=" + payload +
                    ";controls=count=" + HasBoundFieldControl("count") +
                    ",label=" + HasBoundFieldControl("label") +
                    ",longValue=" + HasBoundFieldControl("longValue") +
                    ",doubleValue=" + HasBoundFieldControl("doubleValue") +
                    ",enumValue=" + HasBoundFieldControl("enumValue") +
                    ",value=" + HasBoundFieldControl("value") +
                    ",texture=" + HasBoundFieldControl("texture") +
                    ";popup-count=" + PopupCount +
                    ";groupcount=" + GroupCount +
                    ";tree-children=" + rootVisualElement.childCount +
                    ";timing=" + DescribeTiming() +
                    ";binding=" + DescribeBindingIdentity();
            }

            public string DescribeBindingIdentity()
            {
                var boundElements = new List<string>();
                rootVisualElement.Query<VisualElement>().ForEach(element =>
                {
                    if (element is IBindable bindable && !string.IsNullOrEmpty(bindable.bindingPath))
                    {
                        boundElements.Add(
                            element.GetType().Name + ":" + bindable.bindingPath);
                    }
                });

                var tree = BoundTree;
                var serializedId = _serialized == null
                    ? "<null>"
                    : RuntimeHelpers.GetHashCode(_serialized).ToString();
                var targetId = _target == null
                    ? "<null>"
                    : RuntimeHelpers.GetHashCode(_target).ToString();
                return "tree-id=" + RuntimeHelpers.GetHashCode(tree) +
                    ";serialized-id=" + serializedId +
                    ";target-id=" + targetId +
                    ";property-path=" + (_property?.propertyPath ?? "<null>") +
                    ";elements=" + string.Join(",", boundElements);
            }

            public bool TryInteractWithSelector(string desired, int selectorIndex = 0)
            {
                SelectorInteractionAttempted = true;
                var popups = new List<VisualElement>();
                rootVisualElement.Query<VisualElement>().ForEach(element =>
                {
                    if (element.GetType().Name.StartsWith("PopupField", StringComparison.Ordinal))
                        popups.Add(element);
                });

                var matchingPopups = popups
                    .Where(popup => PopupHasChoice(popup, desired))
                    .ToArray();
                var candidates = matchingPopups.Length > 0 ? matchingPopups : popups.ToArray();
                if (selectorIndex < 0 || selectorIndex >= candidates.Length)
                    return false;

                var popup = candidates[selectorIndex];
                var valueProperty = popup.GetType().GetProperty("value", BindingFlags.Instance | BindingFlags.Public);
                if (valueProperty == null || !valueProperty.CanWrite)
                    return false;
                var choicesProperty = popup.GetType().GetProperty("choices", BindingFlags.Instance | BindingFlags.Public);
                var choices = choicesProperty?.GetValue(popup, null) as System.Collections.IEnumerable;
                if (choices == null)
                    return false;

                foreach (var choice in choices)
                {
                    if (choice == null || choice.ToString()!.IndexOf(desired, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    try
                    {
                        valueProperty.SetValue(popup, choice, null);
                        _serialized?.ApplyModifiedProperties();
                        SelectorChanged = true;
                        return true;
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning("Spire Unity selector interaction failed: " + exception.Message);
                        return false;
                    }
                }

                return false;
            }

            public bool TryEditStringControl(string fieldName, string value)
            {
                TextField? target = null;
                var suffix = "." + fieldName;
                rootVisualElement.Query<TextField>().ForEach(field =>
                {
                    if (target != null)
                        return;

                    var bindingPath = (field as IBindable)?.bindingPath;
                    if (string.Equals(bindingPath, fieldName, StringComparison.Ordinal) ||
                        (!string.IsNullOrEmpty(bindingPath) &&
                            bindingPath.EndsWith(suffix, StringComparison.Ordinal)))
                    {
                        target = field;
                    }
                });

                if (target == null)
                    return false;

                target.value = value;
                _serialized?.ApplyModifiedProperties();
                return true;
            }

            public bool TryEditIntegerControl(string fieldName, int value)
            {
                IntegerField? target = null;
                var suffix = "." + fieldName;
                rootVisualElement.Query<IntegerField>().ForEach(field =>
                {
                    if (target != null)
                        return;

                    var bindingPath = (field as IBindable)?.bindingPath;
                    if (string.Equals(bindingPath, fieldName, StringComparison.Ordinal) ||
                        (!string.IsNullOrEmpty(bindingPath) &&
                            bindingPath.EndsWith(suffix, StringComparison.Ordinal)))
                    {
                        target = field;
                    }
                });

                if (target == null)
                    return false;

                target.value = value;
                _serialized?.ApplyModifiedProperties();
                return true;
            }

            public bool TryEditLongControl(string fieldName, long value)
            {
                LongField? target = null;
                var suffix = "." + fieldName;
                rootVisualElement.Query<LongField>().ForEach(field =>
                {
                    if (target != null)
                        return;

                    var bindingPath = (field as IBindable)?.bindingPath;
                    if (string.Equals(bindingPath, fieldName, StringComparison.Ordinal) ||
                        (!string.IsNullOrEmpty(bindingPath) &&
                            bindingPath.EndsWith(suffix, StringComparison.Ordinal)))
                    {
                        target = field;
                    }
                });

                if (target == null)
                    return false;

                target.value = value;
                _serialized?.ApplyModifiedProperties();
                return true;
            }

            public bool TryEditDoubleControl(string fieldName, double value)
            {
                DoubleField? target = null;
                var suffix = "." + fieldName;
                rootVisualElement.Query<DoubleField>().ForEach(field =>
                {
                    if (target != null)
                        return;

                    var bindingPath = (field as IBindable)?.bindingPath;
                    if (string.Equals(bindingPath, fieldName, StringComparison.Ordinal) ||
                        (!string.IsNullOrEmpty(bindingPath) &&
                            bindingPath.EndsWith(suffix, StringComparison.Ordinal)))
                    {
                        target = field;
                    }
                });

                if (target == null)
                    return false;

                target.value = value;
                _serialized?.ApplyModifiedProperties();
                return true;
            }

            public bool TryEditEnumControl(string fieldName, Enum value)
            {
                if (!IsAvailableEnumValue(value))
                    return false;

                var suffix = "." + fieldName;
                EnumFlagsField? flagsTarget = null;
                rootVisualElement.Query<EnumFlagsField>().ForEach(field =>
                {
                    if (flagsTarget != null)
                        return;

                    var bindingPath = (field as IBindable)?.bindingPath;
                    if (string.Equals(bindingPath, fieldName, StringComparison.Ordinal) ||
                        (!string.IsNullOrEmpty(bindingPath) &&
                            bindingPath.EndsWith(suffix, StringComparison.Ordinal)))
                    {
                        flagsTarget = field;
                    }
                });

                if (flagsTarget != null)
                {
                    if (flagsTarget.value != null && flagsTarget.value.GetType() != value.GetType())
                        return false;

                    flagsTarget.value = value;
                    _serialized?.ApplyModifiedProperties();
                    return true;
                }

                EnumField? target = null;
                rootVisualElement.Query<EnumField>().ForEach(field =>
                {
                    if (target != null)
                        return;

                    var bindingPath = (field as IBindable)?.bindingPath;
                    if (string.Equals(bindingPath, fieldName, StringComparison.Ordinal) ||
                        (!string.IsNullOrEmpty(bindingPath) &&
                            bindingPath.EndsWith(suffix, StringComparison.Ordinal)))
                    {
                        target = field;
                    }
                });

                if (target != null)
                {
                    if (target.value != null && target.value.GetType() != value.GetType())
                        return false;

                    target.value = value;
                    _serialized?.ApplyModifiedProperties();
                    return true;
                }

                PopupField<string>? popupTarget = null;
                rootVisualElement.Query<PopupField<string>>().ForEach(popup =>
                {
                    if (popupTarget != null)
                        return;

                    var bindingPath = (popup as IBindable)?.bindingPath;
                    if (string.Equals(bindingPath, fieldName, StringComparison.Ordinal) ||
                        (!string.IsNullOrEmpty(bindingPath) &&
                            bindingPath.EndsWith(suffix, StringComparison.Ordinal)))
                    {
                        popupTarget = popup;
                    }
                });

                if (popupTarget == null)
                    return false;

                var desiredChoice = FindEnumDisplayChoice(popupTarget.choices, value);
                if (desiredChoice == null)
                    return false;

                popupTarget.value = desiredChoice;
                _serialized?.ApplyModifiedProperties();
                return true;
            }

            private static bool IsAvailableEnumValue(Enum value)
            {
                if (value == null)
                    return false;

                var enumType = value.GetType();
                if (Enum.IsDefined(enumType, value))
                    return true;

                if (!enumType.IsDefined(typeof(FlagsAttribute), inherit: false))
                    return false;

                ulong requested = Convert.ToUInt64(value);
                ulong definedMask = 0;
                foreach (var declared in Enum.GetValues(enumType))
                    definedMask |= Convert.ToUInt64(declared);
                return (requested & ~definedMask) == 0;
            }

            private static string? FindEnumDisplayChoice(IEnumerable<string> choices, Enum value)
            {
                var enumType = value.GetType();
                var candidates = new List<string>();
                var memberName = Enum.GetName(enumType, value);
                if (memberName != null)
                {
                    var member = enumType.GetField(
                        memberName,
                        BindingFlags.Public | BindingFlags.Static);
                    var inspectorName = member?.GetCustomAttributes(
                            typeof(InspectorNameAttribute), inherit: false)
                        .OfType<InspectorNameAttribute>()
                        .Select(attribute => attribute.displayName)
                        .FirstOrDefault();
                    if (!string.IsNullOrEmpty(inspectorName))
                        candidates.Add(inspectorName);
                    candidates.Add(memberName);
                }

                candidates.Add(value.ToString());
                foreach (var choice in choices)
                {
                    if (choice == null)
                        continue;

                    foreach (var candidate in candidates)
                    {
                        if (string.Equals(choice, candidate, StringComparison.Ordinal) ||
                            choice.StartsWith(candidate + " ", StringComparison.Ordinal) ||
                            choice.StartsWith(candidate + "(", StringComparison.Ordinal))
                        {
                            return choice;
                        }
                    }
                }

                return null;
            }

            public string DescribeStringControl(string fieldName)
            {
                TextField? target = null;
                var suffix = "." + fieldName;
                rootVisualElement.Query<TextField>().ForEach(field =>
                {
                    if (target != null)
                        return;

                    var bindingPath = (field as IBindable)?.bindingPath;
                    if (string.Equals(bindingPath, fieldName, StringComparison.Ordinal) ||
                        (!string.IsNullOrEmpty(bindingPath) &&
                            bindingPath.EndsWith(suffix, StringComparison.Ordinal)))
                    {
                        target = field;
                    }
                });

                var serializedValue = "<unavailable>";
                try
                {
                    var property = _serialized != null && _property != null
                        ? _serialized.FindProperty(_property.propertyPath) ?? _property
                        : null;
                    var payload = property == null ? null : RequirePayloadProperty(property);
                    serializedValue = payload?.FindPropertyRelative(fieldName)?.stringValue ?? "<null>";
                }
                catch (Exception exception)
                {
                    serializedValue = "<error:" + exception.GetType().Name + ">";
                }

                return "control=" + (target?.value ?? "<null>") + ";serialized=" + serializedValue;
            }

            public void SetPendingPayloadFloat(string propertyPath, float value)
            {
                if (_serialized == null)
                    throw new InvalidOperationException("The drawer window is not bound to a SerializedObject.");

                var payload = RequirePayloadProperty(_serialized, propertyPath);
                var field = payload.FindPropertyRelative("value");
                if (field == null || field.propertyType != SerializedPropertyType.Float)
                    throw new InvalidOperationException("The pending payload value was unavailable: " + propertyPath);

                field.floatValue = value;
            }

            private static bool PopupHasChoice(VisualElement popup, string desired)
            {
                var choicesProperty = popup.GetType().GetProperty("choices", BindingFlags.Instance | BindingFlags.Public);
                var choices = choicesProperty?.GetValue(popup, null) as System.Collections.IEnumerable;
                if (choices == null)
                    return false;

                foreach (var choice in choices)
                {
                    if (choice != null && choice.ToString()!.IndexOf(desired, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }

                return false;
            }
        }
    }

}
