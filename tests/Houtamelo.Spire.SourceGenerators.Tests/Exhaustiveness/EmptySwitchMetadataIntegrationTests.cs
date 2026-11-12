using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Houtamelo.Spire;
using Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Exhaustiveness;

public sealed class EmptySwitchMetadataIntegrationTests
{
    private const string ProducerSource = """
        #nullable enable
        using System;
        using Houtamelo.Spire;

        namespace Houtamelo.Spire.Functional;

        [DiscriminatedUnion]
        public partial struct Result<T>
        {
            [Variant] public static partial Result<T> Ok(T value);
            [Variant] public static partial Result<T> Err(Exception error);
        }
        """;

    private const string ConsumerSource = """
        #nullable enable
        using Houtamelo.Spire.Functional;

        public static class Consumer
        {
            public static void EmptyStatement(Result<int> result)
            {
                switch (result)
                {
                }
            }

            public static int EmptyExpression(Result<int> result)
                => result switch { };

            public static int OnlyOkExpression(Result<int> result)
                => result switch
                {
                    { IsOk: true, ok: var (value) } => value,
                };

            public static void CatchAllStatement(Result<int> result)
            {
                switch (result)
                {
                    default:
                        break;
                }
            }
        }
        """;

    [Fact]
    public async Task ConsumerWithSpireApiReference_ReportsEmptyAndPartialSwitches()
    {
        var result = await AnalyzeConsumerAsync(includeSpireApiReference: true);

        Assert.NotNull(result.EnforceExhaustivenessAttribute);
        var spireDiagnostics = result.Diagnostics
            .Where(d => d.Id == "SPIRE009")
            .ToArray();
        Assert.Equal(3, spireDiagnostics.Length);

        Assert.Equal(2,
            spireDiagnostics.Count(d => d.Properties["MissingVariants"] == "Ok,Err"));
        Assert.Single(spireDiagnostics, d => d.Properties["MissingVariants"] == "Err");
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "AD0001");
    }

    [Fact]
    public async Task ConsumerWithoutDirectSpireApiReference_SkipsSpireAnalysis()
    {
        var result = await AnalyzeConsumerAsync(includeSpireApiReference: false);

        Assert.Null(result.EnforceExhaustivenessAttribute);
        Assert.DoesNotContain(result.Compilation.References, IsSpireRuntimeReference);
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "SPIRE009");
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "AD0001");
    }

    private static async Task<(
        Compilation Compilation,
        ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> Diagnostics,
        INamedTypeSymbol? EnforceExhaustivenessAttribute)> AnalyzeConsumerAsync(
        bool includeSpireApiReference)
    {
        var producerReference = EmitProducerReference();
        var references = TestHarnessEnvironment.FrameworkReferences
            .Where(reference => !IsSpireRuntimeReference(reference))
            .ToImmutableArray()
            .Add(producerReference);
        if (includeSpireApiReference)
        {
            references = references.Add(MetadataReference.CreateFromFile(
                typeof(EnforceExhaustivenessAttribute).Assembly.Location));
        }

        var tree = CSharpSyntaxTree.ParseText(
            ConsumerSource,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview),
            path: "empty-switch-consumer.cs");
        var compilation = CSharpCompilation.Create(
            "EmptySwitchConsumer",
            new[] { tree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var enforceAttribute = compilation.GetTypeByMetadataName(
            "Houtamelo.Spire.EnforceExhaustivenessAttribute");
        var diagnostics = await compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new ExhaustivenessAnalyzer()),
                new CompilationWithAnalyzersOptions(
                    new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty),
                    onAnalyzerException: null,
                    concurrentAnalysis: true,
                    logAnalyzerExecutionTime: false,
                    reportSuppressedDiagnostics: true))
            .GetAllDiagnosticsAsync();
        Assert.DoesNotContain(diagnostics, d => d.Id == "AD0001");
        return (compilation, diagnostics, enforceAttribute);
    }

    private static bool IsSpireRuntimeReference(MetadataReference reference)
        => string.Equals(
            Path.GetFileNameWithoutExtension(reference.Display ?? string.Empty),
            "Houtamelo.Spire",
            System.StringComparison.OrdinalIgnoreCase);

    private static MetadataReference EmitProducerReference()
    {
        GeneratorTestHelper.RunGenerator(
            ProducerSource,
            out var compilation,
            out var generatorDiagnostics,
            path: "result-producer.cs");
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        Assert.True(emit.Success,
            string.Join("\n", emit.Diagnostics.Select(d => $"{d.Id}: {d.GetMessage()}")));
        return MetadataReference.CreateFromImage(stream.ToArray());
    }

}
