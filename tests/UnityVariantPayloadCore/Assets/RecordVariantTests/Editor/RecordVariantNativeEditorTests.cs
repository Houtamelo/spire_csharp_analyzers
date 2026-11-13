#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Spire.UnityRecordVariantTests;

public static class RecordVariantNativeEditorTests
{
    private const string AssetFolder = "Assets/RecordVariantTests/Generated";
    private const string AssetPath = AssetFolder + "/RecordVariantFixture.asset";
    private const string FormerAliasAssetPath = AssetFolder + "/RecordVariantFormerAlias.asset";
    private const string ExplicitAliasAssetPath = AssetFolder + "/RecordVariantExplicitAlias.asset";
    private const string LegacyMethodAliasAssetPath = AssetFolder + "/RecordVariantLegacyMethodAlias.asset";
    private const string WriteReportPath = "record-variant-native-write-results.json";
    private const string ReadReportPath = "record-variant-native-read-results.json";

    public static void Write()
        => RunPhase("write", WriteReportPath, WriteAsset);

    public static void Read()
        => RunPhase("read", ReadReportPath, ReadAsset);

    private static void RunPhase(string phase, string reportName, Action<Report> action)
    {
        var report = new Report
        {
            phase = phase,
            unityVersion = Application.unityVersion,
        };

        try
        {
            action(report);
        }
        catch (Exception exception)
        {
            report.failures.Add(exception.ToString());
        }

        report.status = report.failures.Count == 0 ? "pass" : "fail";
        File.WriteAllText(ProjectPath(reportName), JsonUtility.ToJson(report, true));
        EditorApplication.Exit(report.status == "pass" ? 0 : 1);
    }

    private static void WriteAsset(Report report)
    {
        EnsureFolder();
        if (AssetDatabase.LoadAssetAtPath<RecordVariantAsset>(AssetPath) is not null)
            AssetDatabase.DeleteAsset(AssetPath);
        if (AssetDatabase.LoadAssetAtPath<RecordVariantAsset>(FormerAliasAssetPath) is not null)
            AssetDatabase.DeleteAsset(FormerAliasAssetPath);
        if (AssetDatabase.LoadAssetAtPath<RecordVariantAsset>(ExplicitAliasAssetPath) is not null)
            AssetDatabase.DeleteAsset(ExplicitAliasAssetPath);
        if (AssetDatabase.LoadAssetAtPath<RecordVariantAsset>(LegacyMethodAliasAssetPath) is not null)
            AssetDatabase.DeleteAsset(LegacyMethodAliasAssetPath);

        var asset = ScriptableObject.CreateInstance<RecordVariantAsset>();
        var node = new DerivedRecordNode { id = 17, label = "shared", derivedValue = 23 };
        asset.partialRecord = new RecordVariantUnion.Slider(42) with { Extra = 9001 };
        asset.fullyAnnotated = new RecordVariantUnion.FullyAnnotated(43);
        asset.explicitRecord = new RecordVariantUnion.Explicit(45);
        asset.methodControl = RecordVariantUnion.Method(44);
        asset.fieldlessRecord = new RecordVariantUnion.Empty();
        asset.managedRecord = new RecordVariantUnion.Managed(node);
        asset.managedNullRecord = new RecordVariantUnion.Managed(null);
        asset.managedSharedRecord = new RecordVariantUnion.Managed(node);

        AssetDatabase.CreateAsset(asset, AssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        asset = AssetDatabase.LoadAssetAtPath<RecordVariantAsset>(AssetPath);
        Require(asset is not null, report, "The record-variant asset did not reload after creation.");
        if (asset is null)
            return;

        AssertEqualityAndExtraState(asset, report);
        AssertSerializedProperties(asset, report);
        AssertManagedReferenceState(asset, node, report);

        var yaml = File.ReadAllText(AssetPath);
        Require(yaml.Contains("<value>k__BackingField", StringComparison.Ordinal), report,
            "Native YAML did not contain the generated record backing field.");
        Require(yaml.Contains("fullyAnnotated", StringComparison.Ordinal), report,
            "Native YAML did not contain the fully annotated record field.");
        Require(yaml.Contains("methodControl", StringComparison.Ordinal), report,
            "Native YAML did not contain the method-declared control field.");

        report.checks += 4;
        File.WriteAllText(ProjectPath("record-variant-native-baseline.yaml"), yaml);

        var aliasYaml = yaml.Replace(
            "SliderSerializedPayload",
            "OldSliderSerializedPayload")
            .Replace(
                "m_Name: RecordVariantFixture",
                "m_Name: RecordVariantFormerAlias");
        File.WriteAllText(ProjectPath(FormerAliasAssetPath), aliasYaml);
        AssetDatabase.ImportAsset(FormerAliasAssetPath, ImportAssetOptions.ForceUpdate);
        var aliasAsset = AssetDatabase.LoadAssetAtPath<RecordVariantAsset>(FormerAliasAssetPath);
        Require(aliasAsset is not null, report,
            "The former-name YAML alias asset did not import.");
        if (aliasAsset is not null)
        {
            AssertFormerNameAlias(aliasAsset, asset, report);
            File.WriteAllText(ProjectPath("record-variant-native-former-alias-baseline.yaml"), aliasYaml);
        }

        var explicitAliasYaml = yaml.Replace(
            "ExplicitSerializedPayload",
            "OldExplicitSerializedPayload")
            .Replace(
                "m_Name: RecordVariantFixture",
                "m_Name: RecordVariantExplicitAlias");
        File.WriteAllText(ProjectPath(ExplicitAliasAssetPath), explicitAliasYaml);
        AssetDatabase.ImportAsset(ExplicitAliasAssetPath, ImportAssetOptions.ForceUpdate);
        var explicitAliasAsset = AssetDatabase.LoadAssetAtPath<RecordVariantAsset>(ExplicitAliasAssetPath);
        Require(explicitAliasAsset is not null, report,
            "The explicit-field former-name YAML alias asset did not import.");
        if (explicitAliasAsset is not null)
        {
            AssertFormerNameExplicitAlias(explicitAliasAsset, asset, report);
            File.WriteAllText(ProjectPath("record-variant-native-explicit-alias-baseline.yaml"), explicitAliasYaml);
        }

        var legacyMethodAliasYaml = yaml.Replace(
            "SliderSerializedPayload",
            "OldSliderSerializedPayload")
            .Replace(
                "m_Name: RecordVariantFixture",
                "m_Name: RecordVariantLegacyMethodAlias");
        legacyMethodAliasYaml = ReplaceWrapperFieldKey(
            legacyMethodAliasYaml,
            "OldSliderSerializedPayload",
            "<value>k__BackingField",
            "_value");
        Require(
            legacyMethodAliasYaml.Contains("class: RecordVariantUnionTypes/SerializedPayloads/OldSliderSerializedPayload", StringComparison.Ordinal) &&
            legacyMethodAliasYaml.Contains("_value: 42", StringComparison.Ordinal),
            report,
            "The legacy method alias YAML did not rewrite the OldSlider field key.");
        Require(
            legacyMethodAliasYaml.Contains("<value>k__BackingField: 43", StringComparison.Ordinal),
            report,
            "The legacy method alias rewrite changed an unrelated record field key.");
        File.WriteAllText(ProjectPath(LegacyMethodAliasAssetPath), legacyMethodAliasYaml);
        AssetDatabase.ImportAsset(LegacyMethodAliasAssetPath, ImportAssetOptions.ForceUpdate);
        var legacyMethodAliasAsset = AssetDatabase.LoadAssetAtPath<RecordVariantAsset>(LegacyMethodAliasAssetPath);
        Require(legacyMethodAliasAsset is not null, report,
            "The legacy-method former-name YAML alias asset did not import.");
        if (legacyMethodAliasAsset is not null)
        {
            AssertFormerNameAlias(legacyMethodAliasAsset, asset, report);
            File.WriteAllText(ProjectPath("record-variant-native-legacy-method-alias-baseline.yaml"), legacyMethodAliasYaml);
        }
    }

    private static void ReadAsset(Report report)
    {
        var asset = AssetDatabase.LoadAssetAtPath<RecordVariantAsset>(AssetPath);
        Require(asset is not null, report, "The record-variant asset was not available in the read process.");
        if (asset is null)
            return;

        AssertEqualityAndExtraState(asset, report);
        AssertSerializedProperties(asset, report);
        AssertManagedReferenceState(asset, null, report);

        var baselinePath = ProjectPath("record-variant-native-baseline.yaml");
        Require(File.Exists(baselinePath), report, "The write-process YAML baseline is missing.");
        if (File.Exists(baselinePath))
        {
            var current = File.ReadAllText(AssetPath);
            var baseline = File.ReadAllText(baselinePath);
            Require(current == baseline, report,
                "The record-variant YAML changed between the write and read processes.");
            report.checks++;
        }

        var aliasAsset = AssetDatabase.LoadAssetAtPath<RecordVariantAsset>(FormerAliasAssetPath);
        Require(aliasAsset is not null, report,
            "The former-name YAML alias asset was not available in the read process.");
        if (aliasAsset is not null)
            AssertFormerNameAlias(aliasAsset, asset, report);

        var explicitAliasAsset = AssetDatabase.LoadAssetAtPath<RecordVariantAsset>(ExplicitAliasAssetPath);
        Require(explicitAliasAsset is not null, report,
            "The explicit-field former-name YAML alias asset was not available in the read process.");
        if (explicitAliasAsset is not null)
            AssertFormerNameExplicitAlias(explicitAliasAsset, asset, report);

        var legacyMethodAliasAsset = AssetDatabase.LoadAssetAtPath<RecordVariantAsset>(LegacyMethodAliasAssetPath);
        Require(legacyMethodAliasAsset is not null, report,
            "The legacy-method former-name YAML alias asset was not available in the read process.");
        if (legacyMethodAliasAsset is not null)
            AssertFormerNameAlias(legacyMethodAliasAsset, asset, report);
    }

    private static void AssertEqualityAndExtraState(RecordVariantAsset asset, Report report)
    {
        var first = new RecordVariantUnion.Slider(42) with { Extra = 1 };
        var second = new RecordVariantUnion.Slider(42) with { Extra = 999 };
        Require(!first.Equals(second), report,
            "The authored record's own equality unexpectedly ignored its extra state.");

        var firstUnion = (RecordVariantUnion)first;
        var secondUnion = (RecordVariantUnion)second;
        Require(firstUnion.Equals(secondUnion), report,
            "Union equality did not ignore authored non-positional record state.");
        Require(firstUnion.GetHashCode() == secondUnion.GetHashCode(), report,
            "Union hashing did not ignore authored non-positional record state.");

        var prepared = asset.partialRecord.__SpirePrepareForSerialization();
        var hydrated = prepared.__SpireHydrateSerialized();
        Require(hydrated.Equals(asset.partialRecord), report,
            "Prepare/hydrate changed logical union equality.");
        Require(hydrated.GetHashCode() == asset.partialRecord.GetHashCode(), report,
            "Prepare/hydrate changed logical union hashing.");
        Require(hydrated.slider.Extra == 0, report,
            "Union reconstruction retained non-positional record state unexpectedly.");
        report.checks += 6;
    }

    private static void AssertSerializedProperties(RecordVariantAsset asset, Report report)
    {
        using var serialized = new SerializedObject(asset);
        Require(serialized.FindProperty("partialRecord._serializedPayload.value") is not null, report,
            "The partial record did not expose the existing serialized payload wrapper.");
        Require(serialized.FindProperty("fullyAnnotated._serializedPayload.value") is not null, report,
            "The fully annotated record did not expose the existing serialized payload wrapper.");
        Require(serialized.FindProperty("explicitRecord._serializedPayload.value.value") is not null, report,
            "The explicit-field record did not expose its direct serialized value field.");
        Require(serialized.FindProperty("methodControl._serializedPayload.value") is not null, report,
            "The method control did not expose the existing serialized payload wrapper.");
        Require(serialized.FindProperty("fieldlessRecord._serializedPayload.value") is not null, report,
            "The fieldless record did not expose the existing serialized payload wrapper.");
        Require(serialized.FindProperty("partialRecord._serializedPayload.value.<value>k__BackingField") is not null, report,
            "The partial record value backing field was not exposed through SerializedObject.");
        Require(serialized.FindProperty("fullyAnnotated._serializedPayload.value.<value>k__BackingField") is not null, report,
            "The fully annotated record value backing field was not exposed through SerializedObject.");
        report.checks += 6;
    }

    private static void AssertManagedReferenceState(
        RecordVariantAsset asset,
        RecordNode? expected,
        Report report)
    {
        using var serialized = new SerializedObject(asset);
        var value = serialized.FindProperty("managedRecord._serializedPayload.value");
        Require(value is not null, report, "The managed record payload wrapper was not exposed.");
        if (value is null)
            return;

        var node = value.FindPropertyRelative("<node>k__BackingField");
        Require(node is not null, report,
            "The managed record backing field was not exposed through SerializedObject.");
        if (node is null)
            return;

        Require(node.propertyType == SerializedPropertyType.ManagedReference, report,
            "The record managed-reference field was not a native managed reference.");
        if (expected is not null)
        {
            Require(node.managedReferenceValue is DerivedRecordNode, report,
                "The derived managed-reference runtime type was not preserved.");
            var shared = serialized.FindProperty("managedSharedRecord._serializedPayload.value")!
                .FindPropertyRelative("<node>k__BackingField");
            Require(ReferenceEquals(node.managedReferenceValue, shared.managedReferenceValue), report,
                "The shared managed-reference identity was not preserved between union fields.");
            var nullNode = serialized.FindProperty("managedNullRecord._serializedPayload.value")!
                .FindPropertyRelative("<node>k__BackingField");
            Require(nullNode.managedReferenceValue is null, report,
                "The null managed-reference value was not preserved.");
        }
        else
        {
            Require(node.managedReferenceValue is not null, report,
                "The managed-reference value was lost on a fresh read process.");
            var shared = serialized.FindProperty("managedSharedRecord._serializedPayload.value")!
                .FindPropertyRelative("<node>k__BackingField");
            Require(ReferenceEquals(node.managedReferenceValue, shared.managedReferenceValue), report,
                "The shared managed-reference identity was not preserved on read.");
            var nullNode = serialized.FindProperty("managedNullRecord._serializedPayload.value")!
                .FindPropertyRelative("<node>k__BackingField");
            Require(nullNode.managedReferenceValue is null, report,
                "The null managed-reference value was not preserved on read.");
        }
        report.checks++;
    }

    private static void AssertFormerNameAlias(
        RecordVariantAsset aliasAsset,
        RecordVariantAsset canonicalAsset,
        Report report)
    {
        var canonicalPayload = canonicalAsset.partialRecord.slider;
        var aliasPayload = aliasAsset.partialRecord.slider;
        Require(aliasPayload.value == 42, report,
            "The former-name YAML alias did not restore the canonical record value.");
        Require(aliasAsset.partialRecord.kind == RecordVariantUnion.Kind.Slider, report,
            "The former-name YAML alias did not restore the canonical active variant.");
        Require(aliasAsset.partialRecord.Equals(canonicalAsset.partialRecord), report,
            "The former-name YAML alias did not restore union equality with the canonical asset.");
        Require(aliasAsset.partialRecord.GetHashCode() == canonicalAsset.partialRecord.GetHashCode(), report,
            "The former-name YAML alias did not restore the canonical union hash.");
        Require(aliasPayload.value == canonicalPayload.value, report,
            "The former-name YAML alias payload differs from the canonical record payload.");
        report.checks += 5;
    }

    private static void AssertFormerNameExplicitAlias(
        RecordVariantAsset aliasAsset,
        RecordVariantAsset canonicalAsset,
        Report report)
    {
        using var serialized = new SerializedObject(aliasAsset);
        var value = serialized.FindProperty("explicitRecord._serializedPayload.value.value");
        Require(value is not null && value.intValue == 45, report,
            "The explicit-field former-name YAML alias did not restore value 45.");
        Require(aliasAsset.explicitRecord.kind == RecordVariantUnion.Kind.Explicit, report,
            "The explicit-field former-name YAML alias did not restore its active variant.");
        Require(aliasAsset.explicitRecord.Equals(canonicalAsset.explicitRecord), report,
            "The explicit-field former-name YAML alias did not restore union equality.");
        Require(aliasAsset.explicitRecord.GetHashCode() == canonicalAsset.explicitRecord.GetHashCode(), report,
            "The explicit-field former-name YAML alias did not restore the canonical hash.");
        report.checks += 4;
    }

    private static string ReplaceWrapperFieldKey(
        string yaml,
        string wrapperTypeName,
        string oldFieldName,
        string newFieldName)
    {
        var typeMarker = "/" + wrapperTypeName;
        var typeStart = yaml.IndexOf(typeMarker, StringComparison.Ordinal);
        if (typeStart < 0)
            throw new InvalidOperationException(
                "Could not find serialized wrapper type marker: " + wrapperTypeName);

        var nextReference = yaml.IndexOf("\n    - rid:", typeStart, StringComparison.Ordinal);
        if (nextReference < 0)
            nextReference = yaml.Length;

        var prefix = yaml[..typeStart];
        var block = yaml[typeStart..nextReference];
        var suffix = yaml[nextReference..];
        if (!block.Contains(oldFieldName, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Could not find serialized field key '" + oldFieldName + "' in wrapper " + wrapperTypeName);

        var rewrittenBlock = block.Replace(oldFieldName, newFieldName);
        if (!rewrittenBlock.Contains(newFieldName, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Serialized wrapper rewrite did not add field key '" + newFieldName + "'.");

        return prefix + rewrittenBlock + suffix;
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/RecordVariantTests/Generated"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/RecordVariantTests"))
                AssetDatabase.CreateFolder("Assets", "RecordVariantTests");
            AssetDatabase.CreateFolder("Assets/RecordVariantTests", "Generated");
        }
    }

    private static string ProjectPath(string relative)
        => Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, relative);

    private static void Require(bool condition, Report report, string message)
    {
        report.checks++;
        if (!condition)
            report.failures.Add(message);
    }

    [Serializable]
    private sealed class Report
    {
        public string phase = string.Empty;
        public string unityVersion = string.Empty;
        public string status = string.Empty;
        public int checks;
        public List<string> failures = new();
    }
}
