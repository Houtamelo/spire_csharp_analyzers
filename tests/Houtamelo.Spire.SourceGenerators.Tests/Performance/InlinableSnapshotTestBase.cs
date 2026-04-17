using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Xunit;
using Xunit.Sdk;

namespace Houtamelo.Spire.SourceGenerators.Tests.Performance;

/// <summary>
/// Snapshot test base for the [Inlinable] twin generator. Discovers leaf
/// directories under cases/Performance/Inlinable/ at runtime; each must
/// contain input.cs and output.cs.
/// </summary>
public abstract class InlinableSnapshotTestBase
{
    [Theory]
    [InlinableCaseDiscovery]
    public void Verify(string casePath)
    {
        var casesDir = Path.Combine(AppContext.BaseDirectory, "cases", "Performance", "Inlinable");
        var caseDir = Path.Combine(casesDir, casePath);

        var inputPath = Path.Combine(caseDir, "input.cs");
        var outputPath = Path.Combine(caseDir, "output.cs");

        var inputSource = File.ReadAllText(inputPath);
        var expectedSource = File.ReadAllText(outputPath);

        var result = GeneratorTestHelper.RunInlinableTwinGenerator(
            inputSource, out var compilation, out var diagnostics);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);

        var actualSource = GeneratorTestHelper.GetInlinableGeneratedSource(result);
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
public sealed class InlinableCaseDiscoveryAttribute : DataAttribute
{
    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        var casesDir = Path.Combine(AppContext.BaseDirectory, "cases", "Performance", "Inlinable");
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
