using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.ReadableFactories;

public sealed class ReadableFactoryTests
{
    [Fact]
    public void MixedUnion_UsesReadableLocalsAndRenamedUnmanagedHelpers()
    {
        var generated = Generate(MixedUnionSource());
        AssertNoErrors(generated);

        var source = generated.Source;
        Assert.Contains("internal struct Unmanaged", source, StringComparison.Ordinal);
        Assert.Contains("internal struct Teleport", source, StringComparison.Ordinal);
        Assert.Contains("internal Teleport __teleport;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("UnmanagedData", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TeleportUnmanagedFields", source, StringComparison.Ordinal);
        Assert.DoesNotContain("__Teleport", source, StringComparison.Ordinal);
        Assert.Contains("__unmanaged.__teleport.target", source, StringComparison.Ordinal);
        Assert.Contains("__unmanaged.__teleport.duration", source, StringComparison.Ordinal);

        var teleport = Factory(source, "Teleport");
        var locals = LocalNames(teleport);
        Assert.Equal(
            new[] { "teleportFields", "unmanagedFields", "managedFields" },
            locals);

        var returned = Assert.IsType<ObjectCreationExpressionSyntax>(
            teleport.Body!.Statements.OfType<ReturnStatementSyntax>().Single().Expression);
        Assert.Equal("Kind.Teleport", returned.ArgumentList!.Arguments[0].Expression.ToString());
        Assert.Equal("unmanagedFields", returned.ArgumentList.Arguments[1].Expression.ToString());
        Assert.Equal("managedFields", returned.ArgumentList.Arguments[2].Expression.ToString());
        Assert.DoesNotContain(
            returned.ArgumentList.Arguments,
            argument => argument.Expression.DescendantNodesAndSelf()
                .OfType<ObjectCreationExpressionSyntax>().Any());

        Assert.DoesNotContain("\n\nreturn", teleport.Body!.ToFullString(), StringComparison.Ordinal);
    }

    [Fact]
    public void KindAndVariantFlagsAreReadonly()
    {
        var generated = Generate(MixedUnionSource());
        AssertNoErrors(generated);

        var root = CSharpSyntaxTree.ParseText(generated.Source).GetRoot();
        var kind = root.DescendantNodes()
            .OfType<PropertyDeclarationSyntax>()
            .Single(property => property.Identifier.ValueText == "kind");
        Assert.Contains(kind.Modifiers, modifier => modifier.IsKind(SyntaxKind.ReadOnlyKeyword));

        var flags = root.DescendantNodes()
            .OfType<PropertyDeclarationSyntax>()
            .Where(property => property.Identifier.ValueText.StartsWith("Is", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(3, flags.Length);
        Assert.All(flags, property =>
            Assert.Contains(property.Modifiers, modifier => modifier.IsKind(SyntaxKind.ReadOnlyKeyword)));
    }

    [Fact]
    public void ReadonlyAndRefStructUnionsUseReadonlyAccessorsAndRecordClassesDoNot()
    {
        foreach (var source in new[]
        {
            """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public readonly partial struct ReadonlyCommand
            {
                [Variant] public static partial ReadonlyCommand Idle();
                [Variant] public static partial ReadonlyCommand Move(int value);
            }
            """,
            """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public ref partial struct RefCommand
            {
                [Variant] public static partial RefCommand Idle();
                [Variant] public static partial RefCommand Move(int value);
            }
            """
        })
        {
            var generated = Generate(source);
            AssertNoErrors(generated);

            var root = CSharpSyntaxTree.ParseText(generated.Source).GetRoot();
            var properties = root.DescendantNodes()
                .OfType<PropertyDeclarationSyntax>()
                .Where(property => property.Identifier.ValueText == "kind" ||
                    property.Identifier.ValueText.StartsWith("Is", StringComparison.Ordinal))
                .ToArray();
            Assert.Equal(3, properties.Length);
            Assert.All(properties, property =>
                Assert.Contains(property.Modifiers, modifier => modifier.IsKind(SyntaxKind.ReadOnlyKeyword)));
        }

        var record = Generate(
            """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial record Shape
            {
                public partial record Circle : Shape;
                public partial record Square : Shape;
            }
            """);
        AssertNoErrors(record);

        var recordRoot = CSharpSyntaxTree.ParseText(record.Source).GetRoot();
        var recordFlags = recordRoot.DescendantNodes()
            .OfType<PropertyDeclarationSyntax>()
            .Where(property => property.Identifier.ValueText.StartsWith("Is", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, recordFlags.Length);
        Assert.All(recordFlags, property =>
            Assert.DoesNotContain(property.Modifiers, modifier => modifier.IsKind(SyntaxKind.ReadOnlyKeyword)));
    }

    [Fact]
    public void RecordGeneratedSourcesUseKAndROpeningBraces()
    {
        var result = GeneratorTestHelper.RunGenerator(
            """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial record Shape
            {
                public partial record Circle : Shape;
                public partial record Square : Shape;
            }
            """,
            out var compilation,
            out var diagnostics);
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        foreach (var tree in result.GeneratedTrees)
        {
            var standaloneOpeningBraces = tree.GetText()
                .Lines
                .Select(line => line.ToString().Trim())
                .Count(line => line == "{");
            Assert.True(
                standaloneOpeningBraces == 0,
                $"Generated source '{tree.FilePath}' contains {standaloneOpeningBraces} standalone opening brace(s).");
        }
    }

    [Fact]
    public void NamespaceContentsHaveNoExtraBaseIndentAcrossRecordJsonAndSchemaSources()
    {
        var result = GeneratorTestHelper.RunGenerator(
            """
            using Houtamelo.Spire;

            namespace GeneratedReadability
            {
                [DiscriminatedUnion]
                public partial struct Value
                {
                    [Variant] public static partial Value Item(int value);
                }

                [DiscriminatedUnion(json: JsonLibrary.SystemTextJson)]
                public partial record StjShape
                {
                    public partial record Circle : StjShape;
                }

                [DiscriminatedUnion(json: JsonLibrary.NewtonsoftJson)]
                public partial record NsjShape
                {
                    public partial record Circle : NsjShape;
                }
            }
            """,
            out var compilation,
            out var diagnostics);
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        foreach (var tree in result.GeneratedTrees)
            GeneratedIndentationAssertions.AssertNamespaceMembersHaveNoBaseIndent(tree);
    }

    [Fact]
    public void JsonCompanionSourcesPutTheUnionBeforeConverters()
    {
        var result = GeneratorTestHelper.RunGenerator(
            """
            using Houtamelo.Spire;

            namespace JsonReadability
            {
                [DiscriminatedUnion(json: JsonLibrary.SystemTextJson)]
                public partial record Generic<T>
                {
                    public partial record Some(T Value) : Generic<T>;
                    public partial record None() : Generic<T>;
                }

                [DiscriminatedUnion(json: JsonLibrary.NewtonsoftJson)]
                public partial record Shape
                {
                    public partial record Circle : Shape;
                }
            }
            """,
            out var compilation,
            out var diagnostics);
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        var systemTextJson = result.GeneratedTrees.Single(tree =>
            Path.GetFileName(tree.FilePath).EndsWith(".Stj.g.cs", StringComparison.Ordinal));
        var newtonsoftJson = result.GeneratedTrees.Single(tree =>
            Path.GetFileName(tree.FilePath).EndsWith(".Nsj.g.cs", StringComparison.Ordinal));

        GeneratedIndentationAssertions.AssertFirstNamespaceTypeIs(systemTextJson, "Generic");
        GeneratedIndentationAssertions.AssertFirstNamespaceTypeIs(newtonsoftJson, "Shape");
    }

    [Fact]
    public void RuntimeFactories_PreserveUnmanagedAndManagedPayloadValues()
    {
        var generated = Generate(MixedUnionSource());
        AssertNoErrors(generated);

        var assembly = EmitAndLoad(generated.Compilation);
        var union = assembly.GetType("GameplayCommand", throwOnError: true)!;

        var teleport = union.GetMethod("Teleport")!.Invoke(null, new object[] { 7, 2.5f })!;
        var teleportPayload = union.GetProperty("teleport")!.GetValue(teleport)!;
        Assert.Equal(7, teleportPayload.GetType().GetProperty("target")!.GetValue(teleportPayload));
        Assert.Equal(2.5f, teleportPayload.GetType().GetProperty("duration")!.GetValue(teleportPayload));

        var speech = "hello";
        var say = union.GetMethod("Say")!.Invoke(null, new object[] { speech })!;
        var sayPayload = union.GetProperty("say")!.GetValue(say)!;
        Assert.Same(speech, sayPayload.GetType().GetProperty("speech")!.GetValue(sayPayload));
    }

    [Fact]
    public void UnmanagedOnlyManagedOnlyAndFieldlessVariants_EmitOnlyRequiredStorage()
    {
        var unmanaged = Generate(
            """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct UnmanagedOnly
            {
                [Variant] public static partial UnmanagedOnly A(int value);
                [Variant] public static partial UnmanagedOnly B(float value);
            }
            """);
        AssertNoErrors(unmanaged);
        Assert.Contains("internal struct Unmanaged", unmanaged.Source, StringComparison.Ordinal);
        Assert.DoesNotContain("internal struct Managed", unmanaged.Source, StringComparison.Ordinal);

        var managed = Generate(
            """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct ManagedOnly
            {
                [Variant] public static partial ManagedOnly A(string value);
                [Variant] public static partial ManagedOnly Empty();
            }
            """);
        AssertNoErrors(managed);
        Assert.Contains("internal struct Managed", managed.Source, StringComparison.Ordinal);
        Assert.DoesNotContain("internal struct Unmanaged", managed.Source, StringComparison.Ordinal);

        var mixed = Generate(
            """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct MixedFieldless
            {
                [Variant] public static partial MixedFieldless Value(int value);
                [Variant] public static partial MixedFieldless Empty();
            }
            """);
        AssertNoErrors(mixed);
        Assert.Contains("Kind.Empty", mixed.Source, StringComparison.Ordinal);
        Assert.DoesNotContain("Fields.Unmanaged.Empty", mixed.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void GenericAndNullableFieldsCompileWithoutNullabilityWarnings()
    {
        var generated = Generate(
            """
            #nullable enable
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct GenericCommand<T> where T : unmanaged
            {
                [Variant] public static partial GenericCommand<T> Send(T value, int count, string? note);
                [Variant] public static partial GenericCommand<T> Empty();
            }
            """);
        AssertNoErrors(generated);
        AssertNoWarnings(generated);

        Assert.Contains("readonly T _s", generated.Source, StringComparison.Ordinal);
        Assert.Contains("Fields.Unmanaged.Send", generated.Source, StringComparison.Ordinal);
        Assert.Contains("string?", generated.Source, StringComparison.Ordinal);

        var genericUnion = generated.Compilation.GetTypeByMetadataName("GenericCommand`1")!;
        Assert.Contains(
            genericUnion.GetMembers().OfType<IFieldSymbol>(),
            field => field.Type.Name == "T" && field.DeclaredAccessibility == Accessibility.Private);

        var send = Factory(generated.Source, "Send");
        var sendReturn = ReturnedCreation(send);
        Assert.All(
            sendReturn.ArgumentList!.Arguments.Skip(1),
            argument => Assert.IsType<IdentifierNameSyntax>(argument.Expression));

        var empty = Factory(generated.Source, "Empty");
        var emptyDefault = empty.Body!.DescendantNodes()
            .OfType<LocalDeclarationStatementSyntax>()
            .Single(local => local.Declaration.Type.ToString() == "T");
        Assert.Contains("default", emptyDefault.Declaration.Variables.Single().Initializer!.Value.ToString(), StringComparison.Ordinal);
        var emptyReturn = ReturnedCreation(empty);
        Assert.All(
            emptyReturn.ArgumentList!.Arguments.Skip(1),
            argument => Assert.IsType<IdentifierNameSyntax>(argument.Expression));
    }

    [Fact]
    public void AwkwardNamesAndCollidingParametersUseSafeFactoryLocals()
    {
        var generated = Generate(
            """
            #nullable enable
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Naming<T> where T : unmanaged
            {
                [Variant] public static partial Naming<T> Teleport(
                    int teleportFields,
                    int unmanagedFields,
                    int managedFields,
                    T storage0,
                    string? note);
                [Variant] public static partial Naming<T> HTTPRequest(int requestId);
                [Variant] public static partial Naming<T> @event(string? value, int code);
            }
            """);
        AssertNoErrors(generated);
        AssertNoWarnings(generated);

        var source = generated.Source;
        Assert.Contains("public static partial Naming<T> HTTPRequest(int requestId)", source, StringComparison.Ordinal);
        Assert.Contains("public static partial Naming<T> @event(string? value, int code)", source, StringComparison.Ordinal);
        Assert.Contains("internal struct HTTPRequest", source, StringComparison.Ordinal);
        Assert.Contains("__httpRequest", source, StringComparison.Ordinal);
        Assert.Contains("__unmanaged.__httpRequest.requestId", source, StringComparison.Ordinal);
        Assert.Contains("internal struct @event", source, StringComparison.Ordinal);
        Assert.Contains("__unmanaged.__event.code", source, StringComparison.Ordinal);
        Assert.Contains("event_value", source, StringComparison.Ordinal);

        var teleport = Factory(source, "Teleport");
        var parameters = teleport.ParameterList.Parameters
            .Select(parameter => parameter.Identifier.ValueText)
            .ToHashSet(StringComparer.Ordinal);
        var locals = LocalNames(teleport);

        Assert.Contains(locals, name => name.StartsWith("teleportFields", StringComparison.Ordinal));
        Assert.Contains(locals, name => name.StartsWith("unmanagedFields", StringComparison.Ordinal));
        Assert.Contains(locals, name => name.StartsWith("managedFields", StringComparison.Ordinal));
        Assert.DoesNotContain(locals, parameters.Contains);
    }

    [Fact]
    public void TypeParameterNamesAreReservedByFactoryLocals()
    {
        var generated = Generate(
            """
            #nullable enable
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct TypeParamCollision<managedFields> where managedFields : unmanaged
            {
                [Variant] public static partial TypeParamCollision<managedFields> Send(
                    managedFields value,
                    string? text);
                [Variant] public static partial TypeParamCollision<managedFields> Empty();
            }
            """);
        AssertNoErrors(generated);
        AssertNoWarnings(generated);

        var send = Factory(generated.Source, "Send");
        var union = send.Ancestors().OfType<StructDeclarationSyntax>().First();
        var typeParameters = union.TypeParameterList!.Parameters
            .Select(parameter => parameter.Identifier.ValueText)
            .ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain(LocalNames(send), typeParameters.Contains);
    }

    private static MethodDeclarationSyntax Factory(string source, string name)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        return root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(method => method.Identifier.ValueText == name);
    }

    private static string[] LocalNames(MethodDeclarationSyntax method)
        => method.Body!.DescendantNodes()
            .OfType<VariableDeclaratorSyntax>()
            .Select(variable => variable.Identifier.ValueText)
            .ToArray();

    private static ObjectCreationExpressionSyntax ReturnedCreation(MethodDeclarationSyntax method)
        => Assert.IsType<ObjectCreationExpressionSyntax>(
            method.Body!.Statements.OfType<ReturnStatementSyntax>().Single().Expression);

    private static GeneratedResult Generate(string source)
    {
        var result = GeneratorTestHelper.RunGenerator(
            source,
            out var compilation,
            out var diagnostics,
            path: "readable-factory-test.cs");
        var generatedSource = GeneratorTestHelper.GetUnionGeneratedSource(result);
        Assert.NotNull(generatedSource);
        return new GeneratedResult(compilation, diagnostics, generatedSource!);
    }

    private static void AssertNoErrors(GeneratedResult generated)
    {
        var diagnostics = generated.Diagnostics
            .Concat(generated.Compilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.Empty(diagnostics);
    }

    private static void AssertNoWarnings(GeneratedResult generated)
    {
        var diagnostics = generated.Diagnostics
            .Concat(generated.Compilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
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

    private static string MixedUnionSource() => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct GameplayCommand
        {
            [Variant] public static partial GameplayCommand Teleport(int target, float duration);
            [Variant] public static partial GameplayCommand Say(string speech);
            [Variant] public static partial GameplayCommand Idle();
        }
        """;

    private readonly record struct GeneratedResult(
        Compilation Compilation,
        ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> Diagnostics,
        string Source);
}
