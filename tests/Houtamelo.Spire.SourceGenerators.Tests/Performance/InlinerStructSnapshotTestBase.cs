using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Xunit;
using Xunit.Sdk;

namespace Houtamelo.Spire.SourceGenerators.Tests.Performance;

/// <summary>
/// Snapshot test base for the [InlinerStruct] source generator. Discovers leaf
/// directories under cases/Performance/InlinerStruct/ at runtime; each must
/// contain input.cs and output.cs.
/// </summary>
public abstract class InlinerStructSnapshotTestBase
{
    [Theory]
    [InlinerStructCaseDiscovery]
    public void Verify(string casePath)
    {
        var casesDir = Path.Combine(AppContext.BaseDirectory, "cases", "Performance", "InlinerStruct");
        var caseDir = Path.Combine(casesDir, casePath);

        var inputPath = Path.Combine(caseDir, "input.cs");
        var outputPath = Path.Combine(caseDir, "output.cs");

        var inputSource = File.ReadAllText(inputPath);
        var expectedSource = File.ReadAllText(outputPath);

        var result = GeneratorTestHelper.RunInlinerStructGenerator(
            inputSource, out var compilation, out var diagnostics);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);

        var actualSource = GeneratorTestHelper.GetInlinerGeneratedSource(result);
        Assert.NotNull(actualSource);

        if (!GeneratorTestHelper.AreStructurallyEquivalent(actualSource!, expectedSource))
        {
            throw new XunitException(
                $"Snapshot mismatch for case '{casePath}'.\n\n" +
                $"=== EXPECTED ===\n{expectedSource}\n\n" +
                $"=== ACTUAL ===\n{actualSource}");
        }

        GeneratorTestHelper.AssertNoCompilationErrors(compilation);
    }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class InlinerStructCaseDiscoveryAttribute : DataAttribute
{
    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        var casesDir = Path.Combine(AppContext.BaseDirectory, "cases", "Performance", "InlinerStruct");
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
