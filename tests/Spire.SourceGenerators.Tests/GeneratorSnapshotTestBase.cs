using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using Xunit.Sdk;

namespace Spire.SourceGenerators.Tests;

/// <summary>
/// Base class for snapshot tests. Discovers leaf directories under cases/ at runtime.
/// Each leaf directory must contain input.cs and output.cs.
/// </summary>
public abstract class GeneratorSnapshotTestBase
{
    [Theory]
    [SnapshotCaseDiscovery]
    public void Verify(string casePath)
    {
        var casesDir = Path.Combine(AppContext.BaseDirectory, "cases");
        var caseDir = Path.Combine(casesDir, casePath);

        var inputPath = Path.Combine(caseDir, "input.cs");
        var outputPath = Path.Combine(caseDir, "output.cs");

        var inputSource = File.ReadAllText(inputPath);
        var expectedSource = File.ReadAllText(outputPath);

        var result = GeneratorTestHelper.RunGenerator(
            inputSource, out var compilation, out var diagnostics);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);

        var actualSource = GeneratorTestHelper.GetUnionGeneratedSource(result);
        Assert.NotNull(actualSource);

        var actualTree = CSharpSyntaxTree.ParseText(actualSource!);
        var expectedTree = CSharpSyntaxTree.ParseText(expectedSource);

        var actualRoot = actualTree.GetRoot();
        var expectedRoot = expectedTree.GetRoot();

        if (!actualRoot.IsEquivalentTo(expectedRoot))
        {
            throw new XunitException(
                $"Snapshot mismatch for case '{casePath}'.\n\n" +
                $"=== EXPECTED ===\n{expectedSource}\n\n" +
                $"=== ACTUAL ===\n{actualSource}");
        }

        GeneratorTestHelper.AssertNoCompilationErrors(compilation);
    }
}

/// Discovers snapshot test cases by scanning for leaf directories under cases/.
/// Each leaf directory must contain both input.cs and output.cs.
[AttributeUsage(AttributeTargets.Method)]
public sealed class SnapshotCaseDiscoveryAttribute : DataAttribute
{
    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        var casesDir = Path.Combine(AppContext.BaseDirectory, "cases");
        if (!Directory.Exists(casesDir))
            yield break;

        foreach (var leafDir in FindLeafDirectories(casesDir))
        {
            var inputPath = Path.Combine(leafDir, "input.cs");
            var outputPath = Path.Combine(leafDir, "output.cs");

            if (!File.Exists(inputPath) || !File.Exists(outputPath))
            {
                throw new InvalidOperationException(
                    $"Test case directory '{leafDir}' must contain both input.cs and output.cs");
            }

            var relativePath = Path.GetRelativePath(casesDir, leafDir);
            yield return new object[] { relativePath };
        }
    }

    private static IEnumerable<string> FindLeafDirectories(string root)
    {
        foreach (var dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
        {
            if (Directory.GetDirectories(dir).Length == 0)
                yield return dir;
        }
    }
}
