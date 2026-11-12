using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests;

internal static class GeneratorTestHelper
{
    internal const string InlinerExperimentalDiagnosticId = "SPIREEXP001";

    private static readonly MetadataReference CoreAssemblyReference =
        MetadataReference.CreateFromFile(typeof(EnforceInitializationAttribute).Assembly.Location);

    private static readonly MetadataReference[] BaseReferences =
        GetBaseReferences();

    private static MetadataReference[] GetBaseReferences()
    {
        return TestHarnessEnvironment.FrameworkReferences
            .Append(CoreAssemblyReference)
            .ToArray();
    }

    public static GeneratorDriverRunResult RunGenerator(
        string source,
        out Compilation outputCompilation,
        out ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> diagnostics,
        string path = "test.cs")
        => RunGeneratorCore(
            source,
            out outputCompilation,
            out diagnostics,
            path,
            suppressInlinerExperimentalDiagnostic: false);

    internal static GeneratorDriverRunResult RunGeneratorWithInlinerExperimentalSuppression(
        string source,
        out Compilation outputCompilation,
        out ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> diagnostics,
        string path = "test.cs")
        => RunGeneratorCore(
            source,
            out outputCompilation,
            out diagnostics,
            path,
            suppressInlinerExperimentalDiagnostic: true);

    private static GeneratorDriverRunResult RunGeneratorCore(
        string source,
        out Compilation outputCompilation,
        out ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> diagnostics,
        string path,
        bool suppressInlinerExperimentalDiagnostic)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
            path: path);

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            BaseReferences,
            CreateInlinerCompilationOptions(suppressInlinerExperimentalDiagnostic));

        var generator = new DiscriminatedUnionGenerator();
        var driver = CSharpGeneratorDriver.Create(generator)
            .RunGeneratorsAndUpdateCompilation(
                compilation,
                out outputCompilation,
                out diagnostics);

        return driver.GetRunResult();
    }

    public static GeneratorDriverRunResult RunInlinerStructGenerator(
        string source,
        out Compilation outputCompilation,
        out ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> diagnostics,
        string path = "test.cs",
        bool suppressInlinerExperimentalDiagnostic = true)
    {
        var compilation = CreateInlinerCompilation(
            source,
            path,
            suppressInlinerExperimentalDiagnostic);

        var generator = new Houtamelo.Spire.Analyzers.SourceGenerators.Performance.InlinerStructGenerator();
        var driver = CSharpGeneratorDriver.Create(generator)
            .RunGeneratorsAndUpdateCompilation(
                compilation,
                out outputCompilation,
                out diagnostics);

        return driver.GetRunResult();
    }

    public static string? GetInlinerGeneratedSource(GeneratorDriverRunResult result)
    {
        return result.GeneratedTrees
            .Where(t => System.IO.Path.GetFileName(t.FilePath).EndsWith(".InlinerStruct.g.cs"))
            .Select(t => t.GetText().ToString())
            .FirstOrDefault();
    }

    public static GeneratorDriverRunResult RunInlinableTwinGenerator(
        string source,
        out Compilation outputCompilation,
        out ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> diagnostics,
        string path = "test.cs",
        bool suppressInlinerExperimentalDiagnostic = true)
    {
        var compilation = CreateInlinerCompilation(
            source,
            path,
            suppressInlinerExperimentalDiagnostic);

        var generator = new Houtamelo.Spire.Analyzers.SourceGenerators.Performance.InlinableTwinGenerator();
        var driver = CSharpGeneratorDriver.Create(generator)
            .RunGeneratorsAndUpdateCompilation(
                compilation,
                out outputCompilation,
                out diagnostics);

        return driver.GetRunResult();
    }

    public static string? GetInlinableGeneratedSource(GeneratorDriverRunResult result)
    {
        return result.GeneratedTrees
            .Where(t => System.IO.Path.GetFileName(t.FilePath).EndsWith(".InlinableTwin.g.cs"))
            .Select(t => t.GetText().ToString())
            .FirstOrDefault();
    }

    internal static GeneratorDriverRunResult RunAllInlinerGenerators(
        string source,
        out Compilation outputCompilation,
        out ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> diagnostics,
        string path = "test.cs",
        bool suppressInlinerExperimentalDiagnostic = true)
    {
        var compilation = CreateInlinerCompilation(
            source,
            path,
            suppressInlinerExperimentalDiagnostic);
        var driver = CSharpGeneratorDriver.Create(
                new Houtamelo.Spire.Analyzers.SourceGenerators.Performance.InlinerStructGenerator()
                    .AsSourceGenerator(),
                new Houtamelo.Spire.Analyzers.SourceGenerators.Performance.InlinableTwinGenerator()
                    .AsSourceGenerator())
            .RunGeneratorsAndUpdateCompilation(
                compilation,
                out outputCompilation,
                out diagnostics);

        return driver.GetRunResult();
    }

    public static string? GetUnionGeneratedSource(GeneratorDriverRunResult result)
    {
        var unionResult = result.GeneratedTrees
            .Where(t =>
            {
                var fileName = System.IO.Path.GetFileName(t.FilePath);
                return !fileName.EndsWith(".Stj.g.cs")
                    && !fileName.EndsWith(".Nsj.g.cs")
                    && !fileName.EndsWith(".ToString.g.cs")
                    && !fileName.EndsWith(".Schema.g.cs");
            })
            .Select(t => t.GetText().ToString())
            .FirstOrDefault();

        return unionResult;
    }

    /// Gets a generated source file matching a specific hint suffix (e.g. ".Stj.g.cs").
    public static string? GetGeneratedSource(GeneratorDriverRunResult result, string hintSuffix)
    {
        return result.GeneratedTrees
            .Where(t => System.IO.Path.GetFileName(t.FilePath).EndsWith(hintSuffix))
            .Select(t => t.GetText().ToString())
            .FirstOrDefault();
    }

    /// Compares two C# source strings for structural equivalence, ignoring whitespace/trivia.
    /// Roslyn's IsEquivalentTo is sensitive to blank lines — this normalizes first.
    public static bool AreStructurallyEquivalent(string source1, string source2)
    {
        var tree1 = CSharpSyntaxTree.ParseText(source1);
        var tree2 = CSharpSyntaxTree.ParseText(source2);
        return tree1.GetRoot().NormalizeWhitespace().IsEquivalentTo(
            tree2.GetRoot().NormalizeWhitespace());
    }

    public static void AssertNoCompilationErrors(Compilation compilation)
    {
        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        if (errors.Count > 0)
        {
            var messages = string.Join("\n",
                errors.Select(e => $"  {e.Location}: {e.Id}: {e.GetMessage()}"));
            Assert.Fail($"Compilation has {errors.Count} error(s):\n{messages}");
        }
    }

    public static void AssertNoGeneratorDiagnostics(
        ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> diagnostics)
    {
        var errors = diagnostics
            .Where(d => d.Severity >= DiagnosticSeverity.Warning)
            .ToList();

        if (errors.Count > 0)
        {
            var messages = string.Join("\n",
                errors.Select(e => $"  {e.Id}: {e.GetMessage()}"));
            Assert.Fail($"Generator reported {errors.Count} diagnostic(s):\n{messages}");
        }
    }

    private static CSharpCompilationOptions CreateInlinerCompilationOptions(
        bool suppressInlinerExperimentalDiagnostic)
    {
        var options = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            allowUnsafe: true);

        if (suppressInlinerExperimentalDiagnostic)
        {
            options = options.WithSpecificDiagnosticOptions(
                options.SpecificDiagnosticOptions.SetItem(
                    InlinerExperimentalDiagnosticId,
                    ReportDiagnostic.Suppress));
        }

        return options;
    }

    private static CSharpCompilation CreateInlinerCompilation(
        string source,
        string path,
        bool suppressInlinerExperimentalDiagnostic)
    {
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions, path: path);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            BaseReferences,
            CreateInlinerCompilationOptions(suppressInlinerExperimentalDiagnostic));

        if (!HasAccessibleExperimentalAttribute(compilation))
        {
            compilation = compilation.AddSyntaxTrees(
                CSharpSyntaxTree.ParseText(
                    ExperimentalAttributeShimSource,
                    parseOptions,
                    path: "__SpireExperimentalAttributeShim.cs"));
        }

        return compilation;
    }

    private static bool HasAccessibleExperimentalAttribute(Compilation compilation)
    {
        var type = compilation.GetTypeByMetadataName(
            "System.Diagnostics.CodeAnalysis.ExperimentalAttribute");
        return type is not null
            && compilation.IsSymbolAccessibleWithin(type, compilation.Assembly);
    }

    private const string ExperimentalAttributeShimSource = """
        using System;

        namespace System.Diagnostics.CodeAnalysis
        {
            [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Module | AttributeTargets.Class |
                            AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Constructor |
                            AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field |
                            AttributeTargets.Event | AttributeTargets.Interface | AttributeTargets.Delegate,
                            Inherited = false)]
            internal sealed class ExperimentalAttribute : Attribute
            {
                public ExperimentalAttribute(string diagnosticId) => DiagnosticId = diagnosticId;

                public string DiagnosticId { get; }
            }
        }
        """;
}
