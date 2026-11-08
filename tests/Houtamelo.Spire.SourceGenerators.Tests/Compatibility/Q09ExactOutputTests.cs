using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Houtamelo.Spire.SourceGenerators.Tests.Unity;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Compatibility;

public sealed class Q09ExactOutputTests
{
    [Fact]
    public void BaselineManifest_CoreCasesContainExactGeneratedSource()
    {
        var manifest = LoadManifest();

        Assert.Equal("46ac18b", manifest.Baseline);
        var coreCases = manifest.Cases
            .Where(caseInfo => !caseInfo.Kind.StartsWith("unity-", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(28, coreCases.Length);
        Assert.Equal(24, manifest.Cases.Count(caseInfo =>
            caseInfo.Kind.StartsWith("performance-", StringComparison.Ordinal)));

        var outputCount = 0;
        foreach (var caseInfo in coreCases)
        {
            var source = ReadData(caseInfo.Input);
            var result = RunCase(caseInfo, source);
            AssertExactOutputs(caseInfo, result.GeneratedTrees);
            ArchiveActualOutputsIfRequested(caseInfo, result.GeneratedTrees);
            outputCount += caseInfo.Outputs.Count;
        }

        Assert.Equal(65, outputCount);
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void BaselineManifest_UnityCasesContainExactGeneratedSource()
    {
        var manifest = LoadManifest();
        var unityCases = manifest.Cases
            .Where(caseInfo => caseInfo.Kind.StartsWith("unity-", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(2, unityCases.Length);

        var outputCount = 0;
        foreach (var caseInfo in unityCases)
        {
            var source = ReadData(caseInfo.Input);
            var result = RunCase(caseInfo, source);
            AssertExactOutputs(caseInfo, result.GeneratedTrees);
            ArchiveActualOutputsIfRequested(caseInfo, result.GeneratedTrees);
            outputCount += caseInfo.Outputs.Count;
        }

        Assert.Equal(12, outputCount);
    }

    [Fact]
    public void CoreGeneratedSource_IsIndependentOfInputPathAndUnrelatedInput()
    {
        var manifest = LoadManifest();
        var caseInfo = manifest.Cases.Single(candidate => candidate.Id == "core");
        var source = ReadData(caseInfo.Input);

        var first = RunDiscriminatedUnion(source, "q09/core-first.cs");
        var second = RunDiscriminatedUnion(source, "different/root/core-second.cs");
        AssertGeneratedSourcesEqual(first.GeneratedTrees, second.GeneratedTrees);

        var withUnrelatedInput = source + "\nnamespace Q09.Unrelated { public sealed class Marker { } }\n";
        var unrelated = RunDiscriminatedUnion(withUnrelatedInput, "q09/core-first.cs");
        AssertGeneratedSourcesEqual(first.GeneratedTrees, unrelated.GeneratedTrees);
    }

    [Fact]
    public void PerformanceSupport_RemainsStableAcrossRepeatedRuns()
    {
        var manifest = LoadManifest();

        var performanceCase = manifest.Cases.Single(candidate =>
            candidate.Id == "performance-InlinerStruct-static_void_arity1");
        var performanceSource = ReadData(performanceCase.Input);
        var performanceFirst = RunInlinerStruct(performanceSource, "q09/performance-first.cs");
        var performanceSecond = RunInlinerStruct(performanceSource, "q09/performance-second.cs");
        AssertGeneratedSourcesEqual(performanceFirst.GeneratedTrees, performanceSecond.GeneratedTrees);
    }

    [Fact]
    [Trait("Environment", "Unity")]
    public void UnitySupport_RemainsStableAcrossRepeatedRuns()
    {
        var manifest = LoadManifest();
        var unityCase = manifest.Cases.Single(candidate => candidate.Id == "unity-editor");
        var unitySource = ReadData(unityCase.Input);
        var unityFirst = RunUnity(unitySource, includeEditorReferences: true);
        var unitySecond = RunUnity(unitySource, includeEditorReferences: true);
        AssertGeneratedSourcesEqual(unityFirst.GeneratedTrees, unitySecond.GeneratedTrees);
    }

    private static GeneratedRun RunCase(CaseManifest caseInfo, string source)
    {
        return caseInfo.Kind switch
        {
            "discriminated-union" => RunDiscriminatedUnion(source, caseInfo.Input),
            "unity-player" => RunUnity(source, includeEditorReferences: false),
            "unity-editor" => RunUnity(source, includeEditorReferences: true),
            "performance-inliner" => RunInlinerStruct(source, caseInfo.Input),
            "performance-inlinable" => RunInlinableTwin(source, caseInfo.Input),
            "benchmark" => RunBenchmark(source, caseInfo.Input),
            _ => throw new InvalidOperationException(
                $"Unknown Q09 compatibility case kind '{caseInfo.Kind}'."),
        };
    }

    private static GeneratedRun RunDiscriminatedUnion(string source, string path)
    {
        var result = GeneratorTestHelper.RunGenerator(
            source,
            out var compilation,
            out var diagnostics,
            path: path);
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);
        return new GeneratedRun(result.GeneratedTrees, compilation);
    }

    private static GeneratedRun RunUnity(string source, bool includeEditorReferences)
    {
        var result = UnityGeneratorTestHarness.Run(
            source,
            includeEditorReferences: includeEditorReferences);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        return new GeneratedRun(result.RunResult.GeneratedTrees, result.OutputCompilation);
    }

    private static GeneratedRun RunInlinerStruct(string source, string path)
    {
        var result = GeneratorTestHelper.RunInlinerStructGenerator(
            source,
            out var compilation,
            out var diagnostics,
            path: path);
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);
        return new GeneratedRun(result.GeneratedTrees, compilation);
    }

    private static GeneratedRun RunInlinableTwin(string source, string path)
    {
        var result = GeneratorTestHelper.RunInlinableTwinGenerator(
            source,
            out var compilation,
            out var diagnostics,
            path: path);
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);
        return new GeneratedRun(result.GeneratedTrees, compilation);
    }

    private static GeneratedRun RunBenchmark(string source, string path)
    {
        var parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Latest);
        var tree = CSharpSyntaxTree.ParseText(source, parseOptions, path: path);
        var references = TestHarnessEnvironment.FrameworkReferences
            .Append(MetadataReference.CreateFromFile(
                typeof(Houtamelo.Spire.DiscriminatedUnionAttribute).Assembly.Location));
        var compilation = CSharpCompilation.Create(
            "Q09BenchmarkCompatibility",
            new[] { tree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var driver = CSharpGeneratorDriver.Create(
                new[] { new BenchmarkUnionGenerator().AsSourceGenerator() },
                parseOptions: parseOptions)
            .RunGeneratorsAndUpdateCompilation(
                compilation,
                out var outputCompilation,
                out var diagnostics);
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(outputCompilation);
        return new GeneratedRun(driver.GetRunResult().GeneratedTrees, outputCompilation);
    }

    private static void AssertExactOutputs(
        CaseManifest caseInfo,
        ImmutableArray<SyntaxTree> generatedTrees)
    {
        var actual = generatedTrees
            .Select(tree =>
            {
                var hint = Path.GetFileName(tree.FilePath);
                Assert.False(string.IsNullOrWhiteSpace(hint));
                return (Hint: hint!, Source: tree.GetText().ToString());
            })
            .OrderBy(item => item.Hint, StringComparer.Ordinal)
            .ToArray();
        var expected = caseInfo.Outputs
            .OrderBy(output => output.Hint, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            expected.Select(output => output.Hint).ToArray(),
            actual.Select(output => output.Hint).ToArray());

        foreach (var expectedOutput in expected)
        {
            var actualOutput = actual.Single(output => output.Hint == expectedOutput.Hint);
            var expectedSource = ReadData(expectedOutput.Expected);
            var normalizedExpected = NormalizeNewlines(expectedSource);
            var normalizedActual = NormalizeNewlines(actualOutput.Source);

            Assert.Equal(
                expectedOutput.SourceSha256,
                Sha256(normalizedExpected));
            Assert.Equal(
                expectedOutput.SourceSha256,
                Sha256(normalizedActual));
            Assert.Equal(normalizedExpected, normalizedActual);
        }
    }

    private static void AssertGeneratedSourcesEqual(
        ImmutableArray<SyntaxTree> first,
        ImmutableArray<SyntaxTree> second)
    {
        var firstSources = GetGeneratedSources(first);
        var secondSources = GetGeneratedSources(second);
        Assert.Equal(firstSources.Keys.OrderBy(key => key, StringComparer.Ordinal),
            secondSources.Keys.OrderBy(key => key, StringComparer.Ordinal));
        foreach (var hint in firstSources.Keys)
        {
            Assert.Equal(
                NormalizeNewlines(firstSources[hint]),
                NormalizeNewlines(secondSources[hint]));
        }
    }

    private static void ArchiveActualOutputsIfRequested(
        CaseManifest caseInfo,
        ImmutableArray<SyntaxTree> generatedTrees)
    {
        var archiveRoot = Environment.GetEnvironmentVariable("SPIRE_Q09_ACTUAL_ROOT");
        if (string.IsNullOrWhiteSpace(archiveRoot))
            return;

        var actual = GetGeneratedSources(generatedTrees);
        foreach (var expectedOutput in caseInfo.Outputs)
        {
            var hint = expectedOutput.Hint;
            var expectedName = Path.GetFileName(expectedOutput.Expected);
            var caseDirectory = Path.Combine(archiveRoot, caseInfo.Id);
            Directory.CreateDirectory(caseDirectory);
            var actualPath = Path.Combine(caseDirectory, expectedName);
            File.WriteAllText(
                actualPath,
                actual[hint],
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }

    private static Dictionary<string, string> GetGeneratedSources(
        ImmutableArray<SyntaxTree> generatedTrees)
    {
        var sources = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var tree in generatedTrees)
        {
            var hint = Path.GetFileName(tree.FilePath);
            Assert.False(string.IsNullOrWhiteSpace(hint));
            Assert.True(
                sources.TryAdd(hint!, tree.GetText().ToString()),
                $"Duplicate generated hint '{hint}'.");
        }

        return sources;
    }

    private static CompatibilityManifest LoadManifest()
    {
        var manifestPath = Path.Combine(
            TestHarnessEnvironment.DataRoot,
            "Compatibility",
            "manifest.json");
        Assert.True(File.Exists(manifestPath), $"Q09 manifest not found: {manifestPath}");
        var manifest = JsonSerializer.Deserialize<CompatibilityManifest>(
            File.ReadAllText(manifestPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(manifest);
        return manifest!;
    }

    private static string ReadData(string relativePath)
    {
        var path = Path.Combine(
            TestHarnessEnvironment.DataRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"Q09 compatibility data not found: {path}");
        return File.ReadAllText(path);
    }

    private static string NormalizeNewlines(string source)
        => source.Replace("\r\n", "\n").Replace('\r', '\n');

    private static string Sha256(string source)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(source));
        var builder = new StringBuilder(bytes.Length * 2);
        foreach (var value in bytes)
            builder.Append(value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        return builder.ToString();
    }

    private sealed class GeneratedRun
    {
        public GeneratedRun(ImmutableArray<SyntaxTree> generatedTrees, Compilation compilation)
        {
            GeneratedTrees = generatedTrees;
            Compilation = compilation;
        }

        public ImmutableArray<SyntaxTree> GeneratedTrees { get; }
        public Compilation Compilation { get; }
    }

    private sealed class CompatibilityManifest
    {
        public string Baseline { get; set; } = string.Empty;
        public List<CaseManifest> Cases { get; set; } = new();
    }

    private sealed class CaseManifest
    {
        public string Id { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string Input { get; set; } = string.Empty;
        public List<OutputManifest> Outputs { get; set; } = new();
    }

    private sealed class OutputManifest
    {
        public string Hint { get; set; } = string.Empty;
        public string Expected { get; set; } = string.Empty;
        public string SourceSha256 { get; set; } = string.Empty;
    }
}
