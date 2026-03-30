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
    private static readonly MetadataReference CoreAssemblyReference =
        MetadataReference.CreateFromFile(typeof(EnforceInitializationAttribute).Assembly.Location);

    private static readonly MetadataReference[] BaseReferences =
        GetBaseReferences();

    private static MetadataReference[] GetBaseReferences()
    {
        // Collect core BCL references from the running test process.
        // Includes System.Runtime, System.Collections, etc.
        var trustedAssemblies = ((string)System.AppContext.GetData(
            "TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(System.IO.Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Cast<MetadataReference>()
            .Append(CoreAssemblyReference)
            .ToArray();
        return trustedAssemblies;
    }

    public static GeneratorDriverRunResult RunGenerator(
        string source,
        out Compilation outputCompilation,
        out ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> diagnostics,
        string path = "test.cs")
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
            path: path);

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            BaseReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true));

        var generator = new DiscriminatedUnionGenerator();
        var driver = CSharpGeneratorDriver.Create(generator)
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
}
