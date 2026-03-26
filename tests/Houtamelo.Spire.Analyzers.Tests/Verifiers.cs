using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Spire.Analyzers.Tests;

public static class AnalyzerVerifier<TAnalyzer>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    private static readonly MetadataReference AnalyzerAssemblyReference =
        MetadataReference.CreateFromFile(typeof(TAnalyzer).Assembly.Location);

    private static readonly ReferenceAssemblies Net80Assemblies =
        ReferenceAssemblies.Net.Net80;

    public static async Task VerifyAsync(string source)
    {
        var test = new CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = Net80Assemblies,
        };
        test.TestState.AdditionalReferences.Add(AnalyzerAssemblyReference);
        await test.RunAsync();
    }

    public static DiagnosticResult Diagnostic(string diagnosticId)
        => CSharpAnalyzerVerifier<TAnalyzer, DefaultVerifier>.Diagnostic(diagnosticId);
}

public static class TestCaseLoader
{
    public static string LoadCase(string ruleId, string caseName)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, ruleId, "cases");
        var sharedPath = Path.Combine(dir, "_shared.cs");
        var casePath = Path.Combine(dir, $"{caseName}.cs");

        var shared = File.Exists(sharedPath) ? File.ReadAllText(sharedPath) : "";
        var caseSource = File.ReadAllText(casePath);

        return shared + Environment.NewLine + caseSource;
    }
}
