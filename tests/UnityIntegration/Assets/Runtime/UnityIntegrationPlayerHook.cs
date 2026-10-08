#nullable enable

using System;
using System.IO;
using UnityEngine;

namespace Spire.UnityIntegration
{

    public sealed class UnityIntegrationPlayerHook : MonoBehaviour
    {
        public const string ExpectedLabel = "player";
        public const string ExpectedBoxedFieldsLabel = "boxed-player";
        public const int ExpectedBoxedTupleValue = 26;
        public const int ExpectedBoxedGenericValue = 42;
        public const string ExpectedBoxedGenericLabel = "boxed-generic-player";
        public const uint ExpectedUnsafeRaw = 123;
        public const float ExpectedUnsafeAmount = 2.5f;
        public const string ExpectedUnsafeLabel = "unsafe-player";
        public const int ExpectedUnsafeGenericValue = 37;
        public const uint ExpectedUnsafeGenericCode = 19;
        public const float ExpectedUnsafeGenericWeight = 1.75f;
        public const string ExpectedUnsafeGenericLabel = "unsafe-generic-player";
        public const float ExpectedOverlapWidth = 2.5f;
        public const float ExpectedOverlapHeight = 4.5f;
        public const string ExpectedOverlapLabel = "overlap-player";
        public const string ReportArgument = "-spire-report";
        public const string ReportFileName = "spire-unity-integration-player.json";

        private void Start()
        {
            var host = GetComponent<UnitySerializationMono>();
            var loaded = host != null;
            var kind = loaded ? host!.single.kind.ToString() : "MissingHost";
            var label = string.Empty;
            var boxedFieldsLabel = string.Empty;
            var boxedTupleValue = 0;
            var boxedGenericValue = 0;
            var boxedGenericLabel = string.Empty;
            var unsafeRaw = 0u;
            var unsafeAmount = 0f;
            var unsafeLabel = string.Empty;
            var unsafeGenericValue = 0;
            var unsafeGenericCode = 0u;
            var unsafeGenericWeight = 0f;
            var unsafeGenericLabel = string.Empty;
            var overlapWidth = 0f;
            var overlapHeight = 0f;
            var overlapLabel = string.Empty;
            if (host != null)
            {
                var union = host.single;
                if (union.kind == UnityShapeSerializable.Kind.Label)
                    label = union.text;
                var boxedFields = host.boxedFields;
                if (boxedFields.kind == UnityBoxedFieldsUnion.Kind.Text)
                    boxedFieldsLabel = boxedFields.label;
                var boxedTuple = host.boxedTuple;
                if (boxedTuple.kind == UnityBoxedTupleUnion.Kind.Number)
                    boxedTupleValue = boxedTuple.value;
                var boxedGeneric = host.boxedFieldsGenericInt;
                if (boxedGeneric.kind == UnityBoxedFieldsGeneric<int>.Kind.Some)
                    boxedGenericValue = boxedGeneric.value;
                var boxedGenericString = host.boxedTupleGenericString;
                if (boxedGenericString.kind == UnityBoxedTupleGeneric<string>.Kind.Some)
                    boxedGenericLabel = boxedGenericString.value;
                var unsafeUnion = host.unsafeUnion;
                if (unsafeUnion.kind == UnityUnsafeOverlapUnion.Kind.Mixed)
                {
                    unsafeRaw = unsafeUnion.raw;
                    unsafeAmount = unsafeUnion.amount;
                    unsafeLabel = unsafeUnion.label;
                }
                var unsafeInt = host.unsafeGenericInt;
                if (unsafeInt.kind == UnityUnsafeOverlapGeneric<int>.Kind.Some)
                {
                    unsafeGenericValue = unsafeInt.value;
                    unsafeGenericCode = unsafeInt.code;
                    unsafeGenericWeight = unsafeInt.weight;
                }
                var unsafeString = host.unsafeGenericString;
                if (unsafeString.kind == UnityUnsafeOverlapGeneric<string>.Kind.Some)
                    unsafeGenericLabel = unsafeString.value;
                var overlap = host.overlapUnion;
                if (overlap.kind == UnityOverlapUnion.Kind.Rectangle)
                {
                    overlapWidth = overlap.width;
                    overlapHeight = overlap.height;
                }
                var overlapReference = host.overlapReference;
                if (overlapReference.kind == UnityOverlapReferenceUnion.Kind.Text)
                    overlapLabel = overlapReference.label;
            }
            var report = new PlayerReport
            {
                unityVersion = Application.unityVersion,
                loaded = loaded,
                kind = kind,
                label = label,
                boxedFieldsLabel = boxedFieldsLabel,
                boxedTupleValue = boxedTupleValue,
                boxedGenericValue = boxedGenericValue,
                boxedGenericLabel = boxedGenericLabel,
                unsafeRaw = unsafeRaw,
                unsafeAmount = unsafeAmount,
                unsafeLabel = unsafeLabel,
                unsafeGenericValue = unsafeGenericValue,
                unsafeGenericCode = unsafeGenericCode,
                unsafeGenericWeight = unsafeGenericWeight,
                unsafeGenericLabel = unsafeGenericLabel,
                overlapWidth = overlapWidth,
                overlapHeight = overlapHeight,
                overlapLabel = overlapLabel,
                runtimeAssembly = typeof(UnityShapeSerializable).Assembly.FullName ?? string.Empty,
            };

            var path = ResolveReportPath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            var valid = report.loaded &&
                kind == UnityShapeSerializable.Kind.Label.ToString() &&
                label == ExpectedLabel &&
                boxedFieldsLabel == ExpectedBoxedFieldsLabel &&
                boxedTupleValue == ExpectedBoxedTupleValue &&
                boxedGenericValue == ExpectedBoxedGenericValue &&
                boxedGenericLabel == ExpectedBoxedGenericLabel &&
                unsafeRaw == ExpectedUnsafeRaw &&
                unsafeAmount == ExpectedUnsafeAmount &&
                unsafeLabel == ExpectedUnsafeLabel &&
                unsafeGenericValue == ExpectedUnsafeGenericValue &&
                unsafeGenericCode == ExpectedUnsafeGenericCode &&
                unsafeGenericWeight == ExpectedUnsafeGenericWeight &&
                unsafeGenericLabel == ExpectedUnsafeGenericLabel &&
                overlapWidth == ExpectedOverlapWidth &&
                overlapHeight == ExpectedOverlapHeight &&
                overlapLabel == ExpectedOverlapLabel;
            Application.Quit(valid ? 0 : 1);
        }

        private static string ResolveReportPath()
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length; index++)
            {
                if (!string.Equals(arguments[index], ReportArgument, StringComparison.Ordinal))
                    continue;

                if (index + 1 >= arguments.Length || string.IsNullOrWhiteSpace(arguments[index + 1]))
                    throw new InvalidOperationException(ReportArgument + " requires a report path.");

                return Path.GetFullPath(arguments[index + 1]);
            }

            return Path.Combine(Application.persistentDataPath, ReportFileName);
        }

        [Serializable]
        private sealed class PlayerReport
        {
            public string unityVersion = string.Empty;
            public bool loaded;
            public string kind = string.Empty;
            public string label = string.Empty;
            public string boxedFieldsLabel = string.Empty;
            public int boxedTupleValue;
            public int boxedGenericValue;
            public string boxedGenericLabel = string.Empty;
            public uint unsafeRaw;
            public float unsafeAmount;
            public string unsafeLabel = string.Empty;
            public int unsafeGenericValue;
            public uint unsafeGenericCode;
            public float unsafeGenericWeight;
            public string unsafeGenericLabel = string.Empty;
            public float overlapWidth;
            public float overlapHeight;
            public string overlapLabel = string.Empty;
            public string runtimeAssembly = string.Empty;
        }
    }

}
