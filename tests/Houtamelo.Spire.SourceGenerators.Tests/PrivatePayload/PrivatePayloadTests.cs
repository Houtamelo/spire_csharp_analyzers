using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Houtamelo.Spire.SourceGenerators.Tests.Unity;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.PrivatePayload;

/// Contract tests for private payload storage and public getter-only access.
public sealed class PrivatePayloadTests
{
    [Fact]
    public void PayloadFieldsArePrivateAndGettersArePublic()
    {
        var result = Run(BasicSource(), out var compilation, out var diagnostics);
        AssertNoErrors(compilation, diagnostics);

        var assembly = EmitAndLoad(compilation);
        var unionType = assembly.GetType("Command", throwOnError: true)!;
        var payloadType = assembly.GetType("CommandTypes+Variants+Teleport", throwOnError: true)!;

        foreach (var name in new[] { "target", "event" })
        {
            var backing = payloadType.GetField(
                "_" + name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(backing);
            Assert.True(backing!.IsPrivate);
            Assert.Null(payloadType.GetField(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));

            var property = payloadType.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(property);
            Assert.True(property!.GetMethod!.IsPublic);
            Assert.Null(property.SetMethod);
        }

        var value = unionType.GetMethod("Teleport")!.Invoke(null, new object[] { 3.5f, 7 })!;
        var payload = unionType.GetProperty("teleport")!.GetValue(value)!;
        Assert.Equal(3.5f, payloadType.GetProperty("target")!.GetValue(payload));
        Assert.Equal(7, payloadType.GetProperty("event")!.GetValue(payload));
    }

    [Fact]
    public void PayloadTypesAreNonPartialIncludingFormerAliasesAndNestedTypes()
    {
        var result = Run(AliasAndNestedSource(), out var compilation, out var diagnostics);
        AssertNoErrors(compilation, diagnostics);

        foreach (var metadataName in new[]
        {
            "RenamedTypes+Variants+Current",
            "RenamedTypes+Variants+Old",
            "Host+CommandTypes+Variants+Run",
        })
        {
            var type = compilation.GetTypeByMetadataName(metadataName);
            Assert.NotNull(type);
            Assert.DoesNotContain(
                type!.DeclaringSyntaxReferences
                    .Select(reference => reference.GetSyntax())
                    .OfType<StructDeclarationSyntax>()
                    .SelectMany(structure => structure.Modifiers),
                modifier => modifier.IsKind(SyntaxKind.PartialKeyword));
        }
    }

    [Fact]
    public void ConstructorAndDeconstructUsePrivateBackingStorage()
    {
        var result = Run(BasicSource(), out var compilation, out var diagnostics);
        AssertNoErrors(compilation, diagnostics);

        var assembly = EmitAndLoad(compilation);
        var unionType = assembly.GetType("Command", throwOnError: true)!;
        var payloadType = assembly.GetType("CommandTypes+Variants+Teleport", throwOnError: true)!;
        var value = unionType.GetMethod("Teleport")!.Invoke(null, new object[] { 4.25f, 11 })!;
        var payload = unionType.GetProperty("teleport")!.GetValue(value)!;

        var deconstruct = payloadType.GetMethod("Deconstruct")!;
        var outputs = new object?[] { null, null };
        deconstruct.Invoke(payload, outputs);
        Assert.Equal(4.25f, outputs[0]);
        Assert.Equal(11, outputs[1]);
        Assert.Null(payloadType.GetProperty("target")!.SetMethod);
        Assert.Null(payloadType.GetProperty("event")!.SetMethod);
    }

    [Fact]
    public void PayloadMutationIsRejectedByTheGeneratedReadOnlyProperties()
    {
        var result = Run(
            BasicSource() + """
            public static class Mutator
            {
                public static void Change(CommandTypes.Variants.Teleport payload)
                {
                    payload.target = 9f;
                }
            }
            """,
            out var compilation,
            out var diagnostics);

        var errors = diagnostics
            .Concat(compilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.Contains(errors, diagnostic => diagnostic.Id == "CS0200");
    }

    [Fact]
    public void GenericAndNestedPayloadsExposePrivateBackingFields()
    {
        var result = Run(GenericAndNestedSource(), out var compilation, out var diagnostics);
        AssertNoErrors(compilation, diagnostics);

        var genericPayload = compilation.GetTypeByMetadataName("OptionTypes1+Variants+Some`1");
        Assert.NotNull(genericPayload);
        Assert.Contains(genericPayload!.GetMembers().OfType<IFieldSymbol>(), field =>
            field.Name == "_value" && field.DeclaredAccessibility == Accessibility.Private);
        Assert.Contains(genericPayload.GetMembers("value").OfType<IPropertySymbol>(), property =>
            property.DeclaredAccessibility == Accessibility.Public && property.SetMethod is null);

        var nestedPayload = compilation.GetTypeByMetadataName("Host+CommandTypes+Variants+Run");
        Assert.NotNull(nestedPayload);
        Assert.Contains(nestedPayload!.GetMembers().OfType<IFieldSymbol>(), field =>
            field.Name == "_count" && field.DeclaredAccessibility == Accessibility.Private);
        Assert.Contains(nestedPayload.GetMembers("count").OfType<IPropertySymbol>(), property =>
            property.DeclaredAccessibility == Accessibility.Public && property.SetMethod is null);
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void UnityPayloadBackingFieldsCarrySerializationAttributes()
    {
        var result = UnityGeneratorTestHarness.Run(UnitySource());
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var payload = result.OutputCompilation.GetTypeByMetadataName("CommandTypes+Variants+Run");
        Assert.NotNull(payload);
        var effect = payload!.GetMembers("_effect").OfType<IFieldSymbol>().Single();
        var count = payload.GetMembers("_count").OfType<IFieldSymbol>().Single();

        Assert.Equal(Accessibility.Private, effect.DeclaredAccessibility);
        Assert.Equal(Accessibility.Private, count.DeclaredAccessibility);
        Assert.True(HasAttribute(effect, "UnityEngine.SerializeReference"));
        Assert.False(HasAttribute(effect, "UnityEngine.SerializeField"));
        Assert.True(HasAttribute(count, "UnityEngine.SerializeField"));
        Assert.False(HasAttribute(count, "UnityEngine.SerializeReference"));
        Assert.Contains(payload.GetMembers("effect").OfType<IPropertySymbol>(), property =>
            property.SetMethod is null);
        Assert.Contains(payload.GetMembers("count").OfType<IPropertySymbol>(), property =>
            property.SetMethod is null);
    }

    private static GeneratorDriverRunResult Run(
        string source,
        out Compilation compilation,
        out System.Collections.Immutable.ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> diagnostics)
        => GeneratorTestHelper.RunGenerator(source, out compilation, out diagnostics, "private-payload-test.cs");

    private static void AssertNoErrors(
        Compilation compilation,
        IEnumerable<Microsoft.CodeAnalysis.Diagnostic> diagnostics)
    {
        var errors = diagnostics
            .Concat(compilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.Empty(errors);
    }

    private static Assembly EmitAndLoad(Compilation compilation)
    {
        using var stream = new System.IO.MemoryStream();
        var emit = compilation.Emit(stream);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
        return TestHarnessEnvironment.LoadAssembly(stream.ToArray());
    }

    private static bool HasAttribute(ISymbol symbol, string fullName)
        => symbol.GetAttributes().Any(attribute =>
            string.Equals(attribute.AttributeClass?.ToDisplayString(), fullName, StringComparison.Ordinal));

    private static string BasicSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct Command
        {
            [Variant] public static partial Command Teleport(float target, int @event);
        }
        """;

    private static string AliasAndNestedSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct Renamed
        {
            [Variant(FormerlySerializedAs = "Old")] public static partial Renamed Current(int value);
        }
        public static partial class Host
        {
            [DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command Run(int count);
            }
        }
        """;

    private static string GenericAndNestedSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct Option<T> where T : unmanaged
        {
            [Variant] public static partial Option<T> Some(T value);
        }
        public static partial class Host
        {
            [DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command Run(int count);
            }
        }
        """;

    private static string UnitySource() => """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        public sealed class Effect { public int value; }
        [Serializable, DiscriminatedUnion]
        public partial struct Command
        {
            [Variant] public static partial Command Run([UnitySerializeReference] Effect effect, int count);
        }
        """;
}
