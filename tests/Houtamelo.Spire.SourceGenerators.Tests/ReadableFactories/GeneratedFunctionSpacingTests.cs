using System;
using System.Collections.Generic;
using System.Linq;
using Houtamelo.Spire.SourceGenerators.Tests.Unity;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.ReadableFactories;

public sealed class GeneratedFunctionSpacingTests
{
    [Fact]
    public void GeneratedFunctionMembersUseExactlyOneBlankLineWhenEitherIsMultiline()
    {
        var result = GeneratorTestHelper.RunGenerator(
            StandaloneSource(),
            out var compilation,
            out var diagnostics);
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        foreach (var tree in result.GeneratedTrees)
            AssertFunctionSpacing(tree);
        Assert.Contains(
            result.GeneratedTrees.Select(tree => tree.GetText().ToString()),
            source => source.Contains("#pragma warning disable CS8619, CS8620", StringComparison.Ordinal));

        var unityResult = UnityGeneratorTestHarness.Run(
            UnitySource(),
            includeEditorReferences: true);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(unityResult);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(unityResult);

        foreach (var tree in unityResult.RunResult.GeneratedTrees)
            AssertFunctionSpacing(tree);

        var unionTree = unityResult.RunResult.GeneratedTrees.Single(tree =>
            tree.GetRoot().DescendantNodes()
                .OfType<StructDeclarationSyntax>()
                .Any(type => type.Identifier.ValueText == "UnityCommand"));
        var unionSource = unionTree.GetText().ToString();
        Assert.Contains("#if !UNITY_EDITOR", unionSource, StringComparison.Ordinal);
        Assert.Contains("#if UNITY_EDITOR", string.Join(
            "\n",
            unityResult.RunResult.GeneratedTrees.Select(tree => tree.GetText().ToString())),
            StringComparison.Ordinal);
    }

    [Fact]
    public void OneLineKindAndVariantFlagsRemainCompact()
    {
        var result = GeneratorTestHelper.RunGenerator(
            """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command Move(int value);
                [Variant] public static partial Command Idle();
                [Variant] public static partial Command Speak(string text);
            }
            """,
            out var compilation,
            out var diagnostics);
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        var tree = result.GeneratedTrees.Single(generatedTree =>
            generatedTree.GetRoot().DescendantNodes()
                .OfType<StructDeclarationSyntax>()
                .Any(type => type.Identifier.ValueText == "Command"));
        var command = tree.GetRoot().DescendantNodes()
            .OfType<StructDeclarationSyntax>()
            .Single(type => type.Identifier.ValueText == "Command");
        var flags = command.Members
            .OfType<PropertyDeclarationSyntax>()
            .Where(property => property.Identifier.ValueText.StartsWith("Is", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(flags);
        Assert.All(flags, property => Assert.False(IsMultiline(property)));
        for (var index = 0; index + 1 < flags.Length; index++)
        {
            Assert.Equal(
                0,
                CountBlankLines(tree.GetText(), flags[index], flags[index + 1]));
        }
    }

    [Fact]
    public void AttributeTriviaDoesNotMakeAnExpressionBodiedMethodMultiline()
    {
        var result = UnityGeneratorTestHarness.Run(UnitySource());
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);

        var tree = result.RunResult.GeneratedTrees.Single(generatedTree =>
            generatedTree.GetRoot().DescendantNodes()
                .OfType<StructDeclarationSyntax>()
                .Any(type => type.Identifier.ValueText == "UnityCommand"));
        var method = tree.GetRoot().DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(candidate => candidate.Identifier.ValueText == "OnBeforeSerialize");

        Assert.False(IsMultiline(method));
        Assert.NotEmpty(method.AttributeLists);
        Assert.NotNull(method.ExpressionBody);
    }

    private static void AssertFunctionSpacing(SyntaxTree tree)
    {
        var source = tree.GetText();
        var root = tree.GetRoot();
        foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            var members = type.Members;
            for (var index = 0; index + 1 < members.Count; index++)
            {
                var previous = members[index];
                var next = members[index + 1];
                if (!IsFunctionMember(previous) || !IsFunctionMember(next))
                    continue;

                if (IsMultiline(previous) || IsMultiline(next))
                {
                    Assert.True(
                        CountBlankLines(source, previous, next) == 1,
                        $"Expected one blank line between {previous.Kind()} and {next.Kind()} in {tree.FilePath}.");
                }
            }
        }
    }

    private static bool IsFunctionMember(MemberDeclarationSyntax member)
        => member is MethodDeclarationSyntax
            or ConstructorDeclarationSyntax
            or OperatorDeclarationSyntax
            or ConversionOperatorDeclarationSyntax
            or PropertyDeclarationSyntax { AccessorList: not null }
            or PropertyDeclarationSyntax { ExpressionBody: not null };

    private static bool IsMultiline(MemberDeclarationSyntax member)
    {
        var first = member.DescendantTokens()
            .First(token => !token.Parent!.AncestorsAndSelf().OfType<AttributeListSyntax>().Any());
        var last = member.GetLastToken();
        return first.GetLocation().GetLineSpan().StartLinePosition.Line !=
            last.GetLocation().GetLineSpan().EndLinePosition.Line;
    }

    private static int CountBlankLines(
        SourceText source,
        MemberDeclarationSyntax previous,
        MemberDeclarationSyntax next)
    {
        var previousLine = previous.GetLastToken().GetLocation().GetLineSpan().EndLinePosition.Line;
        var nextLine = next.GetLocation().GetLineSpan().StartLinePosition.Line;
        if (nextLine <= previousLine + 1)
            return 0;

        return source.Lines
            .Skip(previousLine + 1)
            .Take(nextLine - previousLine - 1)
            .Count(line => string.IsNullOrWhiteSpace(line.ToString()));
    }

    private static string StandaloneSource() => """
        #nullable enable
        using Houtamelo.Spire;

        [DiscriminatedUnion]
        public partial struct Command
        {
            [Variant]
            public static partial Command Move(int destination, float speed);

            [Variant]
            public static partial Command Idle();
        }

        [DiscriminatedUnion]
        public partial struct NullableCommand<T> where T : unmanaged
        {
            [Variant]
            public static partial NullableCommand<T> Send(T value, string note);

            [Variant]
            public static partial NullableCommand<T> Maybe(T value, string? note);
        }

        [DiscriminatedUnion(json: JsonLibrary.SystemTextJson)]
        public partial record StjShape
        {
            public partial record Circle : StjShape;
            public partial record Point : StjShape;
        }

        [DiscriminatedUnion(json: JsonLibrary.NewtonsoftJson)]
        public partial record NsjShape
        {
            public partial record Circle : NsjShape;
            public partial record Point : NsjShape;
        }
        """;

    private static string UnitySource() => """
        using System;
        using Houtamelo.Spire;

        [Serializable, DiscriminatedUnion]
        public partial struct UnityCommand
        {
            [Variant]
            public static partial UnityCommand Move(
                int destination,
                [UnitySerializeReference] Effect? effect);

            [Variant]
            public static partial UnityCommand Idle();
        }

        [Serializable]
        public sealed class Effect { }
        """;
}
