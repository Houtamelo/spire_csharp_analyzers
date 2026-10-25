using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Houtamelo.Spire.SourceGenerators.Tests.Unity;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.GroupedUnionTypes;

public sealed class GroupedUnionTypesTests
{
    private const BindingFlags AllMembers =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    [Fact]
    public void PublicUnionGroupsVariantsAndFieldsUnderTheTypesContainer()
    {
        var generated = Generate(PublicSource());
        AssertNoErrors(generated);

        var source = generated.Source;
        Assert.Contains("public static class CommandTypes", source, StringComparison.Ordinal);
        Assert.Contains("public static class Variants", source, StringComparison.Ordinal);
        Assert.Contains("internal static class Fields", source, StringComparison.Ordinal);
        Assert.Contains("internal struct Managed", source, StringComparison.Ordinal);
        Assert.Contains("internal struct Unmanaged", source, StringComparison.Ordinal);
        Assert.Contains("internal struct Move", source, StringComparison.Ordinal);
        Assert.DoesNotContain("struct CommandMove", source, StringComparison.Ordinal);
        Assert.Contains("using Fields = global::CommandTypes.Fields;", source, StringComparison.Ordinal);
        Assert.Contains("using Variants = global::CommandTypes.Variants;", source, StringComparison.Ordinal);
        Assert.Contains("public readonly void Deconstruct() {}", source, StringComparison.Ordinal);

        var assembly = EmitAndLoad(generated.OutputCompilation);
        var types = assembly.GetType("CommandTypes", throwOnError: true)!;
        var variants = assembly.GetType("CommandTypes+Variants", throwOnError: true)!;
        var move = assembly.GetType("CommandTypes+Variants+Move", throwOnError: true)!;
        var former = assembly.GetType("CommandTypes+Variants+Old", throwOnError: true)!;
        var fields = assembly.GetType("CommandTypes+Fields", throwOnError: true)!;
        var managed = assembly.GetType("CommandTypes+Fields+Managed", throwOnError: true)!;
        var unmanaged = assembly.GetType("CommandTypes+Fields+Unmanaged", throwOnError: true)!;
        var unmanagedMove = assembly.GetType("CommandTypes+Fields+Unmanaged+Move", throwOnError: true)!;

        Assert.True(types.IsAbstract && types.IsSealed);
        Assert.True(variants.IsNestedPublic);
        Assert.True(move.IsNestedPublic);
        Assert.True(former.IsNestedPublic);
        Assert.True(fields.IsNestedAssembly);
        Assert.True(managed.IsNestedAssembly);
        Assert.True(unmanaged.IsNestedAssembly);
        Assert.True(unmanagedMove.IsNestedAssembly);
        Assert.Contains(unmanaged.GetFields(AllMembers), field => field.Name == "__move");
        Assert.Equal(typeof(int), unmanagedMove.GetField("destination", AllMembers)!.FieldType);
        Assert.Equal(typeof(float), unmanagedMove.GetField("speed", AllMembers)!.FieldType);

        var union = assembly.GetType("Command", throwOnError: true)!;
        var value = union.GetMethod("Move", AllMembers)!.Invoke(null, new object[] { 7, 2.5f })!;
        var payload = union.GetProperty("move", AllMembers)!.GetValue(value)!;
        Assert.Equal(move, payload.GetType());
        Assert.Equal(7, move.GetProperty("destination", AllMembers)!.GetValue(payload));
        Assert.Equal(2.5f, move.GetProperty("speed", AllMembers)!.GetValue(payload));
    }

    [Fact]
    public void GenericUnionUsesAritySuffixedNongenericContainersAndCopiesConstraints()
    {
        var generated = Generate(
            """
            #nullable enable
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Command<T> where T : class, new()
            {
                [Variant] public static partial Command<T> Value(T value);
                [Variant] public static partial Command<T> Count(int count);
            }
            """);
        AssertNoErrors(generated);

        Assert.Contains("public static class CommandTypes1", generated.Source, StringComparison.Ordinal);
        Assert.DoesNotContain("CommandTypes<T>", generated.Source, StringComparison.Ordinal);
        Assert.Contains("readonly T _s", generated.Source, StringComparison.Ordinal);
        Assert.Contains("Fields.Unmanaged.Count", generated.Source, StringComparison.Ordinal);

        var assembly = EmitAndLoad(generated.OutputCompilation);
        var types = assembly.GetType("CommandTypes1", throwOnError: true)!;
        var value = assembly.GetType("CommandTypes1+Variants+Value`1", throwOnError: true)!;
        var count = assembly.GetType("CommandTypes1+Fields+Unmanaged+Count", throwOnError: true)!;
        Assert.Empty(types.GetGenericArguments());
        var parameter = Assert.Single(value.GetGenericArguments());
        Assert.True(parameter.GenericParameterAttributes.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint));
        Assert.True(parameter.GenericParameterAttributes.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint));
        Assert.Equal(typeof(int), count.GetField("count", AllMembers)!.FieldType);

        var union = assembly.GetType("Command`1", throwOnError: true)!.MakeGenericType(typeof(StringBuilder));
        var text = new StringBuilder("payload");
        var valueResult = union.GetMethod("Value", AllMembers)!.Invoke(null, new object[] { text })!;
        var valuePayload = union.GetProperty("value", AllMembers)!.GetValue(valueResult)!;
        Assert.Same(text, valuePayload.GetType().GetProperty("value", AllMembers)!.GetValue(valuePayload));
    }

    [Fact]
    public void GenericAndNongenericUnionsWithTheSameBaseNameGetDistinctContainers()
    {
        var generated = Generate(
            """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command Idle();
            }

            [DiscriminatedUnion]
            public partial struct Command<T> where T : unmanaged
            {
                [Variant] public static partial Command<T> Value(T value);
            }
            """);
        AssertNoErrors(generated);

        var assembly = EmitAndLoad(generated.OutputCompilation);
        Assert.NotNull(assembly.GetType("CommandTypes", throwOnError: true));
        Assert.NotNull(assembly.GetType("CommandTypes1", throwOnError: true));
        Assert.NotNull(assembly.GetType("CommandTypes+Variants+Idle", throwOnError: true));
        Assert.NotNull(assembly.GetType("CommandTypes1+Variants+Value`1", throwOnError: true));
    }

    [Fact]
    public void ReservedUnmanagedHelperNamesAreDisambiguatedWithoutChangingPublicPayloadNames()
    {
        foreach (var (unionName, unmanagedFirst) in new[]
        {
            ("ReservedNamesUnmanagedFirst", true),
            ("ReservedNamesUnmanagedVariantFirst", false),
        })
        {
            var generated = Generate($$"""
                using Houtamelo.Spire;

                [DiscriminatedUnion]
                public partial struct {{unionName}}
                {
                    [Variant] public static partial {{unionName}} {{(unmanagedFirst ? "Unmanaged" : "UnmanagedVariant")}}({{(unmanagedFirst ? "int" : "float")}} value);
                    [Variant] public static partial {{unionName}} {{(unmanagedFirst ? "UnmanagedVariant" : "Unmanaged")}}({{(unmanagedFirst ? "float" : "int")}} value);
                }
                """);
            AssertNoErrors(generated);

            var assembly = EmitAndLoad(generated.OutputCompilation);
            var union = assembly.GetType(unionName, throwOnError: true)!;
            var firstHelper = assembly.GetType(
                unionName + "Types+Fields+Unmanaged+UnmanagedVariant",
                throwOnError: true)!;
            var secondHelper = assembly.GetType(
                unionName + "Types+Fields+Unmanaged+UnmanagedVariant2",
                throwOnError: true)!;
            Assert.Equal(typeof(float), firstHelper.GetField("value", AllMembers)!.FieldType);
            Assert.Equal(typeof(int), secondHelper.GetField("value", AllMembers)!.FieldType);

            var unmanagedPayloadType = assembly.GetType(
                unionName + "Types+Variants+Unmanaged",
                throwOnError: true)!;
            var unmanagedVariantPayloadType = assembly.GetType(
                unionName + "Types+Variants+UnmanagedVariant",
                throwOnError: true)!;

            var unmanaged = union.GetMethod("Unmanaged", AllMembers)!.Invoke(null, new object[] { 7 })!;
            var unmanagedPayload = union.GetProperty("unmanaged", AllMembers)!.GetValue(unmanaged)!;
            Assert.Equal(unmanagedPayloadType, unmanagedPayload.GetType());
            Assert.Equal(7, unmanagedPayloadType.GetProperty("value", AllMembers)!.GetValue(unmanagedPayload));

            var unmanagedVariant = union.GetMethod("UnmanagedVariant", AllMembers)!.Invoke(null, new object[] { 2.5f })!;
            var unmanagedVariantPayload = union.GetProperty("unmanagedVariant", AllMembers)!.GetValue(unmanagedVariant)!;
            Assert.Equal(unmanagedVariantPayloadType, unmanagedVariantPayload.GetType());
            Assert.Equal(2.5f, unmanagedVariantPayloadType.GetProperty("value", AllMembers)!.GetValue(unmanagedVariantPayload));
        }
    }

    [Fact]
    public void AccessibilityAndNestedPlacementArePreserved()
    {
        var generated = Generate(
            """
            using Houtamelo.Spire;

            public partial class Host
            {
                [DiscriminatedUnion]
                private partial struct PrivateCommand
                {
                    [Variant] public static partial PrivateCommand Run(int value);
                }

                [DiscriminatedUnion]
                internal partial struct InternalCommand
                {
                    [Variant] public static partial InternalCommand Run(int value);
                }
            }

            [DiscriminatedUnion]
            internal partial struct InternalTop
            {
                [Variant] public static partial InternalTop Run(int value);
            }
            """);
        AssertNoErrors(generated);

        var assembly = EmitAndLoad(generated.OutputCompilation);
        var privateTypes = assembly.GetType("Host+PrivateCommandTypes", throwOnError: true)!;
        var privateVariants = assembly.GetType("Host+PrivateCommandTypes+Variants", throwOnError: true)!;
        var privatePayload = assembly.GetType("Host+PrivateCommandTypes+Variants+Run", throwOnError: true)!;
        var internalTypes = assembly.GetType("Host+InternalCommandTypes", throwOnError: true)!;
        var internalPayload = assembly.GetType("Host+InternalCommandTypes+Variants+Run", throwOnError: true)!;
        var internalTopTypes = assembly.GetType("InternalTopTypes", throwOnError: true)!;

        Assert.True(privateTypes.IsNestedPrivate);
        Assert.True(privateVariants.IsNestedPublic);
        Assert.True(privatePayload.IsNestedPublic);
        Assert.True(internalTypes.IsNestedAssembly);
        Assert.True(internalPayload.IsNestedPublic);
        Assert.True(internalTopTypes.IsNotPublic);

        var privateTree = generated.RunResult.GeneratedTrees.Single(tree =>
            tree.GetText().ToString().Contains("PrivateCommandTypes", StringComparison.Ordinal));
        Assert.DoesNotContain("using Fields = global::Host.PrivateCommandTypes.Fields;", privateTree.GetText().ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("using Variants = global::Host.PrivateCommandTypes.Variants;", privateTree.GetText().ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void AliasCollisionsKeywordsAndFormerVariantNamesRemainUsable()
    {
        var generated = Generate(
            """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Fields
            {
                [Variant(FormerlySerializedAs = "Old")]
                public static partial Fields @event(int @class);
            }
            """);
        AssertNoErrors(generated);

        var unionTree = generated.RunResult.GeneratedTrees.Single(tree =>
            tree.GetText().ToString().Contains("struct Fields", StringComparison.Ordinal));
        var source = unionTree.GetText().ToString();
        Assert.DoesNotContain("using Fields =", source, StringComparison.Ordinal);
        Assert.Contains("struct @event", source, StringComparison.Ordinal);
        Assert.Contains("using Variants =", source, StringComparison.Ordinal);

        var assembly = EmitAndLoad(generated.OutputCompilation);
        var union = assembly.GetType("Fields", throwOnError: true)!;
        var payload = assembly.GetType("FieldsTypes+Variants+event", throwOnError: true)!;
        var former = assembly.GetType("FieldsTypes+Variants+Old", throwOnError: true)!;
        var value = union.GetMethod("event", AllMembers)!.Invoke(null, new object[] { 3 })!;
        var result = union.GetProperty("event_value", AllMembers)!.GetValue(value)!;
        Assert.Equal(payload, result.GetType());
        Assert.Equal(3, payload.GetProperty("class", AllMembers)!.GetValue(result));
        Assert.NotNull(former.GetMethod("Deconstruct", AllMembers));
    }

    [Fact]
    public void UnityReferencePayloadUsesOnlySerializeReferenceAndKeepsFieldGetterAdjacent()
    {
        var result = UnityGeneratorTestHarness.Run(
            """
            using System;
            using Houtamelo.Spire;

            [Serializable, DiscriminatedUnion]
            public partial struct ReferenceCommand
            {
                [Variant] public static partial ReferenceCommand Ref([UnitySerializeReference] Node? node);
                [Variant] public static partial ReferenceCommand Idle();
            }

            [Serializable]
            public sealed class Node { }
            """);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var tree = result.RunResult.GeneratedTrees.Single(generatedTree =>
            generatedTree.GetText().ToString().Contains("struct Ref", StringComparison.Ordinal));
        var payload = tree.GetRoot().DescendantNodes()
            .OfType<StructDeclarationSyntax>()
            .Single(type => type.Identifier.ValueText == "Ref");
        var field = payload.Members.OfType<FieldDeclarationSyntax>()
            .Single(candidate => candidate.Declaration.Variables.Single().Identifier.ValueText == "_node");
        var names = field.AttributeLists
            .SelectMany(attributes => attributes.Attributes)
            .Select(attribute => attribute.Name.ToString().TrimStart('@'))
            .ToArray();
        Assert.Equal(new[] { "SerializeReference" }, names);
        var getter = payload.Members.OfType<PropertyDeclarationSyntax>()
            .Single(property => property.Identifier.ValueText == "node");
        Assert.Equal(
            field.GetLocation().GetLineSpan().EndLinePosition.Line + 1,
            getter.GetLocation().GetLineSpan().StartLinePosition.Line);
        Assert.Contains("public readonly void Deconstruct() {}", result.GeneratedSource, StringComparison.Ordinal);
    }

    private static GeneratedResult Generate(string source)
    {
        var result = GeneratorTestHelper.RunGenerator(
            source,
            out var compilation,
            out var diagnostics,
            path: "grouped-union-types-test.cs");
        var runResult = result;
        var generatedSource = string.Join("\n", runResult.GeneratedTrees.Select(tree => tree.GetText().ToString()));
        return new GeneratedResult(compilation, diagnostics, generatedSource, runResult);
    }

    private static void AssertNoErrors(GeneratedResult generated)
    {
        var diagnostics = generated.Diagnostics
            .Concat(generated.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.Empty(diagnostics);
    }

    private static Assembly EmitAndLoad(Compilation compilation)
    {
        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
        return TestHarnessEnvironment.LoadAssembly(stream.ToArray());
    }

    private static string PublicSource() => """
        #nullable enable
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct Command
        {
            [Variant] public static partial Command Idle();
            [Variant(FormerlySerializedAs = "Old")] public static partial Command Move(int destination, float speed);
            [Variant] public static partial Command Speak(string text);
        }
        """;

    private sealed record GeneratedResult(
        Compilation OutputCompilation,
        ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> Diagnostics,
        string Source,
        GeneratorDriverRunResult RunResult);
}
