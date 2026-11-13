using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Houtamelo.Spire;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Behavioral;

/// Boundary coverage for generated equality across declaration shapes.
public sealed class VariantPayloadEqualityBoundaryTests
{
    [Fact]
    public void ReadonlyUnion_EqualitySurfaceIsReadonlyAndRuntimeUsable()
    {
        var result = Generate(ReadonlySource());
        AssertNoErrors(result);

        var union = result.OutputCompilation.GetTypeByMetadataName("ImmutableValue")!;
        Assert.Contains(union.AllInterfaces, interfaceType =>
            interfaceType.OriginalDefinition.ToDisplayString() == "System.IEquatable<T>");

        var equals = union.GetMembers("Equals").OfType<IMethodSymbol>().ToArray();
        Assert.Contains(equals, method => method.Parameters.Length == 1 && method.Parameters[0].Type.Name == "ImmutableValue");
        Assert.Contains(equals, method => method.Parameters.Length == 1 && method.Parameters[0].Type.Name == "Value");
        Assert.All(equals, method => Assert.True(method.IsReadOnly));

        var assembly = EmitAndLoad(result.OutputCompilation);
        var type = assembly.GetType("ImmutableValue", throwOnError: true)!;
        var first = type.GetMethod("Value")!.Invoke(null, new object[] { 17 })!;
        var second = type.GetMethod("Value")!.Invoke(null, new object[] { 17 })!;
        Assert.True((bool)type.GetMethod("Equals", new[] { type })!.Invoke(first, new[] { second })!);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        var probe = assembly.GetType("ImmutableValueProbe", throwOnError: true)!;
        Assert.True((bool)probe.GetMethod("CompareIn")!.Invoke(null, new[] { first, second })!);
    }

    [Fact]
    public void GenericReferenceAndUnmanagedUnions_CompileClosedEquality()
    {
        var result = Generate(GenericSource());
        AssertNoErrors(result);

        var reference = result.OutputCompilation.GetTypeByMetadataName("Maybe`1")!;
        var unmanaged = result.OutputCompilation.GetTypeByMetadataName("NumberChoice`1")!;
        Assert.Contains(reference.AllInterfaces, type => type.Name == "IEquatable" &&
            type.TypeArguments[0].Name == "Maybe");
        Assert.Contains(unmanaged.AllInterfaces, type => type.Name == "IEquatable" &&
            type.TypeArguments[0].Name == "NumberChoice");

        var assembly = EmitAndLoad(result.OutputCompilation);
        var maybe = assembly.GetType("Maybe`1", throwOnError: true)!.MakeGenericType(typeof(string));
        var left = maybe.GetMethod("Some")!.Invoke(null, new object?[] { "value" })!;
        var right = maybe.GetMethod("Some")!.Invoke(null, new object?[] { "value" })!;
        Assert.True(left.Equals(right));

        var numbers = assembly.GetType("NumberChoice`1", throwOnError: true)!.MakeGenericType(typeof(int));
        var one = numbers.GetMethod("Some")!.Invoke(null, new object[] { 9 })!;
        var two = numbers.GetMethod("Some")!.Invoke(null, new object[] { 9 })!;
        Assert.True(one.Equals(two));
    }

    [Fact]
    public void UnconstrainedGenericNamedOther_ClosesOverReferenceAndValueTypes()
    {
        var result = Generate(UnconstrainedGenericSource());
        AssertNoErrors(result);

        var union = result.OutputCompilation.GetTypeByMetadataName("GenericOther`1")!;
        Assert.Contains(union.TypeParameters, parameter => parameter.Name == "other");
        Assert.Contains(union.AllInterfaces, type => type.Name == "IEquatable" &&
            type.TypeArguments[0].Name == "GenericOther");

        var assembly = EmitAndLoad(result.OutputCompilation);
        var probe = assembly.GetType("GenericOtherProbe", throwOnError: true)!;
        Assert.True((bool)probe.GetMethod("ReferenceValues")!.Invoke(null, null)!);
        Assert.True((bool)probe.GetMethod("ValueValues")!.Invoke(null, null)!);
    }

    [Fact]
    public void UnconstrainedGenericCollisionParameters_CompileTypedOperatorsAndHashing()
    {
        var result = Generate(GenericCollisionSource());
        AssertNoErrors(result);

        var assembly = EmitAndLoad(result.OutputCompilation);
        var probe = assembly.GetType("GenericCollisionProbe", throwOnError: true)!;
        Assert.True((bool)probe.GetMethod("ReferenceValues")!.Invoke(null, null)!);
        Assert.True((bool)probe.GetMethod("ValueValues")!.Invoke(null, null)!);
    }

    [Fact]
    public void TypedStorage_NullabilityEquivalentListsAndTuplesCompileWarningFree()
    {
        var result = Generate(NullableStorageSource());
        AssertNoErrors(result);

        var assembly = EmitAndLoad(result.OutputCompilation);
        var probe = assembly.GetType("NullableStorageProbe", throwOnError: true)!;
        Assert.True((bool)probe.GetMethod("Check")!.Invoke(null, null)!);
    }

    [Fact]
    public void SharedStorage_NullableReferenceAndDistinctReferenceVariantsCompareWarningFree()
    {
        var result = Generate(SharedNullableSource());
        AssertNoErrors(result);

        var assembly = EmitAndLoad(result.OutputCompilation);
        var probe = assembly.GetType("SharedNullableProbe", throwOnError: true)!;
        Assert.True((bool)probe.GetMethod("Check")!.Invoke(null, null)!);
    }

    [Fact]
    public void NestedEscapedUnion_CompilesQualifiedEqualityMembers()
    {
        var result = Generate(NestedEscapedSource());
        AssertNoErrors(result);

        var union = result.OutputCompilation.GetTypeByMetadataName("Host+class");
        Assert.NotNull(union);
        var payload = result.OutputCompilation.GetTypeByMetadataName("Host+classTypes+Variants+LeftCase");
        Assert.NotNull(payload);
        Assert.Contains(union!.GetMembers("op_Equality").OfType<IMethodSymbol>(), method =>
            method.Parameters.Length == 2 && method.Parameters[0].Type.Name == "class");
    }

    [Fact]
    public void MoreThanEightVariants_EqualityMembersRemainComplete()
    {
        var result = Generate(ManyVariantsSource());
        AssertNoErrors(result);

        var union = result.OutputCompilation.GetTypeByMetadataName("ManyChoices")!;
        var payloadInterfaces = union.AllInterfaces
            .Where(type => type.OriginalDefinition.ToDisplayString() == "System.IEquatable<T>")
            .ToArray();
        Assert.Equal(11, payloadInterfaces.Length);
        Assert.Equal(12, union.GetMembers("Equals").OfType<IMethodSymbol>()
            .Count(method => method.Parameters.Length == 1));
    }

    [Fact]
    public void RefStruct_ReceivesNoUnsupportedEqualitySurface()
    {
        foreach (var languageVersion in new[] { LanguageVersion.CSharp9, LanguageVersion.CSharp12 })
        {
            var result = Generate(RefStructSource(), languageVersion);
            AssertNoErrors(result);
            var union = result.OutputCompilation.GetTypeByMetadataName("RefValue")!;
            Assert.DoesNotContain(union.AllInterfaces, type => type.Name == "IEquatable");
            Assert.Empty(union.GetMembers("op_Equality"));
            Assert.Empty(union.GetMembers("op_Inequality"));
        }
    }

    [Fact]
    public void OrdinaryUnion_EqualityGenerationCompilesWithCSharp9()
    {
        var result = Generate(ReadonlySource(), LanguageVersion.CSharp9);
        AssertNoErrors(result);
        var union = result.OutputCompilation.GetTypeByMetadataName("ImmutableValue")!;
        Assert.Contains(union.GetMembers("op_Equality").OfType<IMethodSymbol>(), method => method.Parameters.Length == 2);
    }

    [Fact]
    public void RecordUnion_KeepsCompilerRecordEqualityWithoutPayloadSurface()
    {
        var result = Generate(RecordSource());
        AssertNoErrors(result);

        var union = result.OutputCompilation.GetTypeByMetadataName("Outcome")!;
        Assert.Contains(union.AllInterfaces, type => type.Name == "IEquatable" && type.TypeArguments[0].Name == "Outcome");
        Assert.DoesNotContain(result.OutputCompilation.Assembly.GlobalNamespace.GetTypeMembers(), type =>
            type.Name == "OutcomeTypes");

        var assembly = EmitAndLoad(result.OutputCompilation);
        var success = assembly.GetType("Outcome+Success", throwOnError: true)!;
        var a = Activator.CreateInstance(success, new object[] { 4 })!;
        var b = Activator.CreateInstance(success, new object[] { 4 })!;
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void UserDeclaredInstanceFields_AreExcludedFromUnionEquality()
    {
        var result = Generate(ExtraFieldSource());
        AssertNoErrors(result);

        var assembly = EmitAndLoad(result.OutputCompilation);
        var union = assembly.GetType("ExtraState", throwOnError: true)!;
        var probe = assembly.GetType("ExtraStateProbe", throwOnError: true)!;
        var first = union.GetMethod("First")!.Invoke(null, new object[] { 42 })!;
        var second = union.GetMethod("First")!.Invoke(null, new object[] { 42 })!;
        var set = probe.GetMethod("Set")!;
        var arguments = new object[] { second, 99 };
        set.Invoke(null, arguments);
        second = arguments[0];

        Assert.True(first.Equals(second));
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    private static GeneratedResult Generate(string source, LanguageVersion languageVersion = LanguageVersion.Preview)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(languageVersion);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions, path: "equality-boundary.cs");
        var references = TestHarnessEnvironment.RuntimeReferences.ToList();
        references.Add(MetadataReference.CreateFromFile(typeof(EnforceInitializationAttribute).Assembly.Location));
        var compatibilityTree = TestHarnessEnvironment.CreateRuntimeCompatibilityTree(parseOptions, references);
        var trees = compatibilityTree is null ? new[] { syntaxTree } : new[] { syntaxTree, compatibilityTree };
        var compilation = CSharpCompilation.Create(
            "EqualityBoundary_" + Guid.NewGuid().ToString("N"),
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true,
                nullableContextOptions: NullableContextOptions.Enable));
        var driver = CSharpGeneratorDriver.Create(
            new[] { new DiscriminatedUnionGenerator().AsSourceGenerator() },
            parseOptions: parseOptions);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);
        return new GeneratedResult(outputCompilation, diagnostics);
    }

    private static void AssertNoErrors(GeneratedResult result)
    {
        var generatorErrors = result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning).ToArray();
        Assert.True(generatorErrors.Length == 0, string.Join("\n", generatorErrors.Select(d => d.ToString())));
        var errors = result.OutputCompilation.GetDiagnostics()
            .Where(d => d.Severity >= DiagnosticSeverity.Warning)
            .ToArray();
        Assert.True(errors.Length == 0, string.Join("\n", errors.Select(d => d.ToString())));
    }

    private static Assembly EmitAndLoad(Compilation compilation)
    {
        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
        return TestHarnessEnvironment.LoadAssembly(stream.ToArray());
    }

    private sealed record GeneratedResult(
        Compilation OutputCompilation,
        ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> Diagnostics);

    private static string ReadonlySource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public readonly partial struct ImmutableValue
        {
            [Variant] public static partial ImmutableValue Value(int value);
        }
        public static class ImmutableValueProbe
        {
            public static bool CompareIn(in ImmutableValue left, in ImmutableValue right) => left.Equals(right);
        }
        """;

    private static string GenericSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct Maybe<T> where T : class
        {
            [Variant] public static partial Maybe<T> Some(T? value);
            [Variant] public static partial Maybe<T> None();
        }
        [DiscriminatedUnion]
        public partial struct NumberChoice<T> where T : unmanaged
        {
            [Variant] public static partial NumberChoice<T> Some(T value);
            [Variant] public static partial NumberChoice<T> None();
        }
        """;

    private static string UnconstrainedGenericSource() => """
        using System;
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct GenericOther<other>
        {
            [Variant] public static partial GenericOther<other> Some(other value);
            [Variant] public static partial GenericOther<other> None();
        }
        public static class GenericOtherProbe
        {
            public static bool ReferenceValues()
            {
                var left = GenericOther<string>.Some("same");
                var right = GenericOther<string>.Some("same");
                var different = GenericOther<string>.Some("different");
                return left.Equals(right) && left.GetHashCode() == right.GetHashCode() && !left.Equals(different);
            }
            public static bool ValueValues()
            {
                var left = GenericOther<int>.Some(7);
                var right = GenericOther<int>.Some(7);
                var different = GenericOther<int>.Some(8);
                return left.Equals(right) && left.GetHashCode() == right.GetHashCode() && !left.Equals(different);
            }
        }
        """;

    private static string GenericCollisionSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct GenericCollision<other, obj, hash, left, right>
        {
            [Variant] public static partial GenericCollision<other, obj, hash, left, right> Value(
                other a, obj b, hash c, left d, right e);
            [Variant] public static partial GenericCollision<other, obj, hash, left, right> Empty();
        }
        public static class GenericCollisionProbe
        {
            public static bool ReferenceValues()
            {
                var left = GenericCollision<string, string, string, string, string>.Value("a", "b", "c", "d", "e");
                var right = GenericCollision<string, string, string, string, string>.Value("a", "b", "c", "d", "e");
                var different = GenericCollision<string, string, string, string, string>.Value("a", "b", "c", "d", "x");
                return left.Equals(right) && left == right && !(left != right) &&
                    left.GetHashCode() == right.GetHashCode() && left != different;
            }
            public static bool ValueValues()
            {
                var left = GenericCollision<int, int, int, int, int>.Value(1, 2, 3, 4, 5);
                var right = GenericCollision<int, int, int, int, int>.Value(1, 2, 3, 4, 5);
                var different = GenericCollision<int, int, int, int, int>.Value(1, 2, 3, 4, 6);
                return left.Equals(right) && left == right && !(left != right) &&
                    left.GetHashCode() == right.GetHashCode() && left != different;
            }
        }
        """;

    private static string NullableStorageSource() => """
        #nullable enable
        using System.Collections.Generic;
        using Houtamelo.Spire;
        [DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Typed)]
        public partial struct NullableStorage
        {
            [Variant] public static partial NullableStorage Texts(List<string> values);
            [Variant] public static partial NullableStorage NullableTexts(List<string?> values);
            [Variant] public static partial NullableStorage Pair((string value, int count) pair);
            [Variant] public static partial NullableStorage NullablePair((string? value, int count) pair);
        }
        public static class NullableStorageProbe
        {
            public static bool Check()
            {
                var textList = new List<string> { "text" };
                var nullableList = new List<string?> { null };
                var textLeft = NullableStorage.Texts(textList);
                var textRight = NullableStorage.Texts(textList);
                var nullableLeft = NullableStorage.NullableTexts(nullableList);
                var nullableRight = NullableStorage.NullableTexts(nullableList);
                var nullLeft = NullableStorage.NullableTexts(null!);
                var nullRight = NullableStorage.NullableTexts(null!);
                var pairLeft = NullableStorage.Pair(("pair", 1));
                var pairRight = NullableStorage.Pair(("pair", 1));
                var nullablePairLeft = NullableStorage.NullablePair((null, 2));
                var nullablePairRight = NullableStorage.NullablePair((null, 2));
                return textLeft == textRight &&
                    textLeft.GetHashCode() == textRight.GetHashCode() &&
                    nullableLeft == nullableRight &&
                    nullableLeft.GetHashCode() == nullableRight.GetHashCode() &&
                    nullLeft.Equals(nullRight) &&
                    pairLeft == pairRight &&
                    pairLeft.GetHashCode() == pairRight.GetHashCode() &&
                    nullablePairLeft == nullablePairRight &&
                    nullablePairLeft.GetHashCode() == nullablePairRight.GetHashCode();
            }
        }
        """;

    private static string SharedNullableSource() => """
        #nullable enable
        using Houtamelo.Spire;
        [DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Shared)]
        public partial struct SharedNullable
        {
            [Variant] public static partial SharedNullable Text(string? text);
            [Variant] public static partial SharedNullable Name(string name);
            [Variant] public static partial SharedNullable Object(object value);
        }
        public static class SharedNullableProbe
        {
            public static bool Check()
            {
                var nullLeft = SharedNullable.Text(null);
                var nullRight = SharedNullable.Text(null);
                var textLeft = SharedNullable.Text("text");
                var textRight = SharedNullable.Text("text");
                var nameLeft = SharedNullable.Name("name");
                var nameRight = SharedNullable.Name("name");
                var objectValue = new object();
                var objectLeft = SharedNullable.Object(objectValue);
                var objectRight = SharedNullable.Object(objectValue);
                return nullLeft == nullRight &&
                    nullLeft.GetHashCode() == nullRight.GetHashCode() &&
                    textLeft == textRight &&
                    textLeft.GetHashCode() == textRight.GetHashCode() &&
                    nameLeft.Equals(nameRight) &&
                    nameLeft.GetHashCode() == nameRight.GetHashCode() &&
                    objectLeft == objectRight &&
                    objectLeft.GetHashCode() == objectRight.GetHashCode();
            }
        }
        """;

    private static string NestedEscapedSource() => """
        using Houtamelo.Spire;
        public static partial class Host
        {
            [DiscriminatedUnion]
            public partial struct @class
            {
                [Variant] public static partial @class LeftCase(int left, string @right, object obj, int hash);
                [Variant] public static partial @class HashCase();
            }
        }
        """;

    private static string ManyVariantsSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct ManyChoices
        {
            [Variant] public static partial ManyChoices A();
            [Variant] public static partial ManyChoices B(int value);
            [Variant] public static partial ManyChoices C(int value);
            [Variant] public static partial ManyChoices D(int value);
            [Variant] public static partial ManyChoices E(int value);
            [Variant] public static partial ManyChoices F(int value);
            [Variant] public static partial ManyChoices G(int value);
            [Variant] public static partial ManyChoices H(int value);
            [Variant] public static partial ManyChoices I(int value);
            [Variant] public static partial ManyChoices J(int value);
        }
        """;

    private static string RefStructSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public ref partial struct RefValue
        {
            [Variant] public static partial RefValue Value(int value);
        }
        """;

    private static string RecordSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public abstract partial record Outcome
        {
            public sealed partial record Success(int value) : Outcome;
            public sealed partial record Failure(string text) : Outcome;
        }
        """;

    private static string ExtraFieldSource() => """
        using Houtamelo.Spire;
        // The generated partial union contributes backing fields; this fixture intentionally
        // adds a user field, so C# cannot define a cross-partial field order (CS0282).
        #pragma warning disable CS0282
        [DiscriminatedUnion]
        public partial struct ExtraState
        {
            public int UserData;
            [Variant] public static partial ExtraState First(int value);
            [Variant] public static partial ExtraState Second();
        }
        #pragma warning restore CS0282
        public static class ExtraStateProbe
        {
            public static void Set(ref ExtraState value, int userData) => value.UserData = userData;
        }
        """;
}
