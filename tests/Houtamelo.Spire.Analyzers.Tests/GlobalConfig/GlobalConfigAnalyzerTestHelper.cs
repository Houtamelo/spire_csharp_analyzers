using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Houtamelo.Spire.Analyzers.Tests.GlobalConfig;

internal static class GlobalConfigAnalyzerTestHelper
{
    private static readonly MetadataReference CoreAssemblyReference =
        MetadataReference.CreateFromFile(typeof(Houtamelo.Spire.EnforceInitializationAttribute).Assembly.Location);

    private static readonly MetadataReference AnalyzerAssemblyReference =
        MetadataReference.CreateFromFile(typeof(Houtamelo.Spire.Analyzers.Rules.SPIRE015ExhaustiveEnumSwitchAnalyzer).Assembly.Location);

    public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync<TAnalyzer>(
        string source,
        Dictionary<string, string> globalOptions)
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        var refs = await ReferenceAssemblies.Net.Net80.ResolveAsync(
            LanguageNames.CSharp, CancellationToken.None);
        refs = refs.Add(CoreAssemblyReference).Add(AnalyzerAssemblyReference);

        var tree = CSharpSyntaxTree.ParseText(source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest));

        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { tree }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var configProvider = new TestAnalyzerConfigOptionsProvider(globalOptions);
        var analyzerOptions = new AnalyzerOptions(
            ImmutableArray<AdditionalText>.Empty, configProvider);

        var analyzer = new TAnalyzer();
        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer), analyzerOptions);

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }
}
