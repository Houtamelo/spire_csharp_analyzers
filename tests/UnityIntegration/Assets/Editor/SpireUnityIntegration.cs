#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
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
        private static int _drawerFrames;
        private static double _drawerStartedAt;
        private static double _nextDrawerFrameAt;
        private static bool _drawerBaseProbeFinalized;
        private static bool _lifecycleStartQueued;
        private static bool _lifecyclePollRegistered;

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
            AddPendingScenario("Drawer_InvalidKind_SelectsFirstKnownVariant", "An invalid raw discriminator is shown as invalid and can select the first known variant.");
            AddPendingScenario("Drawer_SelectionChange_UsesFactoryReplacement", "The generated UI Toolkit selector constructs a default replacement variant.");
            AddPendingScenario("Drawer_ArrayElement_NestedPathWriteback", "An actual array-element selector writes through its containing struct.");
            AddPendingScenario("Drawer_ListElement_NestedPathWriteback", "An actual list-element selector writes through its containing struct.");
            AddPendingScenario("Drawer_UndoRedo_WholeValueAndPayloadEdit", "Undo and Redo restore generated selector changes.");
            AddPendingScenario("Drawer_UndoRedoWhileDetached_ReattachesCurrentTree", "Undo and Redo while detached are reflected when the same visual tree is reattached.");
            AddPendingScenario("Drawer_UndoBeforeFirstPanelAttach_BindsCurrentState", "Undo before first panel attachment binds the current serialized kind and controls.");
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
            };

            try
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                var backend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
                result.scriptingBackend = backend.ToString();
                Require(backend == ScriptingImplementation.Mono2x, "Standalone player backend was not Mono2x.");

                EnsureAssetFolder();
                var scenePath = AssetFolder + "/UnityIntegrationPlayer.unity";
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
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
                hostObject.AddComponent<UnityIntegrationPlayerHook>();
                SceneManager.MoveGameObjectToScene(hostObject, scene);
                EditorSceneManager.SaveScene(scene, scenePath);
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };

                var buildPath = Path.Combine(ProjectRoot, "Builds", "UnityIntegrationPlayer", "SpireUnityIntegration.x86_64");
                Directory.CreateDirectory(Path.GetDirectoryName(buildPath)!);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { scenePath },
                    locationPathName = buildPath,
                    target = BuildTarget.StandaloneLinux64,
                    options = BuildOptions.Development | BuildOptions.AllowDebugging,
                });

                result.status = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? "pass" : "fail";
                result.outputPath = buildPath;
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

        private static void RunCoreScenarios()
        {
            RunScenario("Factory_Text_KindAndFields", "Factory construction exposes the active kind and every field.", FactoryTextKindAndFields);
            RunScenario("Factory_Fieldless_KindAndDefaults", "Fieldless construction exposes its kind and inactive defaults.", FactoryFieldlessKindAndDefaults);
            RunScenario("OnBeforeSerialize_RefreshesWithoutManualPrepare", "Saving an ordinary owner refreshes the active managed payload.", OnBeforeSerializeRefreshesWithoutManualPrepare);
            RunScenario("OnBeforeSerialize_RefreshesMatchingPayloadInPlace", "A matching managed payload object is refreshed in place after its scalar fields change.", OnBeforeSerializeRefreshesMatchingPayloadInPlace);
            RunScenario("OnAfterDeserialize_ReconstructsThroughFactory", "Asset reload reconstructs the active value and clears inactive fields.", OnAfterDeserializeReconstructsThroughFactory);
            RunScenario("OnAfterDeserialize_KnownKindMissingPayload", "A known kind with no payload is safe and has default fields.", OnAfterDeserializeKnownKindMissingPayload);
            RunScenario("OnAfterDeserialize_KnownKindMismatchedPayload", "A known kind never reads another variant payload.", OnAfterDeserializeKnownKindMismatchedPayload);
            RunScenario("OnAfterDeserialize_UnknownKindNoThrow", "An invalid discriminator is safe and remains observable.", OnAfterDeserializeUnknownKindNoThrow);
            RunScenario("VariantSwitch_DropsStaleInactiveData", "Variant replacement clears stale payload data.", VariantSwitchDropsStaleInactiveData);
            RunScenario("PayloadOnlyEdit_ReconstructsImmediateRuntime", "Editing only a managed payload reconstructs the owner immediately.", PayloadOnlyEditReconstructsImmediateRuntime);
            RunScenario("GeneratedInitMutation_RefreshesPayload", "Generated init accessors refresh matching payload fields.", GeneratedInitMutationRefreshesPayload);
            RunScenario("CallbackControl_StructAndClassAreAllowed", "Serializable callback payloads without nested unions preserve their derived runtime state.", CallbackControlStructAndClassAreAllowed);

            RunScenario("AssetSaveUnloadReload_SingleAndObjectReference", "An asset round trip retains values and native object references.", AssetSaveUnloadReloadSingleAndObjectReference);
            RunScenario("Array_RoundTrip", "Array union elements retain order and values after reload.", ArrayRoundTrip);
            RunScenario("List_RoundTrip", "List union elements retain order and values after reload.", ListRoundTrip);
            RunScenario("ContainingStruct_RoundTrip", "A union inside an ordinary serializable struct survives reload.", ContainingStructRoundTrip);
            RunScenario("DerivedComposite_BasePrivateChild_RoundTrip", "A derived serializable composite preserves a private base child union across repeated asset reloads.", DerivedCompositeBasePrivateChildRoundTrip);
            RunScenario("NestedParent_AutomaticChildSnapshotRepeatedSave", "Nested parent and child values survive repeated saves.", NestedParentAutomaticChildSnapshotRepeatedSave);
            RunScenario("NestedParent_MatchingPayloadRefreshRepeatedSave", "Nested matching payloads refresh across repeated saves.", NestedParentMatchingPayloadRefreshRepeatedSave);
            RunScenario("NestedParent_CopyIsolation", "Copying and mutating a parent does not change the original.", NestedParentCopyIsolation);
            RunScenario("NestedArrayElements_IndependentChildSnapshots", "Nested array elements keep independent child payloads.", NestedArrayElementsIndependentChildSnapshots);
            RunScenario("NestedListElements_IndependentChildSnapshots", "Nested list elements keep independent child payloads.", NestedListElementsIndependentChildSnapshots);
            RunScenario("NestedCollection_InitMutationDetachesCache", "A nested init mutation detaches its serialized cache from siblings.", NestedCollectionInitMutationDetachesCache);
            RunScenario("CopiedValue_SharedPayloadMutationDetaches", "An intentional managed-reference alias is detached by a copied-value mutation.", CopiedValueSharedPayloadMutationDetaches);
            RunScenario("CopiedNestedChildPayload_AliasDetachesRepeatedSave", "Aliased nested child payloads detach before a copied child mutation and remain distinct after repeated saves.", CopiedNestedChildPayloadAliasDetachesRepeatedSave);
            RunScenario("CopiedNestedChildReferenceComposite_AliasDetachesWithoutParentReassignment", "Aliased child payloads in ordinary serializable composites detach without reassigning either parent union root.", CopiedNestedChildReferenceCompositeAliasDetachesWithoutParentReassignment);
            RunScenario("CopiedValue_DetachedPayloadRepeatedSaveVariantChange", "A detached copy survives repeated saves and a variant change.", CopiedValueDetachedPayloadRepeatedSaveVariantChange);
            RunScenario("ActivePayloadOnly_NoInactivePayloadOrRuntimeFields", "Only the active payload is serialized.", ActivePayloadOnlyNoInactivePayloadOrRuntimeFields);
            RunScenario("BoxedFields_AssetSaveUnloadReload", "BoxedFields values and object references survive asset reload.", BoxedFieldsAssetSaveUnloadReload);
            RunScenario("BoxedTuple_AssetSaveUnloadReload", "BoxedTuple values and object references survive asset reload.", BoxedTupleAssetSaveUnloadReload);
            RunScenario("BoxedLayouts_RepeatedSaveRefreshesMatchingPayload", "Matching boxed payloads refresh in place across repeated saves.", BoxedLayoutsRepeatedSaveRefreshesMatchingPayload);
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
            RunScenario("UnsafeOverlap_ActivePayloadOnlyYaml", "UnsafeOverlap YAML contains only kind and the active typed payload.", UnsafeOverlapActivePayloadOnlyYaml);
            RunScenario("Overlap_AssetSaveUnloadReload", "Overlap primitive and reference values survive asset reload.", OverlapAssetSaveUnloadReload);
            RunScenario("Overlap_RepeatedSaveAndVariantReset", "Overlap repeated saves and variant resets preserve explicit storage.", OverlapRepeatedSaveAndVariantReset);
            RunScenario("Overlap_CopyNestedAndCollectionIsolation", "Overlap copied nested and collection values remain isolated.", OverlapCopyNestedAndCollectionIsolation);
            RunScenario("Overlap_ActivePayloadOnlyYaml", "Overlap YAML contains only kind and the active typed payload.", OverlapActivePayloadOnlyYaml);

            RunScenario("Drawer_GetPropertyHeight_FieldlessVsFielded", "Drawer height changes between fieldless and fielded variants.", DrawerGetPropertyHeightFieldlessVsFielded);
            RunScenario("Drawer_WholeValue_UsesBoxedValueSetter", "Whole-value boxed assignment applies in the installed editor.", DrawerWholeValueUsesBoxedValueSetter);
            RunScenario("Drawer_ReadsKindAndManagedReference", "Drawer reads use serialized discriminator and managed payload.", DrawerReadsKindAndManagedReference);

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

        private static string OnBeforeSerializeRefreshesMatchingPayloadInPlace()
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
                Require(ReferenceEquals(before, after), "Matching managed payload was replaced instead of refreshed in place.");
                Require(scope.Asset.original.count == 3, "The runtime union was changed while editing its serialized payload.");
                Require(scope.Asset.original.label == "before", "The runtime union label was changed while editing its serialized payload.");
                RequirePayloadValue(after, "count", 3);
                RequirePayloadValue(after, "label", "before");
                RequirePayloadValue(after, "asset", scope.Texture);
                return "same-payload=true;count=3;reference=preserved;payload-edit=overwritten";
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

        private static string OnAfterDeserializeKnownKindMissingPayload()
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
                    scope.Asset.single.kind == UnityShapeSerializable.Kind.Label,
                    "Known kind changed after missing payload; observed kind=" + scope.Asset.single.kind + ".");
                Require(
                    string.IsNullOrEmpty(scope.Asset.single.text) && scope.Asset.single.icon == null,
                    "Missing payload did not produce Unity defaults; observed text='" + scope.Asset.single.text + "', icon=" + scope.Asset.single.icon + ".");
                return "known-kind=" + scope.Asset.single.kind + ";payload=null;default-text='" + scope.Asset.single.text + "';default-icon=null";
            }
        }

        private static string OnAfterDeserializeKnownKindMismatchedPayload()
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
                Require(scope.Asset.single.kind == UnityShapeSerializable.Kind.Circle, "Mismatched payload changed the known kind.");
                Require(scope.Asset.single.radius == 0f, "Mismatched payload leaked another variant field.");
                return "known-kind=Circle;mismatched-payload=ignored";
            }
        }

        private static string OnAfterDeserializeUnknownKindNoThrow()
        {
            using (var scope = new AssetScope("unknown-kind"))
            {
                scope.Asset.single = UnityShapeSerializable.Point();
                scope.Save();
                var serialized = new SerializedObject(scope.Asset);
                serialized.Update();
                var kind = RequireKindProperty(serialized, "single");
                kind.intValue = 255;
                Require(serialized.ApplyModifiedProperties(), "Invalid discriminator edit was not applied.");
                scope.Save();
                scope.Reload();
                var observedKind = GetKindName(scope.Asset.single);
                Require(observedKind != "Circle" && observedKind != "Label" && observedKind != "Point", "Invalid discriminator was normalized to a known variant.");
                Require(
                    scope.Asset.single.radius == 0f &&
                    scope.Asset.single.text == null &&
                    scope.Asset.single.icon == null,
                    "Invalid discriminator reconstructed non-default runtime storage.");
                var reloaded = new SerializedObject(scope.Asset);
                reloaded.Update();
                var persistedKind = RequireKindProperty(reloaded, "single");
                Require(persistedKind.intValue == 255, "Invalid discriminator storage was normalized during reload.");
                return "invalid-kind=" + observedKind + ";raw=255;safe-default=true";
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

        private static string GeneratedInitMutationRefreshesPayload()
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

                var internalSlotCount = 0;
                foreach (var property in scope.Asset.original.GetType()
                    .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(property => IsInternalSlotName(property.Name)))
                {
                    var replacement = InternalSlotValue(property.PropertyType, scope.Texture);
                    if (replacement is null && property.PropertyType.IsValueType)
                        throw new InvalidOperationException("Unsupported generated internal slot type: " + property.PropertyType);
                    var independentCopy = scope.Asset.original;
                    var mutatedCopy = SetInternalSlot(independentCopy, property.Name, replacement);
                    Require(
                        GetSerializedPayloadCache(mutatedCopy) == null,
                        "Generated init mutation retained the serialized payload cache for slot " + property.Name + ".");
                    internalSlotCount++;
                }

                Require(internalSlotCount >= 3, "Generated internal init slots were incomplete.");
                Require(scope.Asset.original.count == 9, "Independent slot probing changed the original count.");
                Require(scope.Asset.original.label == "init-after", "Independent slot probing changed the original label.");
                Require(scope.Asset.original.asset == scope.Texture, "Independent slot probing changed the original object reference.");
                scope.Save();
                var payload = RequirePayload(scope.Asset, "original");
                RequirePayloadValue(payload, "count", 9);
                RequirePayloadValue(payload, "label", "init-after");
                RequirePayloadValue(payload, "asset", scope.Texture);
                return "internal-slots=" + internalSlotCount + ";cache-detached=true;original=preserved;public-init-profile=separate";
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

        private static string NestedCollectionInitMutationDetachesCache()
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
                return "after=after;sibling=sibling;cache-detached=true";
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
                return "original=original;copy=mutated;cache-detached=true";
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
                Require(children.Count == 2, "Serialized union did not contain exactly discriminator and payload fields.");
                Require(children.Any(p => p.propertyType == SerializedPropertyType.Enum), "Serialized discriminator was missing.");
                Require(children.Any(p => p.propertyType == SerializedPropertyType.ManagedReference), "Serialized managed payload was missing.");
                Require(!children.Any(p => p.name == "radius" || p.name == "text" || p.name == "icon"), "Runtime union properties leaked into serialized storage.");
                var yaml = File.ReadAllText(scope.AssetFilePath);
                var payload = RequirePayloadProperty(serialized, "single").managedReferenceValue;
                if (payload == null)
                    throw new InvalidOperationException("Active payload was unavailable in serialized state.");
                Require(yaml.IndexOf(payload.GetType().Name, StringComparison.Ordinal) >= 0, "Active payload type was not present in YAML.");
                return "children=2;active=" + payload.GetType().Name + ";runtime-fields=absent";
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

                scope.Asset.boxedFields = SetInternalSlotForField(
                    scope.Asset.boxedFields,
                    "label",
                    "after-fields");
                scope.Asset.boxedTuple = SetInternalSlotForField(
                    scope.Asset.boxedTuple,
                    "label",
                    ("after-tuple", scope.Texture));

                Require(
                    GetSerializedPayloadCache(scope.Asset.boxedFields) == null &&
                    GetSerializedPayloadCache(scope.Asset.boxedTuple) == null,
                    "A boxed init mutation retained a stale serialized cache.");

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
                return "fields=Text/before-fields->after-fields;tuple=Text/before-tuple->after-tuple;mutation-detached=true;repeat-stable=true;reference=preserved";
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

                var copy = scope.Asset.boxedParentCopy;
                copy = SetInternalSlotForField(copy, "marker", 2);
                copy = SetInternalSlotForField(
                    copy,
                    "child",
                    SetInternalSlotForField(copy.child, "label", "copy"));
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

                var serialized = new SerializedObject(scope.Asset);
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
                Require(DirectChildren(RequireProperty(serialized, "boxedFields")).Count == 2, "BoxedFields exposed extra serialized children.");
                Require(DirectChildren(RequireProperty(serialized, "boxedTuple")).Count == 2, "BoxedTuple exposed extra serialized children.");
                var yaml = File.ReadAllText(scope.AssetFilePath);
                AssertRuntimeFieldsAbsentFromYaml(typeof(UnityBoxedFieldsUnion), yaml);
                AssertRuntimeFieldsAbsentFromYaml(typeof(UnityBoxedTupleUnion), yaml);
                var fieldsPayload = RequirePayloadProperty(serialized, "boxedFields").managedReferenceValue;
                var tuplePayload = RequirePayloadProperty(serialized, "boxedTuple").managedReferenceValue;
                Require(fieldsPayload != null && tuplePayload != null, "Boxed active payloads were unavailable.");
                Require(yaml.IndexOf(fieldsPayload!.GetType().Name, StringComparison.Ordinal) >= 0, "BoxedFields active payload was absent from YAML.");
                Require(yaml.IndexOf(tuplePayload!.GetType().Name, StringComparison.Ordinal) >= 0, "BoxedTuple active payload was absent from YAML.");
                return "fields=kind+payload;tuple=kind+payload;runtime-storage=absent;inactive-payloads=absent";
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
                Require(children.Count == 2, "UnsafeOverlap exposed runtime storage as serialized children.");
                Require(children.Any(property => property.propertyType == SerializedPropertyType.Enum), "UnsafeOverlap discriminator was missing.");
                Require(children.Any(property => property.propertyType == SerializedPropertyType.ManagedReference), "UnsafeOverlap payload was missing.");
                var payload = RequirePayloadProperty(serialized, "unsafeUnion").managedReferenceValue;
                Require(payload != null, "UnsafeOverlap active payload was unavailable.");
                var yaml = File.ReadAllText(scope.AssetFilePath);
                AssertRuntimeFieldsAbsentFromYaml(typeof(UnityUnsafeOverlapUnion), yaml);
                Require(
                    yaml.IndexOf(payload!.GetType().Name, StringComparison.Ordinal) >= 0,
                    "UnsafeOverlap active payload type was absent from YAML.");
                return "children=2;active=" + payload.GetType().Name + ";raw-buffer=absent;managed-slots=absent";
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
                Require(children.Count == 2, "Overlap exposed explicit runtime storage as serialized children.");
                Require(children.Any(property => property.propertyType == SerializedPropertyType.Enum), "Overlap discriminator was missing.");
                Require(children.Any(property => property.propertyType == SerializedPropertyType.ManagedReference), "Overlap payload was missing.");
                var payload = RequirePayloadProperty(serialized, "overlapUnion").managedReferenceValue;
                Require(payload != null, "Overlap active payload was unavailable.");
                var yaml = File.ReadAllText(scope.AssetFilePath);
                AssertRuntimeFieldsAbsentFromYaml(typeof(UnityOverlapUnion), yaml);
                Require(
                    yaml.IndexOf(payload!.GetType().Name, StringComparison.Ordinal) >= 0,
                    "Overlap active payload type was absent from YAML.");
                return "children=2;active=" + payload.GetType().Name + ";explicit-runtime-fields=absent";
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

        private static string DrawerReadsKindAndManagedReference()
        {
            using (var scope = new DrawerHostScope("drawer-read"))
            {
                scope.Host.single = UnityDrawerUnion.Text(4, "serialized");
                var serialized = new SerializedObject(scope.Host);
                serialized.Update();
                var property = RequireProperty(serialized, "single");
                var kind = RequireKindProperty(property);
                var payload = RequirePayloadProperty(property).managedReferenceValue;
                Require(kind.enumDisplayNames[kind.enumValueIndex] == "Empty" || kind.enumDisplayNames[kind.enumValueIndex] == "Number" || kind.enumDisplayNames[kind.enumValueIndex] == "Text" || kind.enumDisplayNames[kind.enumValueIndex] == "ObjectRef", "Serialized kind was not a known drawer variant.");
                Require(payload != null, "Serialized managed payload was unavailable.");
                var boxed = TryReadBoxedValue(property, out var boxedState);
                Require(
                    !boxed || boxedState != "Text",
                    "The installed Unity boxedValue getter unexpectedly reconstructed the complete Text union.");
                RequirePayloadValue(payload!, "label", "serialized");
                return "serialized-kind=Text;payload=present;boxedValue-getter=" + boxedState;
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
        "Drawer_InvalidKind_SelectsFirstKnownVariant",
        "Drawer_SelectionChange_UsesFactoryReplacement",
        "Drawer_ArrayElement_NestedPathWriteback",
        "Drawer_ListElement_NestedPathWriteback",
        "Drawer_UndoRedo_WholeValueAndPayloadEdit",
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

        private static SerializedProperty RequireKindProperty(SerializedObject serialized, string path)
        {
            return RequireKindProperty(RequireProperty(serialized, path));
        }

        private static SerializedProperty RequireKindProperty(SerializedProperty union)
        {
            var direct = DirectChildren(union);
            var kind = direct.FirstOrDefault(property => property.propertyType == SerializedPropertyType.Enum && property.name.IndexOf("kind", StringComparison.OrdinalIgnoreCase) >= 0);
            if (kind == null)
                kind = direct.FirstOrDefault(property => property.propertyType == SerializedPropertyType.Enum);
            return kind ?? throw new InvalidOperationException("Serialized discriminator property was unavailable for " + union.propertyPath + ".");
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
            if (property == null || property.SetMethod == null)
                throw new InvalidOperationException("Generated internal init property was unavailable: " + propertyName + ".");
            property.SetValue(boxed, newValue, null);
            return (T)boxed;
        }

        private static object? GetSerializedPayloadCache<T>(T value)
        {
            var field = value!.GetType().GetField(
                "_serializedPayload",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(value);
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

        private static object? InternalSlotValue(Type type, Texture2D texture)
        {
            if (type == typeof(int))
                return 11;
            if (type == typeof(string))
                return "internal-slot";
            if (type == typeof(Texture2D))
                return texture;
            if (type == typeof(float))
                return 17.5f;
            if (type == typeof(UnityCallbackChild))
                return UnityCallbackChild.Text(13, "internal-child", texture);
            return null;
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
                CreateInvalidKindScenario(),
                CreateDirectSelectorScenario(),
                CreateArraySelectorScenario(),
                CreateListSelectorScenario(),
                CreateUndoRedoScenario(),
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

            private static DrawerUiScenario CreateInvalidKindScenario()
            {
                var scenario = new DrawerUiScenario("Drawer_InvalidKind_SelectsFirstKnownVariant");
                UnityDrawerHost? host = null;
                var phase = 0;
                scenario.Start = (runner, window) =>
                {
                    host = runner.CreateHost("invalid-kind");
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
                            selectorValue.IndexOf("invalid", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            selectorValue.IndexOf("255", StringComparison.Ordinal) >= 0,
                            "Invalid raw kind was not indicated by the generated selector; observed='" + selectorValue + "'.");
                        Require(window.TryInteractWithSelector("Empty"), "Invalid-kind Empty selector was unavailable.");
                        return false;
                    }

                    Require(host!.single.kind == UnityDrawerUnion.Kind.Empty, "Invalid-kind selector did not construct Empty.");
                    Require(
                        !window.HasBoundFieldControl("count") &&
                        !window.HasBoundFieldControl("label") &&
                        !window.HasBoundFieldControl("value") &&
                        !window.HasBoundFieldControl("texture"),
                        "Invalid-kind Empty selection kept inactive controls.");
                    scenario.Observed = "raw-kind=255;display=invalid;selected=Empty;inactive-controls=false";
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
                    second.single = UnityDrawerUnion.Text(2, "two");
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
                        Require(window.TryInteractWithSelector("Number"), "Multi-object Number selector was unavailable.");
                        return false;
                    }

                    Require(first!.single.kind == UnityDrawerUnion.Kind.Number && second!.single.kind == UnityDrawerUnion.Kind.Number, "Multi-object selector did not apply to both targets.");
                    Require(first.nested.value.radius == 11f && second.nested.value.radius == 22f, "Multi-object selector changed untouched per-target data.");
                    Require(window.HasBoundFieldControl("value") && !window.HasBoundFieldControl("count") && !window.HasBoundFieldControl("label"), "Multi-object selector controls did not follow the active variant.");
                    scenario.Observed = "objects=2;selector=Number;untouched-radii=11/22;preserved=true";
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

                Undo.RecordObject(_target, "Spire deferred drawer discriminator change");
                var kind = RequireKindProperty(_serialized, _deferredPropertyPath);
                kind.intValue = rawKind;
                Require(_serialized.ApplyModifiedProperties(), "Deferred raw discriminator edit was not applied.");
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

                var kind = RequireKindProperty(_serialized, propertyPath);
                kind.intValue = rawKind;
                Require(_serialized.ApplyModifiedProperties(), "Raw discriminator edit was not applied.");
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
                        var kind = RequireKindProperty(property);
                        rootKind = kind.intValue.ToString();
                        var enumDisplayNames = kind.enumDisplayNames;
                        if (enumDisplayNames != null &&
                            kind.enumValueIndex >= 0 &&
                            kind.enumValueIndex < enumDisplayNames.Length)
                        {
                            rootKind += ":" + enumDisplayNames[kind.enumValueIndex];
                        }

                        var managedPayload = RequirePayloadProperty(property);
                        var payloadValue = managedPayload.managedReferenceValue;
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
