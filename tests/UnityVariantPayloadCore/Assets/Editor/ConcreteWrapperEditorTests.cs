#nullable enable
#pragma warning disable CS8602, CS8604

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace Spire.UnityVariantPayloadCore
{

    public static class ConcreteWrapperEditorTests
    {
        private const string ResourceFolder = "Assets/Resources/ConcreteWrapperPayloadCore";
        private const string FixturePath = ResourceFolder + "/ConcreteWrapperFixture.asset";
        private const string AliasFixturePath = ResourceFolder + "/ConcreteWrapperAlias.asset";
        private const string LegacyFixturePath = ResourceFolder + "/ConcreteWrapperLegacy.asset";
        private const string TexturePath = ResourceFolder + "/ConcreteWrapperTexture.asset";
        private const string BaselinePath = "unity-concrete-wrapper-baseline.yaml";
        private const string AliasBaselinePath = "unity-concrete-wrapper-alias-baseline.yaml";
        private const string LegacyBaselinePath = "unity-concrete-wrapper-legacy-baseline.yaml";
        private const string UpgradedBaselinePath = "unity-concrete-wrapper-upgraded-baseline.yaml";
        private const string WriteReportPath = "unity-concrete-wrapper-write-results.json";
        private const string ReadReportPath = "unity-concrete-wrapper-read-results.json";
        private const string LegacyWriteReportPath = "unity-concrete-wrapper-legacy-write-results.json";
        private const string UpgradeReportPath = "unity-concrete-wrapper-upgrade-results.json";
        private const string VerifyReportPath = "unity-concrete-wrapper-verify-results.json";

        private const string ShapeMovePayloadType =
            "ConcreteWrapperShapeTypes/SerializedPayloads/MoveSerializedPayload";
        private const string ShapeSpeakPayloadType =
            "ConcreteWrapperShapeTypes/SerializedPayloads/SpeakSerializedPayload";
        private const string ShapeEmptyPayloadType =
            "ConcreteWrapperShapeTypes/SerializedPayloads/EmptySerializedPayload";
        private const string FieldMatrixRichPayloadType =
            "ConcreteWrapperFieldMatrixTypes/SerializedPayloads/RichSerializedPayload";
        private const string AliasCurrentPayloadType =
            "ConcreteWrapperAliasTypes/SerializedPayloads/CurrentSerializedPayload";
        private const string AliasFormerPayloadType =
            "ConcreteWrapperAliasTypes/SerializedPayloads/OldSerializedPayload";
        private const string NestedRunPayloadType =
            "ConcreteWrapperNestedOwner/CommandTypes/SerializedPayloads/RunSerializedPayload";
        private const string LegacyGenericTypeMarker = "SpireSerializedPayload`1";

        private static int _checks;
        private static readonly List<string> Failures = new List<string>();

        public static void Write()
            => RunPhase("write", WriteReportPath, WritePhase);

        public static void Read()
            => RunPhase("read", ReadReportPath, ReadPhase);

        public static void WriteLegacy()
            => RunPhase("legacy-write", LegacyWriteReportPath, WriteLegacyPhase);

        public static void UpgradeLegacy()
            => RunPhase("upgrade", UpgradeReportPath, UpgradeLegacyPhase);

        public static void VerifyUpgraded()
            => RunPhase("verify-upgraded", VerifyReportPath, VerifyUpgradedPhase);

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

        private static void WritePhase()
        {
            EnsureResourceFolder();
            DeleteCanonicalAssets();

            var texture = CreateTexture();
            AssetDatabase.CreateAsset(texture, TexturePath);

            var asset = BuildFixture(texture);
            AssetDatabase.CreateAsset(asset, FixturePath);

            var aliasAsset = ScriptableObject.CreateInstance<ConcreteWrapperAliasAsset>();
            aliasAsset.alias = ConcreteWrapperAlias.Current(77, "alias");
            AssetDatabase.CreateAsset(aliasAsset, AliasFixturePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            asset = AssetDatabase.LoadAssetAtPath<ConcreteWrapperAsset>(FixturePath);
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            Check(asset != null && texture != null, "The canonical fixture assets were not created.");
            if (asset == null || texture == null)
                return;

            AssertSharedPayloadBase();
            AssertConcreteWrapperDefinitions();
            AssertNativeFixtureValues(asset, texture);
            AssertSerializedSurface(asset);
            AssertDrawerPayloadTypeFallback(
                asset,
                typeof(ConcreteWrapperShape),
                "shape",
                GetSerializedPayload(asset.shape),
                ShapeMovePayloadType,
                expectedVariantIndex: 0);
            AssertDrawerSharedManagedReferenceTraversal(asset);
            AssertAliasCanonicalization();

            var baseline = ReadAsset(FixturePath);
            File.WriteAllText(ProjectPath(BaselinePath), baseline);
            AssertUnchangedSave(
                FixturePath,
                "Canonical SaveAssets",
                () => AssetDatabase.SaveAssets(),
                baseline);

            EditorUtility.SetDirty(asset);
            AssertUnchangedSave(
                FixturePath,
                "Canonical SetDirty + SaveAssetIfDirty",
                () => AssetDatabase.SaveAssetIfDirty(asset),
                baseline);
        }

        private static void ReadPhase()
        {
            Check(File.Exists(ProjectPath(BaselinePath)), "The canonical write baseline is missing.");
            Check(File.Exists(ProjectPath(AliasBaselinePath)), "The alias write baseline is missing.");

            var asset = AssetDatabase.LoadAssetAtPath<ConcreteWrapperAsset>(FixturePath);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            var aliasAsset = AssetDatabase.LoadAssetAtPath<ConcreteWrapperAliasAsset>(AliasFixturePath);
            Check(asset != null && texture != null && aliasAsset != null,
                "Fresh-process read could not load the canonical fixture assets.");
            if (asset == null || texture == null || aliasAsset == null)
                return;

            AssertNativeFixtureValues(asset, texture);
            AssertSerializedSurface(asset);
            AssertDrawerPayloadTypeFallback(
                asset,
                typeof(ConcreteWrapperShape),
                "shape",
                GetSerializedPayload(asset.shape),
                ShapeMovePayloadType,
                expectedVariantIndex: 0);
            AssertAliasFixture(aliasAsset);

            var baseline = File.ReadAllText(ProjectPath(BaselinePath));
            Check(
                string.Equals(ReadAsset(FixturePath), baseline, StringComparison.Ordinal),
                "Fresh-process read changed canonical YAML before an edit.");
            AssertUnchangedSave(
                FixturePath,
                "Fresh-process canonical SaveAssets",
                () => AssetDatabase.SaveAssets(),
                baseline);

            var aliasBaseline = File.ReadAllText(ProjectPath(AliasBaselinePath));
            Check(
                string.Equals(ReadAsset(AliasFixturePath), aliasBaseline, StringComparison.Ordinal),
                "Fresh-process read changed alias YAML before an edit.");
            AssertUnchangedSave(
                AliasFixturePath,
                "Fresh-process alias SaveAssets",
                () => AssetDatabase.SaveAssets(),
                aliasBaseline);
        }

        private static void WriteLegacyPhase()
        {
            EnsureResourceFolder();
            DeleteLegacyAsset();

            var asset = ScriptableObject.CreateInstance<LegacyGenericWrapperAsset>();
            asset.shape = ConcreteWrapperShape.Move(new Vector2(6f, 7f), 8.5f);
            AssetDatabase.CreateAsset(asset, LegacyFixturePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            asset = AssetDatabase.LoadAssetAtPath<LegacyGenericWrapperAsset>(LegacyFixturePath);
            Check(asset != null, "The legacy fixture asset was not created.");
            if (asset == null)
                return;

            AssertLegacyFixtureValues(asset);
            var payload = GetSerializedPayload(asset.shape);
            Check(IsLegacyGenericPayload(payload),
                "The frozen package did not write the expected generic payload wrapper.");

            var yaml = ReadAsset(LegacyFixturePath);
            Check(yaml.IndexOf(LegacyGenericTypeMarker, StringComparison.Ordinal) >= 0,
                "The legacy YAML did not contain SpireSerializedPayload<T>.");
            Check(yaml.IndexOf(ShapeMovePayloadType, StringComparison.Ordinal) < 0,
                "LegacyWrite unexpectedly depended on the new concrete wrapper typename.");
            File.WriteAllText(ProjectPath(LegacyBaselinePath), yaml);
        }

        private static void UpgradeLegacyPhase()
        {
            Check(File.Exists(ProjectPath(LegacyBaselinePath)), "The legacy write baseline is missing.");

            var asset = AssetDatabase.LoadAssetAtPath<LegacyGenericWrapperAsset>(LegacyFixturePath);
            Check(asset != null, "The legacy fixture could not be loaded by the new package.");
            if (asset == null)
                return;

            AssertLegacyFixtureValues(asset);
            var legacyPayload = GetSerializedPayload(asset.shape);
            Check(IsLegacyGenericPayload(legacyPayload),
                "Loading the old asset did not retain its generic payload for migration.");

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);

            var upgradedYaml = ReadAsset(LegacyFixturePath);
            Check(upgradedYaml.IndexOf(ShapeMovePayloadType, StringComparison.Ordinal) >= 0,
                "A dirty save did not replace the legacy typename with the concrete wrapper.");
            Check(upgradedYaml.IndexOf(LegacyGenericTypeMarker, StringComparison.Ordinal) < 0,
                "A dirty save retained the legacy generic managed-reference typename.");
            Check(ManagedReferenceIds(upgradedYaml).Count > 0,
                "The upgraded asset lost its managed-reference ID.");

            AssetDatabase.ImportAsset(LegacyFixturePath, ImportAssetOptions.ForceUpdate);
            asset = AssetDatabase.LoadAssetAtPath<LegacyGenericWrapperAsset>(LegacyFixturePath);
            Check(asset != null, "The upgraded asset could not be reloaded.");
            if (asset == null)
                return;

            AssertLegacyFixtureValues(asset);
            Check(IsCanonicalMovePayload(GetSerializedPayload(asset.shape)),
                "The upgraded runtime cache was not the concrete Move wrapper.");
            File.WriteAllText(ProjectPath(UpgradedBaselinePath), ReadAsset(LegacyFixturePath));
        }

        private static void VerifyUpgradedPhase()
        {
            Check(File.Exists(ProjectPath(UpgradedBaselinePath)),
                "The upgraded asset baseline is missing.");

            var asset = AssetDatabase.LoadAssetAtPath<LegacyGenericWrapperAsset>(LegacyFixturePath);
            Check(asset != null, "Fresh-process verification could not load the upgraded asset.");
            if (asset == null)
                return;

            AssertLegacyFixtureValues(asset);
            Check(IsCanonicalMovePayload(GetSerializedPayload(asset.shape)),
                "Fresh-process verification did not retain the concrete Move wrapper.");

            var baseline = File.ReadAllText(ProjectPath(UpgradedBaselinePath));
            Check(
                string.Equals(ReadAsset(LegacyFixturePath), baseline, StringComparison.Ordinal),
                "Fresh-process verification changed upgraded YAML before an edit.");
            AssertUnchangedSave(
                LegacyFixturePath,
                "Fresh-process upgraded SaveAssets",
                () => AssetDatabase.SaveAssets(),
                baseline);

            EditorUtility.SetDirty(asset);
            AssertUnchangedSave(
                LegacyFixturePath,
                "Fresh-process upgraded SetDirty + SaveAssetIfDirty",
                () => AssetDatabase.SaveAssetIfDirty(asset),
                baseline);
        }

        private static void AssertSharedPayloadBase()
        {
            var assembly = typeof(ConcreteWrapperShape).Assembly;
            var generic = assembly.GetType(
                "Houtamelo.Spire.Generated.SpireSerializedPayload`1",
                throwOnError: false);
            Check(generic != null, "The shared generic payload base is missing.");
            Check(generic != null && !generic.IsSealed,
                "The shared generic payload base is still sealed.");
            Check(
                generic != null && generic.GetField("value", BindingFlags.Instance | BindingFlags.Public) != null,
                "The shared generic payload base lost its public value field.");
        }

        private static void AssertConcreteWrapperDefinitions()
        {
            var shapeWrappers = new[]
            {
                FindGeneratedWrapper(typeof(ConcreteWrapperShape), "ConcreteWrapperShapeTypes", "Move"),
                FindGeneratedWrapper(typeof(ConcreteWrapperShape), "ConcreteWrapperShapeTypes", "Speak"),
                FindGeneratedWrapper(typeof(ConcreteWrapperShape), "ConcreteWrapperShapeTypes", "Empty"),
            };
            Check(shapeWrappers.All(wrapper => wrapper != null),
                "Canonical concrete wrappers were not emitted for every Shape variant.");
            Check(shapeWrappers.All(IsConcreteWrapper),
                "A canonical Shape wrapper lost its sealed, serializable, preserved, inherited-value shape.");

            var fieldMatrixWrappers = new[]
            {
                FindGeneratedWrapper(
                    typeof(ConcreteWrapperFieldMatrix),
                    "ConcreteWrapperFieldMatrixTypes",
                    "Rich"),
                FindGeneratedWrapper(
                    typeof(ConcreteWrapperFieldMatrix),
                    "ConcreteWrapperFieldMatrixTypes",
                    "Empty"),
            };
            Check(fieldMatrixWrappers.All(wrapper => wrapper != null),
                "FieldMatrix concrete wrappers were not emitted for both variants.");
            Check(fieldMatrixWrappers.All(IsConcreteWrapper),
                "A FieldMatrix wrapper lost the concrete-wrapper contract.");

            var aliasWrappers = new[]
            {
                FindGeneratedWrapper(typeof(ConcreteWrapperAlias), "ConcreteWrapperAliasTypes", "Current"),
                FindGeneratedWrapper(typeof(ConcreteWrapperAlias), "ConcreteWrapperAliasTypes", "Old"),
                FindGeneratedWrapper(typeof(ConcreteWrapperAlias), "ConcreteWrapperAliasTypes", "Empty"),
                FindGeneratedWrapper(typeof(ConcreteWrapperAlias), "ConcreteWrapperAliasTypes", "PriorEmpty"),
                FindGeneratedWrapper(typeof(ConcreteWrapperAlias), "ConcreteWrapperAliasTypes", "Other"),
            };
            Check(aliasWrappers.All(wrapper => wrapper != null),
                "Canonical/former alias concrete wrappers were not emitted for every alias identity.");
            Check(aliasWrappers.All(IsConcreteWrapper),
                "A canonical/former alias wrapper lost the concrete-wrapper contract.");

            var nestedWrapper = FindGeneratedWrapper(
                typeof(ConcreteWrapperNestedOwner),
                "CommandTypes",
                "Run");
            Check(nestedWrapper != null && IsConcreteWrapper(nestedWrapper),
                "The non-generic nested union did not emit a concrete Run wrapper.");
        }

        private static bool IsConcreteWrapper(Type? wrapper)
        {
            if (wrapper == null)
                return false;

            var constructor = wrapper.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);
            var baseType = wrapper.BaseType;
            var genericBase = baseType?.IsGenericType == true
                ? baseType.GetGenericTypeDefinition()
                : null;
            var inheritedValue = baseType?.GetField("value", BindingFlags.Instance | BindingFlags.Public);
            var declaredValue = wrapper.GetField(
                "value",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

            return wrapper.IsClass && wrapper.IsSealed && !wrapper.IsPublic &&
                wrapper.IsDefined(typeof(SerializableAttribute), inherit: false) &&
                wrapper.IsDefined(typeof(UnityEngine.Scripting.PreserveAttribute), inherit: false) &&
                constructor != null && constructor.IsPublic &&
                genericBase != null &&
                genericBase.FullName == "Houtamelo.Spire.Generated.SpireSerializedPayload`1" &&
                inheritedValue != null && declaredValue == null;
        }

        private static Type? FindGeneratedWrapper(Type owner, string typeContainerName, string variantName)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic;
            var typeContainer = owner.Assembly.GetType(
                (owner.Namespace ?? string.Empty) + "." + typeContainerName,
                throwOnError: false);
            typeContainer = typeContainer ?? owner.GetNestedType(typeContainerName, flags);
            var serializedPayloads = typeContainer?.GetNestedType("SerializedPayloads", flags);
            return serializedPayloads?.GetNestedType(variantName + "SerializedPayload", flags);
        }

        private static void AssertNativeFixtureValues(
            ConcreteWrapperAsset asset,
            Texture2D texture)
        {
            Check(
                asset.shape.kind == ConcreteWrapperShape.Kind.Move &&
                asset.shape.move.destination == new Vector2(2f, 3f) &&
                asset.shape.move.speed == 7.5f,
                "Fielded Move values did not restore.");
            Check(
                asset.speech.kind == ConcreteWrapperShape.Kind.Speak &&
                asset.speech.speak.speech == "hello",
                "String payload values did not restore.");
            Check(asset.empty.kind == ConcreteWrapperShape.Kind.Empty,
                "Fieldless payload kind did not restore.");

            var rich = asset.rich.rich;
            Check(
                asset.rich.kind == ConcreteWrapperFieldMatrix.Kind.Rich &&
                rich.count == 17 && rich.label == "rich" &&
                rich.values.Count == 3 && rich.values[0] == 3 && rich.values[2] == 9,
                "Collection and scalar payload values did not restore.");
            Check(
                rich.asset != null && asset.sharedTexture != null,
                "UnityEngine.Object references were null after serialization (rich.asset=" +
                (rich.asset == null ? "null" : "non-null") + ", sharedTexture=" +
                (asset.sharedTexture == null ? "null" : "non-null") + ", texture=" +
                (texture == null ? "null" : "non-null") + ").");
            if (rich.asset != null && asset.sharedTexture != null)
            {
                Check(
                    rich.asset == texture,
                    "The Rich payload UnityEngine.Object did not resolve to the expected texture asset (expected=" +
                    AssetDatabase.GetAssetPath(texture) + ", actual=" +
                    AssetDatabase.GetAssetPath(rich.asset) + ").");
                Check(
                    asset.sharedTexture == texture,
                    "The sharedTexture UnityEngine.Object did not resolve to the expected texture asset (expected=" +
                    AssetDatabase.GetAssetPath(texture) + ", actual=" +
                    AssetDatabase.GetAssetPath(asset.sharedTexture) + ").");

                var expectedTexturePath = AssetDatabase.GetAssetPath(texture);
                var expectedTextureGuid = AssetDatabase.AssetPathToGUID(expectedTexturePath);
                var richTexturePath = AssetDatabase.GetAssetPath(rich.asset);
                var sharedTexturePath = AssetDatabase.GetAssetPath(asset.sharedTexture);
                var richTextureGuid = AssetDatabase.AssetPathToGUID(richTexturePath);
                var sharedTextureGuid = AssetDatabase.AssetPathToGUID(sharedTexturePath);
                Check(
                    string.Equals(expectedTexturePath, TexturePath, StringComparison.Ordinal) &&
                    string.Equals(richTexturePath, expectedTexturePath, StringComparison.Ordinal) &&
                    string.Equals(sharedTexturePath, expectedTexturePath, StringComparison.Ordinal) &&
                    !string.IsNullOrEmpty(expectedTextureGuid) &&
                    string.Equals(richTextureGuid, expectedTextureGuid, StringComparison.Ordinal) &&
                    string.Equals(sharedTextureGuid, expectedTextureGuid, StringComparison.Ordinal),
                    "UnityEngine.Object path/GUID identity did not survive serialization (expected path=" +
                    expectedTexturePath + ", rich path=" + richTexturePath + ", shared path=" +
                    sharedTexturePath + ", expected GUID=" + expectedTextureGuid + ", rich GUID=" +
                    richTextureGuid + ", shared GUID=" + sharedTextureGuid + ").");
            }

            Check(
                asset.graphRoot != null && rich.graph != null &&
                ReferenceEquals(asset.graphRoot, rich.graph),
                "Shared SerializeReference graph identity did not survive serialization.");
            if (asset.graphRoot != null)
            {
                Check(
                    asset.graphRoot.next != null && asset.graphRoot.next.next != null &&
                    ReferenceEquals(asset.graphRoot, asset.graphRoot.next.next),
                    "Cyclic SerializeReference graph identity did not survive serialization.");
            }

            Check(
                asset.alias.kind == ConcreteWrapperAlias.Kind.Current &&
                asset.alias.current.amount == 99 && asset.alias.current.label == "canonical",
                "Canonical alias payload did not restore.");
            Check(
                asset.command.kind == ConcreteWrapperNestedOwner.Command.Kind.Run &&
                asset.command.run.speed == 4.5f,
                "Nested non-generic union payload did not restore.");
            Check(
                asset.shapeArray.Length == 3 &&
                asset.shapeArray[0].kind == ConcreteWrapperShape.Kind.Move &&
                asset.shapeArray[1].kind == ConcreteWrapperShape.Kind.Empty &&
                asset.shapeArray[2].kind == ConcreteWrapperShape.Kind.Move &&
                asset.shapeArray[2].move.speed == 11f,
                "Union array order or values did not restore.");
            Check(
                asset.shapeList.Count == 3 &&
                asset.shapeList[0].kind == ConcreteWrapperShape.Kind.Empty &&
                asset.shapeList[1].kind == ConcreteWrapperShape.Kind.Speak &&
                asset.shapeList[2].kind == ConcreteWrapperShape.Kind.Move &&
                asset.shapeList[2].move.speed == 14f,
                "Union list order or values did not restore.");
        }

        private static void AssertSerializedSurface(ConcreteWrapperAsset asset)
        {
            using (var serialized = new SerializedObject(asset))
            {
                serialized.Update();

                var shapePayload = serialized.FindProperty("shape._serializedPayload");
                var shapeValue = serialized.FindProperty("shape._serializedPayload.value");
                Check(
                    HasManagedReferenceType(shapePayload, ShapeMovePayloadType),
                    "Move did not use its concrete managed-reference typename or ID.");
                Check(
                    shapeValue != null &&
                    shapeValue.FindPropertyRelative("_destination") != null &&
                    shapeValue.FindPropertyRelative("_speed") != null,
                    "Move inherited payload fields were not exposed at their private serialized paths.");

                var speechPayload = serialized.FindProperty("speech._serializedPayload");
                Check(
                    HasManagedReferenceType(speechPayload, ShapeSpeakPayloadType) &&
                    serialized.FindProperty("speech._serializedPayload.value._speech") != null,
                    "Speak did not expose its concrete typename and private serialized field.");

                var emptyPayload = serialized.FindProperty("empty._serializedPayload");
                var emptyValue = serialized.FindProperty("empty._serializedPayload.value");
                Check(
                    HasManagedReferenceType(emptyPayload, ShapeEmptyPayloadType) &&
                    emptyValue != null && !emptyValue.hasChildren,
                    "Empty did not use a concrete fieldless wrapper without synthetic fields.");

                var richPayload = serialized.FindProperty("rich._serializedPayload");
                var richValue = serialized.FindProperty("rich._serializedPayload.value");
                Check(
                    HasManagedReferenceType(richPayload, FieldMatrixRichPayloadType) &&
                    richValue != null &&
                    richValue.FindPropertyRelative("_asset") != null &&
                    richValue.FindPropertyRelative("_values") != null &&
                    richValue.FindPropertyRelative("_graph") != null,
                    "Rich inherited object, collection, and graph fields were not exposed.");

                var aliasPayload = serialized.FindProperty("alias._serializedPayload");
                Check(
                    HasManagedReferenceType(aliasPayload, AliasCurrentPayloadType) &&
                    serialized.FindProperty("alias._serializedPayload.value._amount") != null,
                    "Current alias did not use its concrete typename and private amount field.");

                var commandPayload = serialized.FindProperty("command._serializedPayload");
                Check(
                    HasManagedReferenceType(commandPayload, NestedRunPayloadType) &&
                    serialized.FindProperty("command._serializedPayload.value._speed") != null,
                    "Nested Run did not use its concrete typename and private speed field.");

                Check(
                    serialized.FindProperty("shapeArray.Array.data[0]._serializedPayload.value._speed") != null &&
                    serialized.FindProperty("shapeList.Array.data[2]._serializedPayload.value._speed") != null,
                    "Array/list union elements did not expose inherited payload paths.");
                Check(
                    serialized.FindProperty("shape._serializedPayload.value._kind") == null,
                    "The payload value unexpectedly contains a nested discriminator field.");
            }
        }

        private static void AssertAliasCanonicalization()
        {
            var original = ReadAsset(AliasFixturePath);
            Check(original.IndexOf(AliasCurrentPayloadType, StringComparison.Ordinal) >= 0,
                "The alias fixture was not initially written with its current concrete typename.");
            var former = original.Replace(
                AliasCurrentPayloadType,
                AliasFormerPayloadType,
                StringComparison.Ordinal);
            Check(!string.Equals(original, former, StringComparison.Ordinal),
                "The alias YAML transform did not create a former-name payload.");

            File.WriteAllText(ProjectPath(AliasFixturePath), former);
            AssetDatabase.ImportAsset(AliasFixturePath, ImportAssetOptions.ForceUpdate);
            var aliasAsset = AssetDatabase.LoadAssetAtPath<ConcreteWrapperAliasAsset>(AliasFixturePath);
            Check(aliasAsset != null, "The former-name alias fixture could not be reloaded.");
            if (aliasAsset == null)
                return;

            var formerYaml = ReadAsset(AliasFixturePath);
            Check(formerYaml.IndexOf(AliasFormerPayloadType, StringComparison.Ordinal) >= 0,
                "The imported former-name fixture did not retain its former concrete typename before inspection.");
            var formerPayload = GetSerializedPayload(aliasAsset.alias);
            AssertAliasRuntimeValues(aliasAsset);
            Check(
                formerPayload != null && formerPayload.GetType().Name == "OldSerializedPayload",
                "The former-name alias cache was not the OldSerializedPayload wrapper (actual=" +
                (formerPayload == null ? "null" : formerPayload.GetType().FullName) + ").");
            AssertDrawerPayloadTypeFallback(
                aliasAsset,
                typeof(ConcreteWrapperAlias),
                "alias",
                formerPayload,
                AliasFormerPayloadType,
                expectedVariantIndex: 0);
            AssertAliasFixture(aliasAsset);
            var canonicalBeforeSave = ReadAsset(AliasFixturePath);
            Check(canonicalBeforeSave.IndexOf(AliasFormerPayloadType, StringComparison.Ordinal) >= 0,
                "The imported former-name fixture did not retain its former concrete typename before save.");

            EditorUtility.SetDirty(aliasAsset);
            AssetDatabase.SaveAssetIfDirty(aliasAsset);
            var canonical = ReadAsset(AliasFixturePath);
            Check(canonical.IndexOf(AliasCurrentPayloadType, StringComparison.Ordinal) >= 0,
                "Former-name alias save did not canonicalize its concrete typename.");
            Check(canonical.IndexOf(AliasFormerPayloadType, StringComparison.Ordinal) < 0,
                "Former-name alias save retained the former concrete typename.");
            File.WriteAllText(ProjectPath(AliasBaselinePath), canonical);
        }

        private static void AssertAliasFixture(ConcreteWrapperAliasAsset asset)
        {
            AssertAliasRuntimeValues(asset);
            using (var serialized = new SerializedObject(asset))
            {
                serialized.Update();
                Check(
                    HasManagedReferenceType(
                        serialized.FindProperty("alias._serializedPayload"),
                        AliasCurrentPayloadType),
                    "Alias SerializedObject did not identify the canonical concrete wrapper.");
            }
        }

        private static void AssertAliasRuntimeValues(ConcreteWrapperAliasAsset asset)
        {
            Check(
                asset.alias.kind == ConcreteWrapperAlias.Kind.Current &&
                asset.alias.current.amount == 77 && asset.alias.current.label == "alias",
                "Former-name alias did not restore the current variant and fields.");
        }

        private static void AssertDrawerPayloadTypeFallback(
            UnityEngine.Object target,
            Type unionType,
            string unionPropertyPath,
            object? concretePayload,
            string expectedPayloadType,
            int expectedVariantIndex)
        {
            Check(concretePayload != null,
                unionType.Name + " had no concrete payload for drawer fallback probing.");
            if (concretePayload == null)
                return;

            var drawerType = FindGeneratedDrawer(unionType);
            Check(drawerType != null,
                "TypeCache did not discover the generated drawer for " + unionType.FullName + ".");
            if (drawerType == null)
                return;

            var drawer = Activator.CreateInstance(drawerType, nonPublic: true);
            Check(drawer != null, "The generated drawer could not be instantiated.");
            if (drawer == null)
                return;

            var method = FindReadPayloadTypeIndex(drawerType);
            Check(method != null,
                "The generated drawer did not expose its inherited ReadPayloadTypeIndex method.");
            if (method == null)
                return;

            using (var serialized = new SerializedObject(target))
            {
                serialized.Update();
                var unionProperty = serialized.FindProperty(unionPropertyPath);
                var kind = serialized.FindProperty(unionPropertyPath + "._kind");
                var payloadProperty = serialized.FindProperty(unionPropertyPath + "._serializedPayload");
                Check(
                    unionProperty != null && kind != null && payloadProperty != null,
                    "The drawer fallback probe could not find the union, kind, and payload properties.");
                if (unionProperty == null || kind == null || payloadProperty == null)
                    return;

                payloadProperty.managedReferenceValue = concretePayload;
                Check(
                    payloadProperty.managedReferenceValue != null &&
                    payloadProperty.managedReferenceFullTypename.IndexOf(
                        expectedPayloadType,
                        StringComparison.Ordinal) >= 0,
                    "The drawer fallback probe installed an unexpected managed-reference wrapper (expected=" +
                    expectedPayloadType + ", actual=" + payloadProperty.managedReferenceFullTypename +
                    ", runtime=" + concretePayload.GetType().FullName + ").");
                kind.intValue = 99;
                var index = InvokeReadPayloadTypeIndex(
                    drawer,
                    method,
                    unionProperty,
                    payloadProperty,
                    concretePayload);
                Check(
                    index == expectedVariantIndex,
                    "The drawer inferred variant index " + index + " instead of " + expectedVariantIndex +
                    " from the concrete payload while the discriminator was invalid.");
            }
        }

        private static void AssertDrawerSharedManagedReferenceTraversal(ConcreteWrapperAsset sharedAsset)
        {
            var drawerType = FindGeneratedDrawer(typeof(ConcreteWrapperFieldMatrix));
            Check(drawerType != null, "TypeCache did not discover the FieldMatrix drawer for alias traversal.");
            if (drawerType == null)
                return;

            var drawer = Activator.CreateInstance(drawerType, nonPublic: true);
            Check(drawer != null, "The FieldMatrix drawer could not be instantiated for alias traversal.");
            if (drawer == null)
                return;

            var method = FindHasSharedManagedReference(drawerType);
            Check(method != null, "The generated drawer did not expose HasSharedManagedReference.");
            if (method == null)
                return;

            var wrapper = GetSerializedPayload(sharedAsset.shape);
            Check(wrapper != null, "The canonical Move wrapper was unavailable for alias traversal.");
            if (wrapper == null)
                return;

            var unsharedAsset = ScriptableObject.CreateInstance<ConcreteWrapperSharedWrapperProbeAsset>();
            var cycle = new ConcreteWrapperGraphNode { name = "unshared-cycle", value = 1 };
            cycle.next = cycle;
            unsharedAsset.graphRoot = cycle;
            unsharedAsset.left = wrapper;
            unsharedAsset.right = null;
            Check(
                ManagedReferenceUtility.SetManagedReferenceIdForObject(unsharedAsset, cycle, 0),
                "The unshared cyclic graph could not be assigned managed-reference ID 0.");
            try
            {
                using (var unsharedSerialized = new SerializedObject(unsharedAsset))
                {
                    unsharedSerialized.Update();
                    var unsharedPayload = unsharedSerialized.FindProperty("left");
                    var unsharedRight = unsharedSerialized.FindProperty("right");
                    var unsharedGraph = unsharedSerialized.FindProperty("graphRoot");
                    Check(unsharedPayload != null && unsharedPayload.managedReferenceValue != null &&
                        unsharedPayload.managedReferenceId >= 0 &&
                        unsharedRight != null && unsharedRight.managedReferenceValue == null &&
                        unsharedRight.managedReferenceId != unsharedPayload.managedReferenceId &&
                        unsharedGraph != null && unsharedGraph.managedReferenceId == 0,
                        "The unshared wrapper probe did not expose the expected managed-reference state.");
                    if (unsharedPayload == null)
                        return;

                    var unsharedResult = InvokeHasSharedManagedReference(drawer, method, unsharedPayload);
                    Check(!unsharedResult,
                        "Cyclic traversal incorrectly classified an unshared wrapper as shared.");
                }

                var sharedProbe = ScriptableObject.CreateInstance<ConcreteWrapperSharedWrapperProbeAsset>();
                var sharedCycle = new ConcreteWrapperGraphNode { name = "shared-cycle", value = 2 };
                sharedCycle.next = sharedCycle;
                sharedProbe.graphRoot = sharedCycle;
                sharedProbe.left = wrapper;
                sharedProbe.right = wrapper;
                Check(
                    ManagedReferenceUtility.SetManagedReferenceIdForObject(sharedProbe, wrapper, 0),
                    "The shared wrapper could not be assigned managed-reference ID 0.");
                try
                {
                    using (var sharedSerialized = new SerializedObject(sharedProbe))
                    {
                        sharedSerialized.Update();
                        var sharedPayload = sharedSerialized.FindProperty("left");
                        var repeatedPayload = sharedSerialized.FindProperty("right");
                        Check(sharedPayload != null && repeatedPayload != null &&
                            sharedPayload.managedReferenceValue != null &&
                            sharedPayload.managedReferenceId == 0 &&
                            sharedPayload.managedReferenceId == repeatedPayload.managedReferenceId &&
                            ReferenceEquals(sharedPayload.managedReferenceValue, repeatedPayload.managedReferenceValue),
                            "The shared wrapper probe did not retain equal ID 0 and managed-reference identity.");
                        if (sharedPayload == null)
                            return;

                        var firstResult = InvokeHasSharedManagedReference(drawer, method, sharedPayload);
                        var repeatedResult = InvokeHasSharedManagedReference(drawer, method, sharedPayload);
                        Check(firstResult,
                            "The actual shared wrapper alias was not detected through SerializedObject.");
                        Check(repeatedResult,
                            "Repeated managed-reference traversal skipped the actual shared wrapper alias.");
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(sharedProbe);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(unsharedAsset);
            }
        }

        private static MethodInfo? FindHasSharedManagedReference(Type drawerType)
        {
            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var current = drawerType;
            while (current != null)
            {
                var method = current.GetMethods(flags)
                    .FirstOrDefault(candidate => candidate.Name == "HasSharedManagedReference");
                if (method != null)
                    return method;
                current = current.BaseType!;
            }

            return null;
        }

        private static bool InvokeHasSharedManagedReference(
            object drawer,
            MethodInfo method,
            SerializedProperty payload)
        {
            try
            {
                return (bool)method.Invoke(drawer, new object[] { payload })!;
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
        }

        [Serializable]
        private sealed class ConcreteWrapperSharedWrapperProbeAsset : ScriptableObject
        {
            [SerializeReference]
            public ConcreteWrapperGraphNode? graphRoot;

            [SerializeReference]
            public object? left;

            [SerializeReference]
            public object? right;
        }

        private static Type? FindGeneratedDrawer(Type unionType)
        {
            foreach (var type in TypeCache.GetTypesWithAttribute<CustomPropertyDrawer>())
            {
                foreach (var attribute in type.GetCustomAttributesData())
                {
                    if (attribute.AttributeType != typeof(CustomPropertyDrawer) ||
                        attribute.ConstructorArguments.Count == 0 ||
                        !(attribute.ConstructorArguments[0].Value is Type target))
                        continue;

                    if (target == unionType ||
                        (unionType.IsGenericType && target == unionType.GetGenericTypeDefinition()))
                        return type;
                }
            }

            return null;
        }

        private static MethodInfo? FindReadPayloadTypeIndex(Type drawerType)
        {
            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var current = drawerType;
            while (current != null)
            {
                var method = current.GetMethods(flags)
                    .FirstOrDefault(candidate => candidate.Name == "ReadPayloadTypeIndex");
                if (method != null)
                    return method;
                current = current.BaseType!;
            }

            return null;
        }

        private static int InvokeReadPayloadTypeIndex(
            object drawer,
            MethodInfo method,
            SerializedProperty unionProperty,
            SerializedProperty payloadProperty,
            object concretePayload)
        {
            var arguments = new object?[method.GetParameters().Length];
            var parameters = method.GetParameters();
            for (var index = 0; index < parameters.Length; index++)
            {
                var parameter = parameters[index];
                if (parameter.ParameterType == typeof(SerializedProperty))
                {
                    arguments[index] = parameter.Name != null &&
                        parameter.Name.IndexOf("payload", StringComparison.OrdinalIgnoreCase) >= 0
                        ? payloadProperty
                        : unionProperty;
                }
                else if (parameter.ParameterType == typeof(Type))
                {
                    arguments[index] = concretePayload.GetType();
                }
                else if (parameter.ParameterType == typeof(object) ||
                    parameter.ParameterType.IsInstanceOfType(concretePayload))
                {
                    arguments[index] = concretePayload;
                }
                else
                {
                    throw new InvalidOperationException(
                        "ReadPayloadTypeIndex has an unsupported parameter: " + parameter.ParameterType.FullName);
                }
            }

            try
            {
                var result = method.Invoke(drawer, arguments);
                return Convert.ToInt32(result);
            }
            catch (TargetInvocationException exception)
            {
                throw exception.InnerException ?? exception;
            }
        }

        private static void AssertLegacyFixtureValues(LegacyGenericWrapperAsset asset)
        {
            Check(
                asset.shape.kind == ConcreteWrapperShape.Kind.Move &&
                asset.shape.move.destination == new Vector2(6f, 7f) &&
                asset.shape.move.speed == 8.5f,
                "Legacy Move payload values did not restore.");
        }

        private static bool IsLegacyGenericPayload(object? payload)
        {
            if (payload == null)
                return false;

            var type = payload.GetType();
            return (type.FullName ?? string.Empty).IndexOf(LegacyGenericTypeMarker, StringComparison.Ordinal) >= 0 ||
                (type.BaseType?.FullName ?? string.Empty).IndexOf(LegacyGenericTypeMarker, StringComparison.Ordinal) >= 0;
        }

        private static bool IsCanonicalMovePayload(object? payload)
            => payload != null && payload.GetType().Name == "MoveSerializedPayload" &&
                (payload.GetType().FullName ?? string.Empty).IndexOf("SerializedPayloads", StringComparison.Ordinal) >= 0;

        private static bool HasManagedReferenceType(SerializedProperty? property, string expected)
            => property != null &&
                property.propertyType == SerializedPropertyType.ManagedReference &&
                property.managedReferenceId != 0 &&
                property.managedReferenceFullTypename.IndexOf(expected, StringComparison.Ordinal) >= 0;

        private static object? GetSerializedPayload<T>(T value)
        {
            var field = typeof(T).GetField(
                "_serializedPayload",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Check(field != null, typeof(T).Name + " has no _serializedPayload field.");
            return field == null ? null : field.GetValue(value);
        }

        private static void AssertUnchangedSave(
            string assetPath,
            string operation,
            Action save,
            string baseline)
        {
            save();
            var current = ReadAsset(assetPath);
            Check(
                string.Equals(current, baseline, StringComparison.Ordinal),
                operation + " rewrote unchanged serialized YAML or managed-reference IDs.");
            Check(
                ManagedReferenceIds(current).Count == ManagedReferenceIds(baseline).Count,
                operation + " changed the number of managed-reference IDs.");
            Check(
                SameManagedReferenceIds(current, baseline),
                operation + " changed the managed-reference ID sequence.");
        }

        private static void EnsureResourceFolder()
            => Directory.CreateDirectory(ProjectPath(ResourceFolder));

        private static void DeleteCanonicalAssets()
        {
            foreach (var path in new[] { FixturePath, AliasFixturePath, TexturePath })
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.Refresh();
        }

        private static void DeleteLegacyAsset()
        {
            AssetDatabase.DeleteAsset(LegacyFixturePath);
            AssetDatabase.Refresh();
        }

        private static Texture2D CreateTexture()
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = "ConcreteWrapperTexture",
            };
            texture.SetPixel(0, 0, Color.magenta);
            texture.SetPixel(1, 0, Color.cyan);
            texture.SetPixel(0, 1, Color.yellow);
            texture.SetPixel(1, 1, Color.black);
            texture.Apply();
            return texture;
        }

        private static ConcreteWrapperAsset BuildFixture(Texture2D texture)
        {
            var root = new ConcreteWrapperGraphNode
            {
                name = "root",
                value = 1,
            };
            var child = new ConcreteWrapperGraphNode
            {
                name = "child",
                value = 2,
            };
            root.next = child;
            child.next = root;

            var asset = ScriptableObject.CreateInstance<ConcreteWrapperAsset>();
            asset.shape = ConcreteWrapperShape.Move(new Vector2(2f, 3f), 7.5f);
            asset.speech = ConcreteWrapperShape.Speak("hello");
            asset.empty = ConcreteWrapperShape.Empty();
            asset.shapeArray = new[]
            {
                ConcreteWrapperShape.Move(new Vector2(4f, 5f), 6f),
                ConcreteWrapperShape.Empty(),
                ConcreteWrapperShape.Move(new Vector2(8f, 9f), 11f),
            };
            asset.shapeList = new List<ConcreteWrapperShape>
            {
                ConcreteWrapperShape.Empty(),
                ConcreteWrapperShape.Speak("list"),
                ConcreteWrapperShape.Move(new Vector2(12f, 13f), 14f),
            };
            asset.rich = ConcreteWrapperFieldMatrix.Rich(
                17,
                "rich",
                texture,
                root,
                new List<int> { 3, 6, 9 });
            asset.alias = ConcreteWrapperAlias.Current(99, "canonical");
            asset.command = ConcreteWrapperNestedOwner.Command.Run(4.5f);
            asset.sharedTexture = texture;
            asset.graphRoot = root;
            return asset;
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

        private static string ProjectPath(string relativePath)
            => Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, relativePath));

        private static void Check(bool condition, string message)
        {
            _checks++;
            if (!condition)
                throw new InvalidOperationException(message);
        }

        [Serializable]
        private sealed class PhaseReport
        {
            public string phase = string.Empty;
            public string unityVersion = string.Empty;
            public string status = string.Empty;
            public int checks;
            public List<string> failures = new List<string>();
        }
    }

}
