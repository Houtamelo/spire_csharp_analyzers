using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Spire.SourceGenerators.Tests;

internal static class GeneratorTestHelper
{
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
            .ToArray();
        return trustedAssemblies;
    }

    public static GeneratorDriverRunResult RunGenerator(
        string source,
        out Compilation outputCompilation,
        out ImmutableArray<Diagnostic> diagnostics,
        string path = "test.cs")
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
            path: path);

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            BaseReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

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
        var excludedHints = new HashSet<string>
        {
            "DiscriminatedUnionAttribute.g.cs",
            "VariantAttribute.g.cs",
            "Layout.g.cs",
        };

        var unionResult = result.GeneratedTrees
            .Where(t => !excludedHints.Contains(System.IO.Path.GetFileName(t.FilePath)))
            .Select(t => t.GetText().ToString())
            .FirstOrDefault();

        return unionResult;
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
        ImmutableArray<Diagnostic> diagnostics)
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
