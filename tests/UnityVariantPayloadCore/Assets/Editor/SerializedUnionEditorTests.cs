#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Houtamelo.Spire;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace Spire.UnityVariantPayloadCore
{

    public static class SerializedUnionEditorTests
    {
        private const string ResourceFolder = "Assets/Resources/SerializedUnionPayloadCore";
        private const string FixturePath = ResourceFolder + "/SerializedUnionFixture.asset";
        private const string TexturePath = ResourceFolder + "/SerializedUnionTexture.asset";
        private const string PlayerAliasPath = ResourceFolder + "/SerializedUnionPlayerAlias.asset";
        private const string PlayerNullPath = ResourceFolder + "/SerializedUnionPlayerNull.asset";
        private const string PlayerMissingPath = ResourceFolder + "/SerializedUnionPlayerMissing.asset";
        private const string PlayerUnknownPath = ResourceFolder + "/SerializedUnionPlayerUnknown.asset";
        private const string PlayerStaleKindPath = ResourceFolder + "/SerializedUnionPlayerStaleKind.asset";
        private const string PlayerInvalidKindPath = ResourceFolder + "/SerializedUnionPlayerInvalidKind.asset";
        private const string EditorAliasPath = ResourceFolder + "/SerializedUnionEditorAlias.asset";
        private const string EditorNullPath = ResourceFolder + "/SerializedUnionEditorNull.asset";
        private const string EditorMissingPath = ResourceFolder + "/SerializedUnionEditorMissing.asset";
        private const string EditorUnknownPath = ResourceFolder + "/SerializedUnionEditorUnknown.asset";
        private const string EditorStaleKindPath = ResourceFolder + "/SerializedUnionEditorStaleKind.asset";
        private const string EditorInvalidKindPath = ResourceFolder + "/SerializedUnionEditorInvalidKind.asset";
        private const string BaselinePath = "unity-union-serialization-baseline.yaml";
        private const string WriteReportPath = "unity-union-serialization-write-results.json";
        private const string ReadReportPath = "unity-union-serialization-read-results.json";
        private const string RecoveryReportPath = "unity-union-serialization-recovery-results.json";
        private const string AccessorAssertionsReportPath = "unity-union-accessor-assertions-results.json";
        private const string PreparationReportPath = "unity-union-serialization-player-preparation-results.json";
        private const string InvalidPayloadWarning =
            "invalid union serialization data, defaulting to Move";
        // Unity's managed-reference YAML/type-name representation uses '/'
        // between nested types. Reflection checks below intentionally retain '+'.
        private const string SerializedShapeMovePayloadType =
            "SerializedShapeTypes/SerializedPayloads/MoveSerializedPayload";
        private const string SerializedAliasCurrentPayloadType =
            "SerializedAliasUnionTypes/SerializedPayloads/CurrentSerializedPayload";
        private const string SerializedAliasFormerPayloadType =
            "SerializedAliasUnionTypes/SerializedPayloads/FormerSerializedPayload";

        private static int _checks;
        private static readonly List<string> Failures = new List<string>();

        public static void Write()
            => RunPhase("write", WriteReportPath, () => WritePhase());

        public static void Read()
            => RunPhase("read", ReadReportPath, ReadPhase);

        public static void Recovery()
            => RunPhase("recovery", RecoveryReportPath, RecoveryPhase);

        public static void AccessorAssertions()
            => RunPhase("accessor-assertions", AccessorAssertionsReportPath, AccessorAssertionsPhase);

        public static void PreparePlayerAssets()
        {
            _checks = 0;
            Failures.Clear();
            Exception? failure = null;

            try
            {
                WritePhase(includeUnknownRecoveryAssets: false, includeMissingRecoveryAssets: false);
                RemoveEditorOnlyRecoveryAssetsForPlayerBuild();
            }
            catch (Exception exception)
            {
                failure = exception;
                Failures.Add(exception.ToString());
            }

            var report = new PhaseReport
            {
                phase = "prepare-player-assets",
                unityVersion = Application.unityVersion,
                status = Failures.Count == 0 ? "pass" : "fail",
                checks = _checks,
                failures = new List<string>(Failures),
            };
            ApplyNativeCapabilityReport(report);

            try
            {
                File.WriteAllText(ProjectPath(PreparationReportPath), JsonUtility.ToJson(report, true));
            }
            catch (Exception exception)
            {
                failure = failure ?? exception;
                report.status = "fail";
                report.failures.Add("Could not write report: " + exception);
            }

            if (failure != null)
                throw failure;
            if (report.status != "pass")
                throw new InvalidOperationException(string.Join(Environment.NewLine, report.failures));
        }

        private static void RemoveEditorOnlyRecoveryAssetsForPlayerBuild()
        {
            foreach (var path in new[]
            {
                PlayerUnknownPath,
                PlayerMissingPath,
                EditorAliasPath,
                EditorNullPath,
                EditorMissingPath,
                EditorUnknownPath,
                EditorStaleKindPath,
                EditorInvalidKindPath,
            })
            {
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.Refresh();
            Debug.Log("SerializedUnionEditorTests: removed Editor-only/unknown/missing malformed assets before player build; direct callback fallback remains covered in-player.");
        }

        private static void RunPhase(string phase, string reportName, Action action)
        {
            _checks = 0;
            Failures.Clear();

            try
            {
                action();
            }
            catch (Exception exception)
            {
                Failures.Add(exception.ToString());
            }

            var report = new PhaseReport
            {
                phase = phase,
                unityVersion = Application.unityVersion,
                status = Failures.Count == 0 ? "pass" : "fail",
                checks = _checks,
                failures = new List<string>(Failures),
            };
            ApplyNativeCapabilityReport(report);

            try
            {
                File.WriteAllText(ProjectPath(reportName), JsonUtility.ToJson(report, true));
            }
            catch (Exception exception)
            {
                report.status = "fail";
                report.failures.Add("Could not write report: " + exception);
            }

            EditorApplication.Exit(report.status == "pass" ? 0 : 1);
        }

        private static void WritePhase(
            bool includeUnknownRecoveryAssets = true,
            bool includeMissingRecoveryAssets = true)
        {
            EnsureResourceFolder();
            DeleteGeneratedAssets();

            var texture = CreateTexture();
            AssetDatabase.CreateAsset(texture, TexturePath);

            var asset = BuildFixture(texture);
            AssetDatabase.CreateAsset(asset, FixturePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Check(asset != null, "Fixture asset was not created.");
            CheckFactoryDoesNotAllocateSnapshot();
            AssertEditorHydrationState();
            AssertSerializedSurface(asset!);
            AssertNativeFixtureValues(asset!, texture);
            AssertGroupedPayloadTypes();
            AssertSerializedPayloadStateSwitch();
            AssertDirectPrepareDoesNotInvokeUserCallbacks(asset!);

            var baseline = ReadAsset(FixturePath);
            File.WriteAllText(ProjectPath(BaselinePath), baseline);
            AssertUnchangedSave("SaveAssets", () => AssetDatabase.SaveAssets(), baseline);

            EditorUtility.SetDirty(asset);
            AssertUnchangedSave(
                "SetDirty + SaveAssetIfDirty",
                () => AssetDatabase.SaveAssetIfDirty(asset),
                baseline);

            AssertUnchangedSave(
                "ForceReserializeAssets",
                () => AssetDatabase.ForceReserializeAssets(new[] { FixturePath }),
                baseline);

            CreateRecoveryAssets(includeUnknownRecoveryAssets, includeMissingRecoveryAssets);
        }

        private static void ReadPhase()
        {
            var baselinePath = ProjectPath(BaselinePath);
            Check(File.Exists(baselinePath), "The write phase did not persist a YAML baseline.");

            var asset = AssetDatabase.LoadAssetAtPath<SerializedUnionAsset>(FixturePath);
            Check(asset != null, "Fresh-process read could not load the fixture asset.");
            if (asset == null)
                return;

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            Check(texture != null, "Fresh-process read could not load the referenced texture asset.");
            if (texture == null)
                return;

            AssertNativeFixtureValues(asset, texture);
            AssertEditorHydrationState();
            AssertSerializedSurface(asset);
            AssertCopiedValuesAreIndependent();
            AssertGroupedPayloadTypes();
            AssertSerializedPayloadStateSwitch();
            AssertDirectPrepareDoesNotInvokeUserCallbacks(asset);

            var baseline = File.ReadAllText(baselinePath);
            Check(
                string.Equals(ReadAsset(FixturePath), baseline, StringComparison.Ordinal),
                "A fresh-process read changed the canonical YAML before any edit.");
            AssertUnchangedSave("Fresh-process SaveAssets", () => AssetDatabase.SaveAssets(), baseline);

            EditorUtility.SetDirty(asset);
            AssertUnchangedSave(
                "Fresh-process SetDirty + SaveAssetIfDirty",
                () => AssetDatabase.SaveAssetIfDirty(asset),
                baseline);
        }

        private static void RecoveryPhase()
        {
            ResetRecoveryAssetsFromCanonicalBaseline();
            var warningMessages = new List<string>();
            void OnLog(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Warning && string.Equals(condition, InvalidPayloadWarning, StringComparison.Ordinal))
                {
                    lock (warningMessages)
                        warningMessages.Add(condition);
                }
            }

            Application.logMessageReceived += OnLog;
            Application.logMessageReceivedThreaded += OnLog;
            try
            {
                AssertFallbackAsset(EditorNullPath, warningMessages, "null payload");
                AssertFallbackAsset(EditorMissingPath, warningMessages, "missing payload");
                AssertFallbackAsset(EditorUnknownPath, warningMessages, "unknown payload");
                AssertAliasAsset(warningMessages);
                AssertKindAsset(EditorStaleKindPath, warningMessages, "stale kind");
                AssertKindAsset(EditorInvalidKindPath, warningMessages, "invalid kind");
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
                Application.logMessageReceivedThreaded -= OnLog;
            }
        }

        private static void AccessorAssertionsPhase()
        {
            var asset = ScriptableObject.CreateInstance<SerializedUnionAsset>();
            var assertions = new List<string>();
            void OnLog(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Assert)
                    assertions.Add(condition);
            }

            Application.logMessageReceived += OnLog;
            try
            {
                asset.shape = SerializedShape.Move(new Vector2(2f, 3f), 7.5f);
                asset.generic = SerializedGeneric<int>.Value(42, "generic");
                asset.command = SerializedNestedOwner.Command.Run(3.5f);

                var shapeMove = asset.shape.move;
                Check(
                    shapeMove.destination == new Vector2(2f, 3f) && shapeMove.speed == 7.5f,
                    "The active Move accessor did not preserve its payload values.");
                Check(
                    assertions.Count == 0,
                    "Reading an active Move accessor emitted an assertion.");

                _ = asset.shape.empty;
                Check(
                    HasAccessorAssertion(assertions, "Empty", "Empty", "Move"),
                    "Reading the inactive Empty accessor did not emit its kind assertion.");

                var assertionCount = assertions.Count;
                var genericValue = asset.generic.value;
                Check(
                    genericValue.value == 42 && genericValue.label == "generic",
                    "The active generic Value accessor did not preserve its payload values.");
                Check(
                    assertions.Count == assertionCount,
                    "Reading an active generic Value accessor emitted an assertion.");

                _ = asset.generic.empty;
                Check(
                    HasAccessorAssertion(assertions, "Empty", "Empty", "Value"),
                    "Reading the inactive generic Empty accessor did not emit its kind assertion.");

                var nestedAssertionCount = assertions.Count;
                var commandRun = asset.command.run;
                Check(
                    commandRun.speed == 3.5f,
                    "The active nested Run accessor did not preserve its payload value.");
                Check(
                    assertions.Count == nestedAssertionCount,
                    "Reading an active nested Run accessor emitted an assertion.");

                _ = asset.command.stop;
                Check(
                    HasAccessorAssertion(assertions, "Stop", "Stop", "Run"),
                    "Reading the inactive nested Stop accessor did not emit its kind assertion.");
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        private static bool HasAccessorAssertion(
            List<string> assertions,
            string variant,
            string expectedKind,
            string actualKind)
        {
            var expected = "Accessing the `" + variant + "` variant's fields is only allowed when `kind == Kind." +
                expectedKind + "`. Actual kind: " + actualKind;
            return assertions.Any(message => message.IndexOf(expected, StringComparison.Ordinal) >= 0);
        }

        private static void AssertFallbackAsset(
            string path,
            List<string> warningMessages,
            string description)
        {
            var warningCount = WarningCount(warningMessages);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var asset = AssetDatabase.LoadAssetAtPath<SerializedUnionAsset>(path);
            Check(asset != null, "Could not load " + description + " recovery asset.");
            if (asset == null)
                return;

            if (asset.shape.kind != SerializedShape.Kind.Move)
                throw new InvalidOperationException(description + " recovery did not select the first variant.");
            Check(
                asset.shape.move.destination == Vector2.zero && asset.shape.move.speed == 0f,
                description + " recovery did not use default first-variant fields.");
            Check(
                WarningCount(warningMessages) > warningCount,
                description + " recovery did not emit an invalid-data warning during native deserialization.");

            var nativeWarningCount = WarningCount(warningMessages);

            using (var serialized = new SerializedObject(asset))
            {
                serialized.Update();
                serialized.Update();
                Check(
                    serialized.FindProperty("shape._serializedPayload") != null,
                    description + " recovery lost the serialized payload property.");
            }

            Check(
                WarningCount(warningMessages) == nativeWarningCount,
                description + " recovery warned again during repaint/update.");

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            var canonical = ReadAsset(path);
            Check(
                canonical.IndexOf(SerializedShapeMovePayloadType, StringComparison.Ordinal) >= 0,
                description + " recovery did not canonicalize the next save.");
            Check(
                canonical.IndexOf(
                    "SerializedShapeTypes/SerializedPayloads/NotKnownSerializedPayload",
                    StringComparison.Ordinal) < 0,
                description + " recovery retained an invalid payload typename.");
        }

        private static void AssertAliasAsset(List<string> warningMessages)
        {
            var warningCount = WarningCount(warningMessages);
            AssetDatabase.ImportAsset(EditorAliasPath, ImportAssetOptions.ForceUpdate);
            var asset = AssetDatabase.LoadAssetAtPath<SerializedUnionAsset>(EditorAliasPath);
            Check(asset != null, "Could not load former-name alias recovery asset.");
            if (asset == null)
                return;

            if (asset.alias.kind != SerializedAliasUnion.Kind.Current)
                throw new InvalidOperationException("Former-name payload did not derive the canonical alias variant.");
            Check(
                asset.alias.current.amount == 99 && asset.alias.current.label == "alias",
                "Former-name payload fields did not restore.");
            Check(
                WarningCount(warningMessages) == warningCount,
                "Former-name alias unexpectedly entered invalid-payload recovery.");

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            var canonical = ReadAsset(EditorAliasPath);
            Check(
                canonical.IndexOf(SerializedAliasCurrentPayloadType, StringComparison.Ordinal) >= 0,
                "Former-name alias did not serialize with the canonical payload typename.");
            Check(
                canonical.IndexOf(SerializedAliasFormerPayloadType, StringComparison.Ordinal) < 0,
                "Former-name alias typename remained after canonical save.");
        }

        private static void AssertKindAsset(
            string path,
            List<string> warningMessages,
            string description)
        {
            var warningCount = WarningCount(warningMessages);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var asset = AssetDatabase.LoadAssetAtPath<SerializedUnionAsset>(path);
            Check(asset != null, "Could not load " + description + " recovery asset.");
            if (asset == null)
                return;

            if (asset.shape.kind != SerializedShape.Kind.Move)
                throw new InvalidOperationException(
                    "A recognized payload did not remain authoritative over the " + description + ".");
            Check(
                asset.shape.move.destination == new Vector2(2f, 3f) && asset.shape.move.speed == 7.5f,
                description + " recovery discarded recognized payload fields.");
            Check(
                WarningCount(warningMessages) == warningCount,
                description + " incorrectly entered invalid-payload recovery.");

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            var block = ShapeBlock(ReadAsset(path));
            Check(
                block.IndexOf("    _kind: 0", StringComparison.Ordinal) >= 0,
                description + " recovery did not canonicalize the serialized kind.");
        }

        private static void AssertCopiedValuesAreIndependent()
        {
            var probe = ScriptableObject.CreateInstance<SerializedUnionAsset>();
            try
            {
                probe.copiedLeft = SerializedShape.Move(new Vector2(1f, 1f), 7f);
                probe.copiedRight = probe.copiedLeft;

                using (var serialized = new SerializedObject(probe))
                {
                    serialized.Update();
                    var speed = serialized.FindProperty("copiedLeft._serializedPayload.value._speed");
                    Check(speed != null, "Copied union payload speed property was not exposed.");
                    if (speed != null)
                    {
                        speed.floatValue = 13f;
                        Check(
                            serialized.ApplyModifiedPropertiesWithoutUndo(),
                            "Copied union native edit was not applied.");
                    }
                }

                if (probe.copiedLeft.kind != SerializedShape.Kind.Move)
                    throw new InvalidOperationException("Copied union edit changed the edited variant.");
                if (probe.copiedRight.kind != SerializedShape.Kind.Move)
                    throw new InvalidOperationException("Editing one copied union changed the other variant.");
                Check(probe.copiedLeft.move.speed == 13f,
                    "Copied union edit did not update the edited value.");
                Check(probe.copiedRight.move.speed == 7f,
                    "Editing one copied union changed the other copied value.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        private static void AssertDirectPrepareDoesNotInvokeUserCallbacks(SerializedUnionAsset asset)
        {
            var shape = asset.nested;
            if (shape.kind != SerializedShape.Kind.Rich)
                throw new InvalidOperationException("Callback probe payload did not restore the Rich variant.");

            var rich = shape.rich;
            var callback = rich.helper.callback;
            Check(callback != null, "Callback-bearing helper was not present in the Rich payload.");
            if (callback == null)
                return;

            var beforeSerialize = callback.beforeSerializeCount;
            var afterDeserialize = callback.afterDeserializeCount;
            var prepared = InvokePrepare(shape);
            var restored = SerializedShape.__SpireFromSerialized(GetSerializedPayload(prepared));
            if (restored.kind != SerializedShape.Kind.Rich)
                throw new InvalidOperationException("Direct generated restore changed the callback probe variant.");
            var restoredCallback = restored.rich.helper.callback;
            Check(restoredCallback != null, "Direct generated restore lost the callback-bearing helper.");

            Check(
                callback.beforeSerializeCount == beforeSerialize &&
                callback.afterDeserializeCount == afterDeserialize,
                "Direct generated prepare/restore invoked a user callback outside Unity's native lifecycle.");
        }

        private static void AssertSerializedSurface(SerializedUnionAsset asset)
        {
            using (var serialized = new SerializedObject(asset))
            {
                serialized.Update();
                var shape = serialized.FindProperty("shape");
                var kind = serialized.FindProperty("shape._kind");
                var payload = serialized.FindProperty("shape._serializedPayload");
                var value = serialized.FindProperty("shape._serializedPayload.value");
                var emptyValue = serialized.FindProperty("empty._serializedPayload.value");
                Check(shape != null, "The union field was not exposed to SerializedObject.");
                Check(kind != null && kind.propertyType == SerializedPropertyType.Enum,
                    "The union kind was not serialized as a native enum.");
                Check(payload != null && payload.propertyType == SerializedPropertyType.ManagedReference,
                    "The active payload was not serialized as one managed reference.");
                Check(value != null && value.propertyType == SerializedPropertyType.Generic,
                    "The managed-reference value was not exposed as inline public payload data.");
                Check(
                    payload != null && payload.managedReferenceId != 0,
                    "The active payload did not receive a native managed-reference ID.");
                Check(
                    payload != null &&
                    payload.managedReferenceFullTypename.IndexOf(SerializedShapeMovePayloadType, StringComparison.Ordinal) >= 0,
                    "The active payload typename did not identify the public Move payload.");
                Check(
                    value != null && value.FindPropertyRelative("_destination") != null &&
                    value.FindPropertyRelative("_speed") != null,
                    "Serialized payload backing fields did not retain their declaration names.");
                var richValue = serialized.FindProperty("nested._serializedPayload.value");
                var graph = richValue?.FindPropertyRelative("_graph");
                Check(
                    graph != null && graph.propertyType == SerializedPropertyType.ManagedReference &&
                    graph.managedReferenceId != 0,
                    "The private SerializeReference-only payload field did not remain a native managed reference.");
                Check(emptyValue != null && !emptyValue.hasChildren,
                    "The fieldless payload gained synthetic serialized fields.");
                Check(
                    serialized.FindProperty("shape._serializedPayload.value._kind") == null,
                    "The public payload unexpectedly contains a nested union discriminator.");

                var generic = serialized.FindProperty("generic");
                Check(
                    (generic != null) == SerializedUnionNativeCapabilities.GenericUnionSerializationSupported,
                    SerializedUnionNativeCapabilities.GenericUnionSerializationSupported
                        ? "The generic union field was not exposed to SerializedObject on a supported Unity version."
                        : "The generic union field was unexpectedly exposed to SerializedObject on Unity 2022.");
                if (SerializedUnionNativeCapabilities.GenericUnionSerializationSupported)
                {
                    Check(
                        generic != null && generic.propertyType == SerializedPropertyType.Generic &&
                        serialized.FindProperty("generic._serializedPayload") != null,
                        "The generic union field did not expose its serialized payload on a supported Unity version.");
                }
            }
        }

        private static void AssertGroupedPayloadTypes()
        {
            var genericTypes = typeof(SerializedGenericTypes1);
            var variants = genericTypes.GetNestedType("Variants", BindingFlags.Public);
            Check(variants != null, "The grouped generic union did not expose its public Variants container.");
            if (variants == null)
                return;

            var valueDefinition = variants.GetNestedType("Value`1", BindingFlags.Public);
            Check(valueDefinition != null, "The grouped generic payload type was not loadable by reflection.");
            if (valueDefinition == null)
                return;

            var valueType = valueDefinition.MakeGenericType(typeof(int));
            Check(
                valueType.FullName != null &&
                valueType.FullName.IndexOf("SerializedGenericTypes1+Variants+Value", StringComparison.Ordinal) >= 0,
                "The grouped generic payload type did not retain its arity-suffixed container name.");
            var value = Activator.CreateInstance(valueType, new object[] { 42, "generic" });
            Check(value != null, "The grouped generic payload type could not be constructed.");
            Check(
                value?.GetType().GetProperty("value", BindingFlags.Instance | BindingFlags.Public)?.GetValue(value) is 42,
                "The grouped generic payload did not preserve its value through reflection.");

            var nestedTypes = typeof(SerializedNestedOwner).GetNestedType(
                "CommandTypes",
                BindingFlags.Public | BindingFlags.NonPublic);
            Check(nestedTypes != null, "The nested union did not expose its grouped type container.");
        }

        private static void AssertSerializedPayloadStateSwitch()
        {
            var prepared = InvokePrepare(SerializedShape.Move(new Vector2(2f, 3f), 7.5f));
            var foreignPayload = GetSerializedPayload(
                InvokePrepare(SerializedGenericState<int>.Value(11)));
            Check(foreignPayload != null, "The foreign generic payload was not prepared.");

            var mismatched = WithSerializedPayload(prepared, foreignPayload);
            Check(
                !mismatched.__SpireSerializedStateCurrent(foreignPayload),
                "A payload with a mismatched union type was accepted as current serialized state.");
            InvokeAfterDeserialize(ref mismatched);
            Check(
                mismatched.kind == SerializedShape.Kind.Move && mismatched.move.speed == 0f,
                "A mismatched serialized payload did not recover to the first variant defaults.");

            var nullPayload = WithSerializedPayload(prepared, null);
            Check(
                !nullPayload.__SpireSerializedStateCurrent(null),
                "A null serialized payload was accepted as current serialized state.");
            InvokeAfterDeserialize(ref nullPayload);
            Check(
                nullPayload.kind == SerializedShape.Kind.Move && nullPayload.move.speed == 0f,
                "A null serialized payload did not recover to the first variant defaults.");
        }

        private static void AssertNativeFixtureValues(SerializedUnionAsset asset, Texture2D texture)
        {
            if (asset.shape.kind != SerializedShape.Kind.Move)
                throw new InvalidOperationException("Fielded payload kind did not restore.");
            Check(
                asset.shape.move.destination == new Vector2(2f, 3f) && asset.shape.move.speed == 7.5f,
                "Fielded payload values did not restore.");
            var (destination, speed) = asset.shape.move;
            Check(destination == new Vector2(2f, 3f) && speed == 7.5f,
                "Fielded payload Deconstruct did not preserve values.");

            Check(asset.empty.kind == SerializedShape.Kind.Empty, "Fieldless payload kind did not restore.");
            if (asset.nested.kind != SerializedShape.Kind.Rich)
                throw new InvalidOperationException("Nested payload kind did not restore.");

            var rich = asset.nested.rich;
            if (rich.icon is null || asset.sharedTexture is null)
                throw new InvalidOperationException("UnityEngine.Object references were lost.");
            Check(ReferenceEquals(rich.icon, texture) && ReferenceEquals(asset.sharedTexture, texture) &&
                rich.icon.GetEntityId() == texture.GetEntityId() &&
                asset.sharedTexture.GetEntityId() == texture.GetEntityId(),
                "UnityEngine.Object identity did not survive serialization.");
            Check(rich.helper.label == "nested-helper", "Mutable inline helper label did not restore.");
            Check(rich.helper.callback != null, "Callback-bearing helper did not restore.");
            if (rich.helper.callback != null)
            {
                Check(rich.helper.callback.marker == 17,
                    "Callback-bearing helper fields did not restore.");
                if (rich.helper.callback.nested.kind != SerializedShape.Kind.Move)
                    throw new InvalidOperationException("Callback-bearing helper nested union did not restore.");
                Check(rich.helper.callback.nested.move.speed == 2.25f,
                    "Callback-bearing helper nested union fields did not restore.");
            }
            if (rich.helper.command.kind != SerializedNestedOwner.Command.Kind.Run)
                throw new InvalidOperationException("Nested owner command kind did not restore.");
            Check(rich.helper.command.run.speed == 3.5f,
                "Nested owner union did not restore through an inline helper.");
            if (rich.helper.nested.kind != SerializedShape.Kind.Move)
                throw new InvalidOperationException("Nested helper payload kind did not restore.");
            Check(rich.helper.nested.move.speed == 1.25f,
                "Nested union inside the mutable helper did not restore.");
            if (rich.child.kind != SerializedChildUnion.Kind.Leaf)
                throw new InvalidOperationException("Nested child payload kind did not restore.");
            Check(rich.child.leaf.marker == 11 && rich.child.leaf.label == "child",
                "Nested child union payload did not restore.");
            Check(rich.children.Length == 2 &&
                rich.children[0].kind == SerializedChildUnion.Kind.Leaf &&
                rich.children[1].kind == SerializedChildUnion.Kind.Command,
                "Union array order or variants did not restore.");
            Check(rich.childList.Count == 2 &&
                rich.childList[0].kind == SerializedChildUnion.Kind.Empty &&
                rich.childList[1].kind == SerializedChildUnion.Kind.Leaf,
                "Union list order or variants did not restore.");

            Check(asset.shapeArray.Length == 3 &&
                asset.shapeArray[0].kind == SerializedShape.Kind.Move &&
                asset.shapeArray[1].kind == SerializedShape.Kind.Empty &&
                asset.shapeArray[2].kind == SerializedShape.Kind.Move,
                "Union array variants did not restore in declaration order.");
            Check(asset.shapeList.Count == 3 &&
                asset.shapeList[0].kind == SerializedShape.Kind.Empty &&
                asset.shapeList[1].kind == SerializedShape.Kind.Move &&
                asset.shapeList[2].kind == SerializedShape.Kind.Rich,
                "Union list variants did not restore in declaration order.");

            if (SerializedUnionNativeCapabilities.GenericUnionSerializationSupported)
            {
                if (asset.generic.kind != SerializedGeneric<int>.Kind.Value)
                    throw new InvalidOperationException("Generic union kind did not restore.");
                var generic = asset.generic.value;
                Check(generic.value == 42 && generic.label == "generic",
                    "Generic union fields did not restore.");
                var (genericValue, genericLabel) = generic;
                Check(genericValue == 42 && genericLabel == "generic",
                    "Generic payload Deconstruct did not preserve values.");
            }

            if (asset.alias.kind != SerializedAliasUnion.Kind.Current)
                throw new InvalidOperationException("Canonical alias union kind did not restore.");
            Check(asset.alias.current.amount == 99 && asset.alias.current.label == "alias",
                "Canonical alias union did not restore.");
            if (asset.command.kind != SerializedNestedOwner.Command.Kind.Run)
                throw new InvalidOperationException("Nested owner command kind did not restore.");
            Check(asset.command.run.speed == 3.5f,
                "Nested owner command did not restore.");
            if (asset.inline.nested.kind != SerializedShape.Kind.Move)
                throw new InvalidOperationException("Top-level inline helper payload kind did not restore.");
            Check(asset.inline.nested.move.speed == 1.25f,
                "Top-level inline helper union did not restore.");

            Check(asset.graphRoot != null && rich.graph != null,
                "Managed-reference graph roots were lost.");
            if (asset.graphRoot != null && rich.graph != null)
            {
                Check(ReferenceEquals(asset.graphRoot, rich.graph),
                    "Intentional managed-reference sharing was not preserved.");
                Check(asset.graphRoot.next != null && asset.graphRoot.next.next != null,
                    "Managed-reference cycle was not restored.");
                if (asset.graphRoot.next != null && asset.graphRoot.next.next != null)
                    Check(ReferenceEquals(asset.graphRoot, asset.graphRoot.next.next),
                        "Managed-reference cycle identity was not preserved.");
            }

            if (asset.copiedLeft.kind != SerializedShape.Kind.Move)
                throw new InvalidOperationException("Copied-left union variant did not restore.");
            if (asset.copiedRight.kind != SerializedShape.Kind.Move)
                throw new InvalidOperationException("Copied-right union variant did not restore.");
            Check(asset.copiedLeft.move.speed == asset.copiedRight.move.speed,
                "Copied union values did not restore.");
        }

        private static void CheckFactoryDoesNotAllocateSnapshot()
        {
            var fresh = SerializedShape.Move(new Vector2(2f, 3f), 7.5f);
            Check(GetSerializedPayload(fresh) == null,
                "A factory-created union eagerly allocated its serialization snapshot.");

            var prepared = InvokePrepare(fresh);
            var previousPayload = GetSerializedPayload(prepared);
            Check(previousPayload is UnitySerializationPayloadBase,
                "Serialization preparation did not create the shared payload wrapper.");

            var equivalent = InvokePrepare(prepared);
            Check(ReferenceEquals(previousPayload, GetSerializedPayload(equivalent)),
                "Equivalent serialization preparation did not reuse the wrapper.");

            var changed = SerializedShape.Move(new Vector2(2f, 3f), 8.5f);
            changed = WithSerializedPayload(changed, previousPayload);
            var replacement = InvokePrepare(changed);
            Check(!ReferenceEquals(previousPayload, GetSerializedPayload(replacement)),
                "Changed serialization preparation reused the old wrapper.");
            Check(
                GetPayloadField<float>(previousPayload!, "speed") == 7.5f,
                "Replacing a changed snapshot mutated the previous wrapper.");

            var copied = prepared;
            var changedCopy = SerializedShape.Move(new Vector2(2f, 3f), 12.5f);
            changedCopy = WithSerializedPayload(changedCopy, previousPayload);
            _ = InvokePrepare(changedCopy);
            if (copied.kind != SerializedShape.Kind.Move)
                throw new InvalidOperationException("Equivalent copied preparation changed the variant.");
            Check(copied.move.speed == 7.5f,
                "Changing one copied union mutated the other copied value.");
        }

        private static void AssertEditorHydrationState()
        {
            AssertOnlySerializedPayloadExtra<PlainSerializationState, SerializedSerializationState>();
            AssertOnlySerializedPayloadExtra<PlainReferenceState, SerializedReferenceState>();
            AssertOnlySerializedPayloadExtra<PlainGenericState<int>, SerializedGenericState<int>>();

            var prepared = InvokePrepare(SerializedShape.Move(new Vector2(2f, 3f), 7.5f));
            var realPayload = GetSerializedPayload(prepared);
            Check(realPayload is UnitySerializationPayloadBase,
                "Editor preparation did not create a real serialized payload wrapper.");

            var deserialized = prepared;
            InvokeAfterDeserialize(ref deserialized);
            Check(ReferenceEquals(realPayload, GetSerializedPayload(deserialized)),
                "Editor OnAfterDeserialize replaced the real payload cache.");

            var hydrated = deserialized.__SpireHydrateSerialized();
            var hydratedAgain = hydrated.__SpireHydrateSerialized();
            Check(hydrated.kind == SerializedShape.Kind.Move &&
                hydratedAgain.kind == SerializedShape.Kind.Move &&
                ReferenceEquals(realPayload, GetSerializedPayload(hydrated)) &&
                ReferenceEquals(realPayload, GetSerializedPayload(hydratedAgain)),
                "Editor hydration did not retain the real payload cache across repeated calls.");

            var preparedAgain = InvokePrepare(hydratedAgain);
            Check(ReferenceEquals(realPayload, GetSerializedPayload(preparedAgain)),
                "Equivalent Editor preparation did not retain the real payload cache.");
        }

        private static void AssertOnlySerializedPayloadExtra<TPlain, TSerialized>()
        {
            var plainFields = InstanceFields(typeof(TPlain));
            var serializedFields = InstanceFields(typeof(TSerialized));
            var extras = serializedFields
                .Where(serialized => !plainFields.Any(plain => plain.Name == serialized.Name))
                .ToArray();
            Check(extras.Length == 1 && extras[0].Name == "_serializedPayload",
                typeof(TSerialized).Name + " added fields other than _serializedPayload: " +
                string.Join(", ", extras.Select(field => field.Name)));
            Check(!serializedFields.Any(field => field.Name == "__SpireSerializationHydrated"),
                typeof(TSerialized).Name + " still contains the per-instance hydration flag.");
        }

        private static void InvokeAfterDeserialize<T>(ref T value)
            where T : ISerializationCallbackReceiver
            => value.OnAfterDeserialize();

        private static FieldInfo[] InstanceFields(Type type)
            => type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(field => !field.IsStatic)
                .ToArray();

        private static void AssertUnchangedSave(string name, Action save, string baseline)
        {
            save();
            var current = ReadAsset(FixturePath);
            Check(
                string.Equals(current, baseline, StringComparison.Ordinal),
                name + " rewrote unchanged serialized YAML or managed-reference IDs.");
            Check(
                ManagedReferenceIds(current).Count == ManagedReferenceIds(baseline).Count,
                name + " changed the number of serialized managed-reference IDs.");
            Check(
                SameManagedReferenceIds(current, baseline),
                name + " changed the serialized managed-reference ID sequence.");
        }

        private static void CreateRecoveryAssets(
            bool includeUnknownRecoveryAssets = true,
            bool includeMissingRecoveryAssets = true)
        {
            CreateCopyAndTransform(PlayerAliasPath, text =>
                text.Replace(SerializedAliasCurrentPayloadType, SerializedAliasFormerPayloadType, StringComparison.Ordinal));
            CreateCopyAndTransform(EditorAliasPath, text =>
                text.Replace(SerializedAliasCurrentPayloadType, SerializedAliasFormerPayloadType, StringComparison.Ordinal));

            CreateCopyAndTransform(PlayerNullPath, text => EnsureNullReferenceSentinel(
                ReplaceShapePayload(text, null, removePayload: false)));
            CreateCopyAndTransform(EditorNullPath, text => EnsureNullReferenceSentinel(
                ReplaceShapePayload(text, null, removePayload: false)));

            if (includeMissingRecoveryAssets)
            {
                CreateCopyAndTransform(PlayerMissingPath, RemoveShapePayloadRecord);
                CreateCopyAndTransform(EditorMissingPath, RemoveShapePayloadRecord);
            }

            if (includeUnknownRecoveryAssets)
            {
                CreateCopyAndTransform(PlayerUnknownPath, text =>
                    ReplaceShapePayload(
                        text,
                        "SerializedShapeTypes/SerializedPayloads/NotKnownSerializedPayload",
                        removePayload: false));
                CreateCopyAndTransform(EditorUnknownPath, text =>
                    ReplaceShapePayload(
                        text,
                        "SerializedShapeTypes/SerializedPayloads/NotKnownSerializedPayload",
                        removePayload: false));
            }

            CreateCopyAndTransform(PlayerStaleKindPath, text => ReplaceShapeKind(text, 1));
            CreateCopyAndTransform(EditorStaleKindPath, text => ReplaceShapeKind(text, 1));
            CreateCopyAndTransform(PlayerInvalidKindPath, text => ReplaceShapeKind(text, 99));
            CreateCopyAndTransform(EditorInvalidKindPath, text => ReplaceShapeKind(text, 99));
        }

        private static void ResetRecoveryAssetsFromCanonicalBaseline()
        {
            var baseline = File.ReadAllText(ProjectPath(BaselinePath));
            Check(
                string.Equals(ReadAsset(FixturePath), baseline, StringComparison.Ordinal),
                "The canonical fixture changed before recovery reset.");

            foreach (var path in RecoveryAssetPaths())
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.Refresh();
            CreateRecoveryAssets();
        }

        private static IEnumerable<string> RecoveryAssetPaths()
        {
            yield return PlayerAliasPath;
            yield return PlayerNullPath;
            yield return PlayerMissingPath;
            yield return PlayerUnknownPath;
            yield return PlayerStaleKindPath;
            yield return PlayerInvalidKindPath;
            yield return EditorAliasPath;
            yield return EditorNullPath;
            yield return EditorMissingPath;
            yield return EditorUnknownPath;
            yield return EditorStaleKindPath;
            yield return EditorInvalidKindPath;
        }

        private static void CreateCopyAndTransform(string targetPath, Func<string, string> transform)
        {
            Check(AssetDatabase.CopyAsset(FixturePath, targetPath),
                "Could not create recovery asset " + targetPath + ".");
            var absolutePath = ProjectPath(targetPath);
            var original = File.ReadAllText(absolutePath);
            var transformed = transform(original);
            Check(!string.Equals(original, transformed, StringComparison.Ordinal),
                "Recovery transform did not change " + targetPath + ".");
            File.WriteAllText(absolutePath, transformed);
            AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceUpdate);
        }

        private static string ReplaceShapePayload(string text, string? replacementType, bool removePayload)
        {
            var block = ShapeBlock(text);
            var payloadMarker = "    _serializedPayload:\n";
            var payloadIndex = block.IndexOf(payloadMarker, StringComparison.Ordinal);
            if (payloadIndex < 0)
                return text;

            var lineEnd = block.IndexOf('\n', payloadIndex + payloadMarker.Length);
            if (lineEnd < 0)
                lineEnd = block.Length;
            else
                lineEnd++;

            if (removePayload)
            {
                block = block.Remove(payloadIndex, lineEnd - payloadIndex);
            }
            else if (replacementType == null)
            {
                var ridIndex = block.IndexOf("      rid: ", payloadIndex, StringComparison.Ordinal);
                if (ridIndex < 0)
                    return text;
                var ridEnd = block.IndexOf('\n', ridIndex);
                if (ridEnd < 0)
                    ridEnd = block.Length;
                block = block.Remove(ridIndex, ridEnd - ridIndex)
                    .Insert(ridIndex, "      rid: " + ManagedReferenceUtility.RefIdNull);
            }
            else
            {
                var ridMatch = Regex.Match(block, @"(?m)^\s*rid: (\d+)\s*$");
                if (!ridMatch.Success)
                    return text;
                var recordMarker = "    - rid: " + ridMatch.Groups[1].Value;
                var recordStart = text.IndexOf(recordMarker, StringComparison.Ordinal);
                if (recordStart < 0)
                    return text;
                var recordEnd = text.IndexOf("\n    - rid:", recordStart + recordMarker.Length,
                    StringComparison.Ordinal);
                if (recordEnd < 0)
                    recordEnd = text.Length;
                var record = text.Substring(recordStart, recordEnd - recordStart)
                    .Replace(SerializedShapeMovePayloadType, replacementType, StringComparison.Ordinal);
                return text.Substring(0, recordStart) + record + text.Substring(recordEnd);
            }

            return ReplaceShapeBlock(text, block);
        }

        private static string RemoveShapePayloadRecord(string text)
        {
            var block = ShapeBlock(text);
            var ridMatch = Regex.Match(block, @"(?m)^\s*rid: (\d+)\s*$");
            if (!ridMatch.Success)
                return text;

            var recordMarker = "    - rid: " + ridMatch.Groups[1].Value;
            var recordStart = text.IndexOf(recordMarker, StringComparison.Ordinal);
            if (recordStart < 0)
                return text;
            var recordEnd = text.IndexOf("\n    - rid:", recordStart + recordMarker.Length,
                StringComparison.Ordinal);
            if (recordEnd < 0)
                recordEnd = text.Length;
            return text.Remove(recordStart, recordEnd - recordStart);
        }

        private static string EnsureNullReferenceSentinel(string text)
        {
            const string referencesMarker = "    RefIds:\n";
            const string sentinel = "    - rid: -2\n      type: {class: , ns: , asm: }\n";
            if (text.IndexOf("    - rid: -2\n", StringComparison.Ordinal) >= 0)
                return text;
            var markerIndex = text.IndexOf(referencesMarker, StringComparison.Ordinal);
            if (markerIndex < 0)
                return text;
            return text.Insert(markerIndex + referencesMarker.Length, sentinel);
        }

        private static string ReplaceShapeKind(string text, int kind)
        {
            var block = ShapeBlock(text);
            var kindMarker = "    _kind: ";
            var kindIndex = block.IndexOf(kindMarker, StringComparison.Ordinal);
            if (kindIndex < 0)
                return text;
            var lineEnd = block.IndexOf('\n', kindIndex);
            if (lineEnd < 0)
                lineEnd = block.Length;
            var replacement = kindMarker + kind;
            block = block.Remove(kindIndex, lineEnd - kindIndex).Insert(kindIndex, replacement);
            return ReplaceShapeBlock(text, block);
        }

        private static string ShapeBlock(string text)
        {
            var start = text.IndexOf("  shape:\n", StringComparison.Ordinal);
            if (start < 0)
                return string.Empty;
            var end = text.IndexOf("\n  empty:", start + 1, StringComparison.Ordinal);
            return end < 0 ? text.Substring(start) : text.Substring(start, end - start);
        }

        private static string ReplaceShapeBlock(string text, string block)
        {
            var start = text.IndexOf("  shape:\n", StringComparison.Ordinal);
            if (start < 0)
                return text;
            var end = text.IndexOf("\n  empty:", start + 1, StringComparison.Ordinal);
            if (end < 0)
                return text;
            return text.Substring(0, start) + block + text.Substring(end);
        }

        private static string ReadAsset(string relativePath)
            => File.ReadAllText(ProjectPath(relativePath));

        private static List<long> ManagedReferenceIds(string yaml)
        {
            var result = new List<long>();
            foreach (Match match in Regex.Matches(yaml, @"(?m)^\s*-?\s*rid: (-?\d+)\s*$"))
            {
                if (long.TryParse(match.Groups[1].Value, out var value))
                    result.Add(value);
            }

            return result;
        }

        private static bool SameManagedReferenceIds(string left, string right)
        {
            var leftIds = ManagedReferenceIds(left);
            var rightIds = ManagedReferenceIds(right);
            if (leftIds.Count != rightIds.Count)
                return false;

            for (var index = 0; index < leftIds.Count; index++)
            {
                if (leftIds[index] != rightIds[index])
                    return false;
            }

            return true;
        }

        private static Texture2D CreateTexture()
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = "SerializedUnionTexture",
            };
            texture.SetPixel(0, 0, Color.magenta);
            texture.SetPixel(1, 0, Color.cyan);
            texture.SetPixel(0, 1, Color.yellow);
            texture.SetPixel(1, 1, Color.black);
            texture.Apply();
            return texture;
        }

        private static SerializedUnionAsset BuildFixture(Texture2D texture)
        {
            var root = new SerializedGraphNode
            {
                name = "root",
                value = 1,
            };
            var child = new SerializedGraphNode
            {
                name = "child",
                value = 2,
            };
            root.next = child;
            child.next = root;

            var helper = new SerializedInlineHelper
            {
                label = "nested-helper",
                command = SerializedNestedOwner.Command.Run(3.5f),
                nested = SerializedShape.Move(new Vector2(4f, 5f), 1.25f),
                callback = new SerializedCallbackHelper
                {
                    marker = 17,
                    nested = SerializedShape.Move(new Vector2(8f, 9f), 2.25f),
                },
            };
            var childUnion = SerializedChildUnion.Leaf(11, "child");
            var rich = SerializedShape.Rich(
                root,
                texture,
                helper,
                childUnion,
                new[]
                {
                    childUnion,
                    SerializedChildUnion.Command(SerializedNestedOwner.Command.Stop()),
                },
                new List<SerializedChildUnion>
                {
                    SerializedChildUnion.Empty(),
                    childUnion,
                });

            var asset = ScriptableObject.CreateInstance<SerializedUnionAsset>();
            asset.shape = SerializedShape.Move(new Vector2(2f, 3f), 7.5f);
            asset.empty = SerializedShape.Empty();
            asset.nested = rich;
            asset.shapeArray = new[]
            {
                SerializedShape.Move(new Vector2(6f, 7f), 8f),
                SerializedShape.Empty(),
                SerializedShape.Move(new Vector2(9f, 10f), 11f),
            };
            asset.shapeList = new List<SerializedShape>
            {
                SerializedShape.Empty(),
                SerializedShape.Move(new Vector2(12f, 13f), 14f),
                rich,
            };
            asset.generic = SerializedGeneric<int>.Value(42, "generic");
            asset.alias = SerializedAliasUnion.Current(99, "alias");
            asset.command = SerializedNestedOwner.Command.Run(3.5f);
            asset.copiedLeft = SerializedShape.Move(new Vector2(21f, 22f), 17f);
            asset.copiedRight = asset.copiedLeft;
            asset.inline = helper;
            asset.sharedTexture = texture;
            asset.graphRoot = root;
            return asset;
        }

        private static void EnsureResourceFolder()
            => Directory.CreateDirectory(ProjectPath(ResourceFolder));

        private static void DeleteGeneratedAssets()
        {
            foreach (var path in new[]
            {
                FixturePath,
                TexturePath,
                PlayerAliasPath,
                PlayerNullPath,
                PlayerMissingPath,
                PlayerUnknownPath,
                PlayerStaleKindPath,
                PlayerInvalidKindPath,
                EditorAliasPath,
                EditorNullPath,
                EditorMissingPath,
                EditorUnknownPath,
                EditorStaleKindPath,
                EditorInvalidKindPath,
            })
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        private static object? GetSerializedPayload<T>(T value)
        {
            var field = typeof(T).GetField(
                "_serializedPayload",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Check(field != null, typeof(T).Name + " has no serialized payload field.");
            return field == null ? null : field.GetValue(value);
        }

        private static T InvokePrepare<T>(T value)
        {
            var method = typeof(T).GetMethod(
                "__SpirePrepareForSerialization",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Check(method != null, typeof(T).Name + " has no preparation bridge.");
            return method == null ? value : (T)method.Invoke(value, null)!;
        }

        private static T WithSerializedPayload<T>(T value, object? payload)
        {
            var field = typeof(T).GetField(
                "_serializedPayload",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Check(field != null, typeof(T).Name + " has no serialized payload field.");
            if (field == null)
                return value;

            object boxed = value!;
            field.SetValue(boxed, payload);
            return (T)boxed;
        }

        private static TValue GetPayloadField<TValue>(object payload, string name)
        {
            var valueField = payload.GetType().GetField("value", BindingFlags.Instance | BindingFlags.Public);
            Check(valueField != null, "The generic payload wrapper has no public value field.");
            if (valueField == null)
                return default!;
            var value = valueField.GetValue(payload);
            Check(value != null, "The generic payload wrapper value was null.");
            if (value == null)
                return default!;
            var property = value.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            Check(property != null, "The public payload has no getter named " + name + ".");
            return property == null ? default! : (TValue)property.GetValue(value)!;
        }

        private static string ProjectPath(string relativePath)
            => Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, relativePath));

        private static void Check(bool condition, string message)
        {
            _checks++;
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void ApplyNativeCapabilityReport(PhaseReport report)
        {
            report.genericUnionSerializationSupported =
                SerializedUnionNativeCapabilities.GenericUnionSerializationSupported;
            report.genericUnionSerializationApplicability =
                SerializedUnionNativeCapabilities.GenericUnionSerializationApplicability;
        }

        private static int WarningCount(List<string> warningMessages)
        {
            lock (warningMessages)
                return warningMessages.Count;
        }

        [Serializable]
        private sealed class PhaseReport
        {
            public string phase = string.Empty;
            public string unityVersion = string.Empty;
            public string status = string.Empty;
            public int checks;
            public List<string> failures = new List<string>();
            public bool genericUnionSerializationSupported;
            public string genericUnionSerializationApplicability = string.Empty;
        }
    }

}
