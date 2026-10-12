using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Houtamelo.Spire.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
#if SPIRE_UNITY_TEST_HOST
using Spire.Tests.Hosting;
#endif

namespace Houtamelo.Spire.Analyzers.Tests;

public sealed class DiagnosticHelpLinkTests
{
    private const string HelpLinkRoot =
        "https://github.com/Houtamelo/spire_csharp_analyzers/blob/main/docs/rules/";

    private static readonly MetadataReference AnalyzerAssemblyReference =
        MetadataReference.CreateFromFile(typeof(SPIRE002EnforceInitializationOnFieldlessTypeAnalyzer).Assembly.Location);

    private static readonly MetadataReference CoreAssemblyReference =
        MetadataReference.CreateFromFile(typeof(Houtamelo.Spire.EnforceInitializationAttribute).Assembly.Location);

    [Fact]
    public async Task SPIRE002_FlaggedDiagnostic_UsesPublishedHelpLink_AndFieldBearingTypeIsNotFlagged()
    {
        var source = """
            using Houtamelo.Spire;

            [EnforceInitialization]
            public struct FieldlessType { }

            [EnforceInitialization]
            public struct FieldBearingType
            {
                public int Value;
            }
            """;

        var references = await ResolveReferencesAsync();
        var compilation = CSharpCompilation.Create(
            "DiagnosticHelpLinkTestAssembly",
            new[]
            {
                CSharpSyntaxTree.ParseText(
                    source,
                    CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
                    path: "DiagnosticHelpLink.cs"),
            },
            references.Add(AnalyzerAssemblyReference).Add(CoreAssemblyReference),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var compilerErrors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.Empty(compilerErrors);

        var diagnostics = await compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(
                    new SPIRE002EnforceInitializationOnFieldlessTypeAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();

        var spireDiagnostics = diagnostics.Where(diagnostic => diagnostic.Id == "SPIRE002").ToArray();
        var diagnostic = Assert.Single(spireDiagnostics);

        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("FieldlessType", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.DoesNotContain("FieldBearingType", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Equal(HelpLinkRoot + "SPIRE002.md", diagnostic.Descriptor.HelpLinkUri);
    }

    [Fact]
    public void BaseRuleSupportedDescriptors_PointToMatchingLocalRuleDocs()
    {
        var analyzers = new DiagnosticAnalyzer[]
        {
            new SPIRE001ArrayOfEnforceInitializationStructAnalyzer(),
            new SPIRE002EnforceInitializationOnFieldlessTypeAnalyzer(),
            new SPIRE003DefaultOfEnforceInitializationStructAnalyzer(),
            new SPIRE004NewOfEnforceInitializationStructWithoutCtorAnalyzer(),
            new SPIRE005ActivatorCreateInstanceOfEnforceInitializationStructAnalyzer(),
            new SPIRE006ClearOfEnforceInitializationElementsAnalyzer(),
            new SPIRE007UnsafeSkipInitOfEnforceInitializationStructAnalyzer(),
            new SPIRE008GetUninitializedObjectOfEnforceInitializationStructAnalyzer(),
            new SPIRE015ExhaustiveEnumSwitchAnalyzer(),
            new SPIRE016InvalidEnforceInitializationEnumValueAnalyzer(),
        };

        var documentationRoot = FindRuleDocumentationRoot();
        var descriptors = analyzers
            .SelectMany(analyzer => analyzer.SupportedDiagnostics)
            .ToArray();

        Assert.NotEmpty(descriptors);

        foreach (var descriptor in descriptors)
        {
            var expectedHelpLink = HelpLinkRoot + descriptor.Id + ".md";
            Assert.Equal(expectedHelpLink, descriptor.HelpLinkUri);

            var documentationPath = Path.Combine(documentationRoot, descriptor.Id + ".md");
            Assert.True(
                File.Exists(documentationPath),
                $"Expected local rule documentation for {descriptor.Id}: {documentationPath}");
        }
    }

    private static async Task<ImmutableArray<MetadataReference>> ResolveReferencesAsync()
    {
#if SPIRE_UNITY_TEST_HOST
        await Task.CompletedTask;
        return UnityTestHost.FrameworkReferences;
#else
        var references = await ReferenceAssemblies.Net.Net80.ResolveAsync(
            LanguageNames.CSharp,
            CancellationToken.None);
        return references;
#endif
    }

    private static string FindRuleDocumentationRoot()
    {
#if SPIRE_UNITY_TEST_HOST
        var dataRoot = UnityTestHost.DataRoot(typeof(DiagnosticHelpLinkTests).Assembly);
        var documentationRoot = Path.Combine(dataRoot, "docs", "rules");
        if (!Directory.Exists(documentationRoot))
        {
            throw new InvalidOperationException(
                "Portable analyzer tests require the committed docs/rules resource " +
                $"under the copied Unity test data root: {documentationRoot}");
        }

        return documentationRoot;
#else
        var repositoryRoot = FindRepositoryRoot();
        return Path.Combine(repositoryRoot, "docs", "rules");
#endif
    }

    private static string FindRepositoryRoot()
    {
        foreach (var startingPath in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(startingPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Spire.Analyzers.slnx")) &&
                    Directory.Exists(Path.Combine(directory.FullName, "docs", "rules")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException(
            "Could not locate the repository root for diagnostic help-link tests.");
    }
}
