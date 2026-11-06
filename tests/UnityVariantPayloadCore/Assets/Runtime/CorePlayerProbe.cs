#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Houtamelo.Spire;
using UnityEngine;

namespace Spire.UnityVariantPayloadCore
{

    public static class CorePlayerProbe
    {
        private const string ReportArgument = "-spire-report";
        private const string DefaultReportName = "unity-variant-payload-core-player.json";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Run()
        {
            var report = new PlayerReport
            {
                unityVersion = Application.unityVersion,
#if ENABLE_IL2CPP
            backend = "IL2CPP",
#else
                backend = "Mono",
#endif
                status = "fail",
                checks = 0,
                failures = new List<string>(),
                debugBuild = Debug.isDebugBuild,
                processorBits = IntPtr.Size * 8,
                genericUnionSerializationSupported =
                    SerializedUnionNativeCapabilities.GenericUnionSerializationSupported,
                genericUnionSerializationApplicability =
                    SerializedUnionNativeCapabilities.GenericUnionSerializationApplicability,
            };

            activeReport = report;

            try
            {
                Check(report, "CoreShape payloads", CheckCoreShape);
                Check(report, "generic concrete payload", CheckGeneric);
                Check(report, "grouped payload type loading", CheckGroupedPayloadTypes);
                Check(report, "typed references", CheckTypedReferences);
                Check(report, "shared references", CheckSharedReferences);
                Check(report, "shared tuple references", CheckSharedTupleReferences);
                Check(report, "automatic reference storage", CheckAutoReferences);
                Check(report, "unknown unmanaged payload", CheckUnknownBlock);
                Check(report, "nested payload scope", CheckNestedCommand);
                Check(report, "serialization hydration state", CheckSerializationHydrationState);
                Check(report, "serialized unions", () => SerializedUnionPlayerProbe.RunChecks(Require));
                report.status = report.failures.Count == 0 ? "pass" : "fail";
            }
            catch (Exception exception)
            {
                report.failures.Add(exception.ToString());
            }

            var path = ResolveReportPath();
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
            }
            catch (Exception exception)
            {
                report.status = "fail";
                report.failures.Add("Could not write player report: " + exception);
            }

            CorePlayerExitScheduler.Schedule(report.status == "pass" ? 0 : 1);
        }

        private static void Check(PlayerReport report, string name, Action assertion)
        {
            try
            {
                assertion();
            }
            catch (Exception exception)
            {
                report.failures.Add(name + ": " + exception.Message);
            }
        }

        private static void CheckCoreShape()
        {
            var value = CoreShape.Circle(new Vector2(2f, 3f), 4f);
            if (value.kind != CoreShape.Kind.Circle)
                throw new InvalidOperationException("Circle kind was not selected.");
            Require(value.kind == CoreShape.Kind.Circle, "Circle kind was not selected.");
            Require(value.circle.position == new Vector2(2f, 3f), "Vector2 payload did not round-trip.");
            Require(value.circle.radius == 4f, "Circle radius did not round-trip.");

            var copiedPayload = value.circle;
            var changedCopy = CoreShape.Circle(copiedPayload.position, 9f);
            Require(changedCopy.circle.radius == 9f && value.circle.radius == 4f,
                "Replacing a copied payload changed the original union.");

            var matched = value switch
            {
                { kind: CoreShape.Kind.Circle, circle: var circle } => circle.radius,
                _ => -1f,
            };
            Require(matched == 4f, "Variant payload pattern did not deconstruct the active payload.");

            var triangle = CoreShape.Triangle(Vector3.zero, Vector3.right, Vector3.up);
            if (triangle.kind != CoreShape.Kind.Triangle)
                throw new InvalidOperationException("Triangle kind was not selected.");
            var (first, second, third) = triangle.triangle;
            Require(first == Vector3.zero && second == Vector3.right && third == Vector3.up,
                "Typed payload Deconstruct did not preserve field order.");
        }

        private static void CheckGeneric()
        {
            var value = GenericCore<int>.Value(7, 2.5f);
            if (value.kind != GenericCore<int>.Kind.Value)
                throw new InvalidOperationException("Generic value kind was not selected.");
            Require(value.kind == GenericCore<int>.Kind.Value, "Generic value kind was not selected.");
            var payload = value.value;
            Require(payload.value == 7 && payload.scale == 2.5f,
                "Generic-dependent and concrete fields did not round-trip.");
            var (number, scale) = payload;
            Require(number == 7 && scale == 2.5f, "Generic payload Deconstruct failed.");
        }

        private static void CheckGroupedPayloadTypes()
        {
            var genericTypes = typeof(SerializedGenericTypes1);
            var variants = genericTypes.GetNestedType("Variants", BindingFlags.Public);
            Require(variants != null, "The grouped generic union did not expose its public Variants container.");
            if (variants == null)
                return;

            var valueDefinition = variants.GetNestedType("Value`1", BindingFlags.Public);
            Require(valueDefinition != null, "The grouped generic payload type was not loadable by reflection.");
            if (valueDefinition == null)
                return;

            var valueType = valueDefinition.MakeGenericType(typeof(int));
            Require(
                valueType.FullName != null &&
                valueType.FullName.IndexOf("SerializedGenericTypes1+Variants+Value", StringComparison.Ordinal) >= 0,
                "The grouped generic payload type did not retain its arity-suffixed container name.");
            var value = Activator.CreateInstance(valueType, new object[] { 42, "generic" });
            Require(value != null, "The grouped generic payload type could not be constructed.");
            Require(
                value?.GetType().GetProperty("value", BindingFlags.Instance | BindingFlags.Public)?.GetValue(value) is 42,
                "The grouped generic payload did not preserve its value through reflection.");

            var nestedTypes = typeof(SerializedNestedOwner).GetNestedType(
                "CommandTypes",
                BindingFlags.Public | BindingFlags.NonPublic);
            Require(nestedTypes != null, "The nested union did not expose its grouped type container.");
        }

        private static void CheckTypedReferences()
        {
            var pair = new ManagedPair { label = "typed", count = 3 };
            var pairValue = TypedReferences.Pair(pair);
            if (pairValue.kind != TypedReferences.Kind.Pair)
                throw new InvalidOperationException("Typed pair kind was not selected.");
            Require(pairValue.pair.pair.label == "typed" && pairValue.pair.pair.count == 3,
                "Typed managed struct did not round-trip.");

            var text = new string('x', 3);
            var textValue = TypedReferences.Text(text);
            if (textValue.kind != TypedReferences.Kind.Text)
                throw new InvalidOperationException("Typed text kind was not selected.");
            Require(ReferenceEquals(text, textValue.text.text), "Typed string slot did not preserve identity.");

            var numbers = new[] { 2, 4, 8 };
            var numberValue = TypedReferences.Numbers(numbers);
            if (numberValue.kind != TypedReferences.Kind.Numbers)
                throw new InvalidOperationException("Typed numbers kind was not selected.");
            Require(ReferenceEquals(numbers, numberValue.numbers.numbers), "Typed array slot did not preserve identity.");

            var left = new string('a', 2);
            var right = new string('b', 2);
            var both = TypedReferences.Both(left, right);
            if (both.kind != TypedReferences.Kind.Both)
                throw new InvalidOperationException("Typed simultaneous kind was not selected.");
            Require(ReferenceEquals(left, both.both.left) && ReferenceEquals(right, both.both.right),
                "Simultaneous typed reference fields were not kept separate.");
        }

        private static void CheckSharedReferences()
        {
            var pair = new ManagedPair { label = "shared", count = 4 };
            var pairValue = SharedReferences.Pair(pair);
            if (pairValue.kind != SharedReferences.Kind.Pair)
                throw new InvalidOperationException("Shared pair kind was not selected.");
            Require(pairValue.pair.pair.label == "shared" && pairValue.pair.pair.count == 4,
                "Shared managed struct did not unbox correctly.");

            var text = new string('s', 2);
            Require(ReferenceEquals(text, SharedReferences.Text(text).text.text),
                "Shared reference slot did not preserve string identity.");

            var numbers = new[] { 1, 3, 5 };
            Require(ReferenceEquals(numbers, SharedReferences.Numbers(numbers).numbers.numbers),
                "Shared reference slot did not preserve array identity.");

#pragma warning disable SPIRE003
            var defaultValue = default(SharedReferences);
#pragma warning restore SPIRE003
            if (defaultValue.kind != SharedReferences.Kind.Pair)
                throw new InvalidOperationException("Default shared union did not select its first variant.");
            var defaultPair = defaultValue.pair.pair;
            Require(defaultPair.label is null && defaultPair.count == 0,
                "Default shared managed struct did not produce default field values.");
        }

        private static void CheckAutoReferences()
        {
            var a = new RefA { value = 11 };
            var b = new RefB { value = "b" };
            var c = new RefC { value = 1.5f };
            var d = new RefD { value = 19 };
            Require(ReferenceEquals(a, AutoReferences.A(a).a.value), "Auto A reference did not round-trip.");
            Require(ReferenceEquals(b, AutoReferences.B(b).b.value), "Auto B reference did not round-trip.");
            Require(ReferenceEquals(c, AutoReferences.C(c).c.value), "Auto C reference did not round-trip.");
            Require(ReferenceEquals(d, AutoReferences.D(d).d.value), "Auto D reference did not round-trip.");
        }

        private static void CheckSharedTupleReferences()
        {
            var first = SharedTupleReferences.First((null, 7));
            if (first.kind != SharedTupleReferences.Kind.First)
                throw new InvalidOperationException("Shared tuple first kind was not selected.");
            Require(first.first.value.Item1 is null && first.first.value.Item2 == 7,
                "Nullable tuple element did not preserve its null value.");

            var title = new string('t', 3);
            var second = SharedTupleReferences.Second((title, 8));
            if (second.kind != SharedTupleReferences.Kind.Second)
                throw new InvalidOperationException("Shared tuple second kind was not selected.");
            Require(ReferenceEquals(title, second.second.value.Item1) && second.second.value.Item2 == 8,
                "Tuple element names or nullable annotations changed the value representation.");

            var leftLabel = new string('l', 2);
            var rightLabel = new string('r', 2);
            var both = SharedTupleReferences.Both((leftLabel, 1), (rightLabel, 2));
            if (both.kind != SharedTupleReferences.Kind.Both)
                throw new InvalidOperationException("Shared tuple simultaneous kind was not selected.");
            Require(
                ReferenceEquals(leftLabel, both.both.left.Item1) &&
                ReferenceEquals(rightLabel, both.both.right.Item1) &&
                both.both.left.Item2 == 1 &&
                both.both.right.Item2 == 2,
                "Simultaneous shared tuple fields did not retain distinct values.");
        }

        private static void CheckUnknownBlock()
        {
            var block = new UnknownBlock { first = 17, marker = 9 };
            var value = UnknownBlockUnion.Block(block);
            if (value.kind != UnknownBlockUnion.Kind.Block)
                throw new InvalidOperationException("Unknown block kind was not selected.");
            Require(value.block.value.first == 17 && value.block.value.marker == 9,
                "Unknown-size unmanaged field did not round-trip.");
        }

        private static void CheckNestedCommand()
        {
            var command = NestedOwner.Command.Run(3f);
            if (command.kind != NestedOwner.Command.Kind.Run)
                throw new InvalidOperationException("Nested command kind was not selected.");
            Require(command.kind == NestedOwner.Command.Kind.Run && command.run.speed == 3f,
                "Nested union sibling payload did not round-trip.");
        }

        private static void CheckSerializationHydrationState()
        {
            AssertOnlyPayloadField<PlainSerializationState, SerializedSerializationState>();
            AssertOnlyPayloadField<PlainReferenceState, SerializedReferenceState>();
            AssertOnlyPayloadField<PlainGenericState<int>, SerializedGenericState<int>>();

            var shape = SerializedShape.Move(new Vector2(6f, 7f), 8f).__SpirePrepareForSerialization();
            InvokeAfterDeserialize(ref shape);
            var hydrated = shape.__SpireHydrateSerialized();
            var marker = GetSerializedPayload(hydrated);
            Require(marker is UnitySerializationPayloadBase,
                "Hydration did not install the shared serialized-state marker.");
            if (hydrated.kind != SerializedShape.Kind.Move)
                throw new InvalidOperationException("Hydration changed the fielded payload variant.");
            Require(hydrated.move.destination == new Vector2(6f, 7f) && hydrated.move.speed == 8f,
                "Hydration did not retain the non-default fielded payload.");

            var generic = SerializedGeneric<int>.Value(42, "generic").__SpirePrepareForSerialization();
            InvokeAfterDeserialize(ref generic);
            var genericHydrated = generic.__SpireHydrateSerialized();
            var alias = SerializedAliasUnion.Current(99, "alias").__SpirePrepareForSerialization();
            InvokeAfterDeserialize(ref alias);
            var aliasHydrated = alias.__SpireHydrateSerialized();
            if (genericHydrated.kind != SerializedGeneric<int>.Kind.Value)
                throw new InvalidOperationException("Hydration changed the generic payload variant.");
            if (aliasHydrated.kind != SerializedAliasUnion.Kind.Current)
                throw new InvalidOperationException("Hydration changed the alias payload variant.");
            Require(ReferenceEquals(marker, GetSerializedPayload(generic)) &&
                ReferenceEquals(marker, GetSerializedPayload(alias)) &&
                genericHydrated.value.value == 42 && genericHydrated.value.label == "generic" &&
                aliasHydrated.current.amount == 99 && aliasHydrated.current.label == "alias",
                "Hydration did not reuse one shared marker across generic and ordinary unions.");

            var copied = hydrated;
            Require(ReferenceEquals(marker, GetSerializedPayload(copied)),
                "Copying a hydrated union did not preserve the shared marker reference.");

            var repeated = hydrated.__SpireHydrateSerialized();
            Require(repeated.kind == SerializedShape.Kind.Move &&
                ReferenceEquals(marker, GetSerializedPayload(repeated)),
                "Repeated explicit hydration reset an already hydrated union.");

            var prepared = hydrated.__SpirePrepareForSerialization();
            var preparedPayload = GetSerializedPayload(prepared);
            Require(preparedPayload is UnitySerializationPayloadBase &&
                !ReferenceEquals(marker, preparedPayload),
                "Player preparation did not replace the shared marker with an active payload wrapper.");

            var richGraph = new SerializedGraphNode { name = "hydration", value = 7 };
            richGraph.next = richGraph;
            var rich = SerializedShape.Rich(
                richGraph,
                null,
                new SerializedInlineHelper
                {
                    label = "nested",
                    command = SerializedNestedOwner.Command.Run(3.5f),
                    nested = SerializedShape.Move(Vector2.one, 1.25f),
                    callback = null!,
                },
                SerializedChildUnion.Command(SerializedNestedOwner.Command.Run(4.5f)),
                new[] { SerializedChildUnion.Empty(), SerializedChildUnion.Leaf(3, "leaf") },
                new List<SerializedChildUnion>
                {
                    SerializedChildUnion.Empty(),
                    SerializedChildUnion.Command(SerializedNestedOwner.Command.Stop()),
                });
            var richPrepared = rich.__SpirePrepareForSerialization();
            var richRestored = SerializedShape.__SpireFromSerialized(GetSerializedPayload(richPrepared));
            var richHydrated = richRestored.__SpireHydrateSerialized();
            var richHydratedAgain = richHydrated.__SpireHydrateSerialized();
            if (richHydratedAgain.kind != SerializedShape.Kind.Rich)
                throw new InvalidOperationException("Hydration changed the rich payload variant.");
            var nested = richHydratedAgain.rich;
            if (nested.helper.nested.kind != SerializedShape.Kind.Move)
                throw new InvalidOperationException("Hydration changed the nested helper variant.");
            if (nested.helper.command.kind != SerializedNestedOwner.Command.Kind.Run)
                throw new InvalidOperationException("Hydration changed the nested command variant.");
            Require(nested.helper.nested.kind == SerializedShape.Kind.Move &&
                nested.helper.nested.move.speed == 1.25f &&
                nested.helper.command.run.speed == 3.5f &&
                nested.children.Length == 2 &&
                nested.childList.Count == 2,
                "Repeated nested hydration reset composite, array, or list payloads.");
            Require(ReferenceEquals(richGraph, nested.graph) &&
                ReferenceEquals(marker, GetSerializedPayload(nested.helper.nested)),
                "Nested hydration did not preserve managed-reference identity and the shared marker.");

            var foreignPayload = GetSerializedPayload(
                SerializedGenericState<int>.Value(11).__SpirePrepareForSerialization());
            var invalid = WithSerializedPayload(hydrated, foreignPayload);
            InvokeAfterDeserialize(ref invalid);
            var invalidMarker = GetSerializedPayload(invalid);
            Require(invalid.kind == SerializedShape.Kind.Move &&
                ReferenceEquals(marker, invalidMarker),
                "A new invalid serialized payload did not fall back while retaining the shared marker.");
        }

        private static void AssertOnlyPayloadField<TPlain, TSerialized>()
        {
            var plainFields = InstanceFields(typeof(TPlain));
            var serializedFields = InstanceFields(typeof(TSerialized));
            var extras = serializedFields
                .Where(serialized => !plainFields.Any(plain => plain.Name == serialized.Name))
                .ToArray();
            Require(extras.Length == 1 && extras[0].Name == "_serializedPayload",
                typeof(TSerialized).Name + " added fields other than _serializedPayload: " +
                string.Join(", ", extras.Select(field => field.Name)));
            Require(!serializedFields.Any(field => field.Name == "__SpireSerializationHydrated"),
                typeof(TSerialized).Name + " still contains the per-instance hydration flag.");
        }

        private static void InvokeAfterDeserialize<T>(ref T value)
            where T : ISerializationCallbackReceiver
            => value.OnAfterDeserialize();

        private static FieldInfo[] InstanceFields(Type type)
            => type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(field => !field.IsStatic)
                .ToArray();

        private static object? GetSerializedPayload<T>(T value)
        {
            var field = typeof(T).GetField(
                "_serializedPayload",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field?.GetValue(value);
        }

        private static T WithSerializedPayload<T>(T value, object? payload)
        {
            var field = typeof(T).GetField(
                "_serializedPayload",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field is null)
                throw new InvalidOperationException(typeof(T).Name + " has no serialized payload field.");
            object boxed = value!;
            field.SetValue(boxed, payload);
            return (T)boxed;
        }

        private static string ResolveReportPath()
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length; index++)
            {
                if (string.Equals(arguments[index], ReportArgument, StringComparison.Ordinal) && index + 1 < arguments.Length)
                    return Path.GetFullPath(arguments[index + 1]);
                if (arguments[index].StartsWith(ReportArgument + "=", StringComparison.Ordinal))
                    return Path.GetFullPath(arguments[index].Substring(ReportArgument.Length + 1));
            }

            var logDirectory = Path.GetDirectoryName(Application.consoleLogPath);
            return string.IsNullOrEmpty(logDirectory)
                ? Path.Combine(Application.persistentDataPath, DefaultReportName)
                : Path.Combine(logDirectory, DefaultReportName);
        }

        private static void Require(bool condition, string message)
        {
            if (activeReport is not null)
                activeReport.checks++;
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static PlayerReport? activeReport;

        [Serializable]
        private sealed class PlayerReport
        {
            public string unityVersion = string.Empty;
            public string backend = string.Empty;
            public string status = string.Empty;
            public int checks;
            public List<string> failures = new();
            public bool debugBuild;
            public int processorBits;
            public bool genericUnionSerializationSupported;
            public string genericUnionSerializationApplicability = string.Empty;
        }
    }

}
