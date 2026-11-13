using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Behavioral;

/// Defines the equality surface and runtime semantics for generated struct unions.
/// The sources intentionally exercise the public API through normal C# binding,
/// then load the emitted assembly to avoid testing a hand-written substitute.
public sealed class VariantPayloadEqualityTests : BehavioralTestBase
{
    [Fact]
    public void GeneratedEqualitySurfaceMatchesUnionAndCanonicalPayloadContract()
    {
        var assembly = CompileAndLoad(EqualitySource);
        var union = assembly.GetType("EqualityUnion", throwOnError: true)!;

        var payloadTypes = new[]
        {
            assembly.GetType("EqualityUnionTypes+Variants+Number", throwOnError: true)!,
            assembly.GetType("EqualityUnionTypes+Variants+Text", throwOnError: true)!,
            assembly.GetType("EqualityUnionTypes+Variants+Empty", throwOnError: true)!,
            assembly.GetType("EqualityUnionTypes+Variants+Probe", throwOnError: true)!,
            assembly.GetType("EqualityUnionTypes+Variants+Array", throwOnError: true)!,
            assembly.GetType("EqualityUnionTypes+Variants+Double", throwOnError: true)!,
            assembly.GetType("EqualityUnionTypes+Variants+Wide", throwOnError: true)!,
            assembly.GetType("EqualityUnionTypes+Variants+Hidden", throwOnError: true)!,
        };

        var equatableInterfaces = union.GetInterfaces()
            .Where(type => type.IsGenericType &&
                type.GetGenericTypeDefinition() == typeof(IEquatable<>))
            .Select(type => type.GetGenericArguments()[0])
            .ToArray();
        Assert.Equal(
            payloadTypes.Append(union).OrderBy(type => type.FullName).Select(type => type.FullName),
            equatableInterfaces.OrderBy(type => type.FullName).Select(type => type.FullName));

        var typedEquals = DeclaredMethod(union, "Equals", union);
        var objectEquals = DeclaredMethod(union, "Equals", typeof(object));
        var hashCode = DeclaredMethod(union, "GetHashCode");
        Assert.NotNull(typedEquals);
        Assert.NotNull(objectEquals);
        Assert.NotNull(hashCode);
        Assert.Equal(typeof(object), objectEquals!.GetBaseDefinition().DeclaringType);
        Assert.Equal(typeof(object), hashCode!.GetBaseDefinition().DeclaringType);
        Assert.Null(union.GetNestedType("Hasher", BindingFlags.Public | BindingFlags.NonPublic));

        foreach (var payload in payloadTypes)
        {
            Assert.DoesNotContain(
                payload.GetInterfaces(),
                type => type.IsGenericType &&
                    type.GetGenericTypeDefinition() == typeof(IEquatable<>) &&
                    type.GetGenericArguments()[0] == union);
            Assert.Null(DeclaredMethod(payload, "Equals", union));
            Assert.Null(DeclaredMethod(payload, "op_Equality", payload, union));
        }

        foreach (var payload in payloadTypes)
        {
            foreach (var name in new[] { "op_Equality", "op_Inequality" })
            {
                Assert.NotNull(DeclaredMethod(union, name, union, union));
                Assert.NotNull(DeclaredMethod(union, name, union, payload));
                Assert.NotNull(DeclaredMethod(union, name, payload, union));
            }
        }

        var accessUnion = assembly.GetType("AccessUnion", throwOnError: true)!;
        var hiddenFactory = accessUnion.GetMethod("MakeHidden", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(hiddenFactory);
        var hidden = assembly.GetType("AccessUnionTypes+Variants+Hidden", throwOnError: true)!;
        Assert.Contains(
            accessUnion.GetInterfaces(),
            type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEquatable<>) &&
                type.GetGenericArguments()[0] == hidden);
    }

    [Fact]
    public void UnionEqualityUsesTagsFieldsAndDefaultComparer()
    {
        var assembly = CompileAndLoad(EqualitySource);
        var probe = assembly.GetType("EqualityProbe", throwOnError: true)!;

        Assert.True((bool)probe.GetMethod("SameNumber")!.Invoke(null, null)!);
        Assert.True((bool)probe.GetMethod("DifferentNumber")!.Invoke(null, null)!);
        Assert.True((bool)probe.GetMethod("DifferentTags")!.Invoke(null, null)!);
        Assert.True((bool)probe.GetMethod("Fieldless")!.Invoke(null, null)!);
        Assert.True((bool)probe.GetMethod("PayloadBothDirections")!.Invoke(null, null)!);
        Assert.True((bool)probe.GetMethod("PayloadTagMismatchAndFieldless")!.Invoke(null, null)!);
        Assert.True((bool)probe.GetMethod("ObjectAndCollections")!.Invoke(null, null)!);
        Assert.True((bool)probe.GetMethod("NullAndFloatingPoint")!.Invoke(null, null)!);
        Assert.True((bool)probe.GetMethod("ArrayReferenceSemantics")!.Invoke(null, null)!);
        Assert.True((bool)probe.GetMethod("CustomTypedComparer")!.Invoke(null, null)!);
        Assert.True((bool)probe.GetMethod("WideEachFieldDiffers")!.Invoke(null, null)!);
        Assert.True((bool)probe.GetMethod("ExtraUserFieldIsIgnored")!.Invoke(null, null)!);
    }

    [Fact]
    public void PrivateFactoryPayloadCanCompareWithoutExposingFactoryConversion()
    {
        var assembly = CompileAndLoad(EqualitySource);
        var union = assembly.GetType("AccessUnion", throwOnError: true)!;
        var payload = assembly.GetType("AccessUnionTypes+Variants+Hidden", throwOnError: true)!;
        var value = union.GetMethod("MakeHidden", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null)!;
        var payloadValue = Activator.CreateInstance(payload, new object[] { 7 })!;

        Assert.True((bool)DeclaredMethod(union, "Equals", payload)!.Invoke(value, new[] { payloadValue })!);
        var implicitPayloads = union.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == "op_Implicit")
            .Select(method => method.GetParameters().Single().ParameterType)
            .ToArray();
        Assert.DoesNotContain(payload, implicitPayloads);
    }

    [Fact]
    public void TypedSharedAndAutoStorageUseLogicalActiveFields()
    {
        var assembly = CompileAndLoad(StorageSource);
        var probe = assembly.GetType("StorageProbe", throwOnError: true)!;
        Assert.True((bool)probe.GetMethod("Typed")!.Invoke(null, null)!);
        Assert.True((bool)probe.GetMethod("Shared")!.Invoke(null, null)!);
        Assert.True((bool)probe.GetMethod("Auto")!.Invoke(null, null)!);
    }

    private static MethodInfo? DeclaredMethod(Type type, string name, params Type[] parameterTypes)
        => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .SingleOrDefault(method => method.Name == name &&
                method.GetParameters().Select(parameter => parameter.ParameterType)
                    .SequenceEqual(parameterTypes));

    private const string EqualitySource = """
        using System;
        using System.Collections.Generic;
        using Houtamelo.Spire;

        public readonly struct ComparisonToken : IEquatable<ComparisonToken>
        {
            public ComparisonToken(int value) => Value = value;
            public int Value { get; }
            public bool Equals(ComparisonToken other) => Value == other.Value;
            public override bool Equals(object? obj) => false;
            public override int GetHashCode() => Value;
        }

        [DiscriminatedUnion]
        public partial struct EqualityUnion
        {
            [Variant] public static partial EqualityUnion Number(int value);
            [Variant] public static partial EqualityUnion Text(string? value);
            [Variant] public static partial EqualityUnion Empty();
            [Variant] public static partial EqualityUnion Probe(ComparisonToken value);
            [Variant] public static partial EqualityUnion Array(int[] values);
            [Variant] public static partial EqualityUnion Double(double value);
            [Variant] public static partial EqualityUnion Wide(
                int first, int second, int third, int fourth, int fifth,
                int sixth, int seventh, int eighth, int ninth);
            [Variant] internal static partial EqualityUnion Hidden(int value);
        }

        [DiscriminatedUnion]
        public partial struct AccessUnion
        {
            [Variant] public static partial AccessUnion Visible(int value);
            [Variant] internal static partial AccessUnion Internal(int value);
            [Variant] private static partial AccessUnion Hidden(int value);
            public static AccessUnion MakeHidden() => Hidden(7);
        }

        public partial struct EqualityUnion
        {
            public int Extra;
        }

        public static class EqualityProbe
        {
            public static bool SameNumber()
            {
                var first = EqualityUnion.Number(4);
                var second = EqualityUnion.Number(4);
                return first.Equals(second) && first == second && second == first &&
                    !(first != second) && EqualityComparer<EqualityUnion>.Default.Equals(first, second) &&
                    first.GetHashCode() == second.GetHashCode();
            }

            public static bool DifferentNumber()
            {
                var first = EqualityUnion.Number(4);
                var second = EqualityUnion.Number(5);
                return !first.Equals(second) && first != second && second != first;
            }

            public static bool DifferentTags()
            {
                var number = EqualityUnion.Number(4);
                var text = EqualityUnion.Text("4");
                return !number.Equals(text) && number != text && text != number;
            }

            public static bool Fieldless()
            {
                var first = EqualityUnion.Empty();
                var second = EqualityUnion.Empty();
                var number = EqualityUnion.Number(0);
                return first == second && first.Equals(second) && first != number && number != first;
            }

            public static bool PayloadBothDirections()
            {
                var union = EqualityUnion.Number(7);
                var payload = new EqualityUnionTypes.Variants.Number(7);
                var different = new EqualityUnionTypes.Variants.Number(8);
                return union.Equals(payload) && union == payload && payload == union &&
                    !(union != payload) && !(payload != union) &&
                    !union.Equals(different) && union != different && different != union;
            }

            public static bool PayloadTagMismatchAndFieldless()
            {
                var number = EqualityUnion.Number(7);
                var textPayload = new EqualityUnionTypes.Variants.Text("7");
                var empty = EqualityUnion.Empty();
                var emptyPayload = new EqualityUnionTypes.Variants.Empty();
                return !number.Equals(textPayload) && number != textPayload && textPayload != number &&
                    empty.Equals(emptyPayload) && empty == emptyPayload && emptyPayload == empty &&
                    !(empty != emptyPayload) && !(emptyPayload != empty);
            }

            public static bool ObjectAndCollections()
            {
                var first = EqualityUnion.Number(7);
                var second = EqualityUnion.Number(7);
                object boxed = first;
                object boxedOther = second;
                object? nullValue = null;
                object otherUnion = AccessUnion.MakeHidden();
                var payload = new EqualityUnionTypes.Variants.Number(7);
                var set = new HashSet<EqualityUnion> { first };
                var dictionary = new Dictionary<EqualityUnion, string> { [first] = "found" };
                return object.Equals(boxed, boxedOther) && !object.Equals(boxed, nullValue) &&
                    !object.Equals(boxed, otherUnion) && !object.Equals(boxed, payload) &&
                    !first.Equals((object)payload) && set.Contains(second) &&
                    dictionary.TryGetValue(second, out var value) && value == "found";
            }

            public static bool NullAndFloatingPoint()
            {
                var first = EqualityUnion.Text(null);
                var second = EqualityUnion.Text(null);
                var text = EqualityUnion.Text("value");
                var nanFirst = EqualityUnion.Double(double.NaN);
                var nanSecond = EqualityUnion.Double(double.NaN);
                var positiveZero = EqualityUnion.Double(+0.0);
                var negativeZero = EqualityUnion.Double(-0.0);
                return first == second && first != text && nanFirst == nanSecond &&
                    positiveZero == negativeZero &&
                    first.GetHashCode() == second.GetHashCode() &&
                    nanFirst.GetHashCode() == nanSecond.GetHashCode() &&
                    positiveZero.GetHashCode() == negativeZero.GetHashCode();
            }

            public static bool ArrayReferenceSemantics()
            {
                var values = new[] { 1, 2 };
                var same = EqualityUnion.Array(values);
                var sameReference = EqualityUnion.Array(values);
                var differentReference = EqualityUnion.Array(new[] { 1, 2 });
                return same == sameReference && same != differentReference;
            }

            public static bool CustomTypedComparer()
            {
                var first = EqualityUnion.Probe(new ComparisonToken(3));
                var second = EqualityUnion.Probe(new ComparisonToken(3));
                return first == second && first.Equals(second) &&
                    first.GetHashCode() == second.GetHashCode();
            }

            public static bool WideEachFieldDiffers()
            {
                var baseline = EqualityUnion.Wide(1, 2, 3, 4, 5, 6, 7, 8, 9);
                return !baseline.Equals(EqualityUnion.Wide(10, 2, 3, 4, 5, 6, 7, 8, 9)) &&
                    !baseline.Equals(EqualityUnion.Wide(1, 20, 3, 4, 5, 6, 7, 8, 9)) &&
                    !baseline.Equals(EqualityUnion.Wide(1, 2, 30, 4, 5, 6, 7, 8, 9)) &&
                    !baseline.Equals(EqualityUnion.Wide(1, 2, 3, 40, 5, 6, 7, 8, 9)) &&
                    !baseline.Equals(EqualityUnion.Wide(1, 2, 3, 4, 50, 6, 7, 8, 9)) &&
                    !baseline.Equals(EqualityUnion.Wide(1, 2, 3, 4, 5, 60, 7, 8, 9)) &&
                    !baseline.Equals(EqualityUnion.Wide(1, 2, 3, 4, 5, 6, 70, 8, 9)) &&
                    !baseline.Equals(EqualityUnion.Wide(1, 2, 3, 4, 5, 6, 7, 80, 9)) &&
                    !baseline.Equals(EqualityUnion.Wide(1, 2, 3, 4, 5, 6, 7, 8, 90));
            }

            public static bool ExtraUserFieldIsIgnored()
            {
                var first = EqualityUnion.Number(3);
                var second = EqualityUnion.Number(3);
                first.Extra = 10;
                second.Extra = 20;
                return first == second && first.GetHashCode() == second.GetHashCode();
            }
        }
        """;

    private const string StorageSource = """
        using System;
        using Houtamelo.Spire;

        public struct ManagedValue : IEquatable<ManagedValue>
        {
            public ManagedValue(string name, int count) { Name = name; Count = count; }
            public string Name;
            public int Count;
            public bool Equals(ManagedValue other) => Name == other.Name && Count == other.Count;
            public override bool Equals(object? obj) => obj is ManagedValue other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(Name, Count);
        }

        [DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Typed)]
        public partial struct TypedUnion
        {
            [Variant] public static partial TypedUnion Value(string text, int count);
            [Variant] public static partial TypedUnion Other(ManagedValue value);
        }

        [DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Shared)]
        public partial struct SharedUnion
        {
            [Variant] public static partial SharedUnion Text(string value);
            [Variant] public static partial SharedUnion Numbers(int[] value);
            [Variant] public static partial SharedUnion Object(object value);
        }

        [DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Auto)]
        public partial struct AutoUnion
        {
            [Variant] public static partial AutoUnion Value(string text, int count);
            [Variant] public static partial AutoUnion Other(int[] values);
            [Variant] public static partial AutoUnion Third(object value);
        }

        public static class StorageProbe
        {
            public static bool Typed()
            {
                var first = TypedUnion.Value("x", 2);
                var second = TypedUnion.Value("x", 2);
                var different = TypedUnion.Value("x", 3);
                var managedFirst = TypedUnion.Other(new ManagedValue("m", 3));
                var managedSecond = TypedUnion.Other(new ManagedValue("m", 3));
                var managedDifferent = TypedUnion.Other(new ManagedValue("m", 4));
                return first == second && first != different && managedFirst == managedSecond &&
                    managedFirst != managedDifferent &&
                    managedFirst.GetHashCode() == managedSecond.GetHashCode();
            }

            public static bool Shared()
            {
                var values = new[] { 1, 2 };
                var first = SharedUnion.Numbers(values);
                var second = SharedUnion.Numbers(values);
                var different = SharedUnion.Numbers(new[] { 1, 2 });
                var text = SharedUnion.Text("x");
                object marker = new object();
                var objectFirst = SharedUnion.Object(marker);
                var objectSecond = SharedUnion.Object(marker);
                var objectDifferent = SharedUnion.Object(new object());
                return first == second && first != different && first != text &&
                    objectFirst == objectSecond && objectFirst != objectDifferent;
            }

            public static bool Auto()
            {
                var values = new[] { 1, 2 };
                var first = AutoUnion.Other(values);
                var second = AutoUnion.Other(values);
                var different = AutoUnion.Other(new[] { 1, 2 });
                return first == second && first != different;
            }
        }
        """;
}
