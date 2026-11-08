using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;
using Xunit.Sdk;

namespace Houtamelo.Spire.SourceGenerators.Tests.FieldAccess;

internal static class GuardFlowTestHelper
{
    public static async Task CheckAsync(string annotatedSource)
    {
        var (source, expected) = ParseMarkup(annotatedSource);
        GeneratorTestHelper.RunGenerator(
            source,
            out var outputCompilation,
            out var generatorDiagnostics,
            path: "guard-flow.cs");

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(outputCompilation);

        using var emitted = new MemoryStream();
        var emitResult = outputCompilation.Emit(emitted);
        Assert.True(
            emitResult.Success,
            "Generated guard-flow compilation could not be emitted:\n" +
            string.Join("\n", emitResult.Diagnostics));

        using var analyzerTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var diagnostics = await outputCompilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(
                new FieldAccessSafetyAnalyzer()))
            .GetAnalyzerDiagnosticsAsync(analyzerTimeout.Token);

        var unexpected = diagnostics
            .Where(diagnostic => diagnostic.Id is not "SPIRE013" and not "SPIRE014")
            .ToArray();
        Assert.True(
            unexpected.Length == 0,
            "Unexpected analyzer diagnostics:\n" +
            string.Join(
                "\n",
                unexpected.Select(diagnostic =>
                    diagnostic.Id + ": " + diagnostic.GetMessage())));

        var actual = diagnostics
            .Where(diagnostic => diagnostic.Id is "SPIRE013" or "SPIRE014")
            .OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start)
            .ThenBy(diagnostic => diagnostic.Id, StringComparer.Ordinal)
            .ToArray();

        if (expected.Length != actual.Length)
        {
            var expectedDescription = string.Join(
                "; ",
                expected.Select(diagnostic =>
                    diagnostic.Id + "@" + diagnostic.Start + ":" + diagnostic.Length +
                    " '" + diagnostic.Text + "'"));
            var actualDescription = string.Join(
                "; ",
                actual.Select(diagnostic =>
                {
                    var span = diagnostic.Location.SourceSpan;
                    var text = diagnostic.Location.SourceTree?.GetText().ToString(span) ?? "<no-source>";
                    return diagnostic.Id + "@" + span.Start + ":" + span.Length +
                        " '" + text + "' " + diagnostic.GetMessage();
                }));
            Assert.Fail(
                "Guard-flow diagnostic count mismatch.\n" +
                "Expected: " + expectedDescription + "\n" +
                "Actual: " + actualDescription);
        }

        for (var index = 0; index < expected.Length; index++)
        {
            var expectedDiagnostic = expected[index];
            var actualDiagnostic = actual[index];
            Assert.Equal(expectedDiagnostic.Id, actualDiagnostic.Id);
            Assert.Equal(expectedDiagnostic.Severity, actualDiagnostic.Severity);
            Assert.True(actualDiagnostic.Location.IsInSource);
            Assert.Equal(expectedDiagnostic.Start, actualDiagnostic.Location.SourceSpan.Start);
            Assert.Equal(expectedDiagnostic.Length, actualDiagnostic.Location.SourceSpan.Length);
            Assert.Equal(
                expectedDiagnostic.Text,
                actualDiagnostic.Location.SourceTree!.GetText().ToString(actualDiagnostic.Location.SourceSpan));
        }
    }

    public static string CommandSource(string consumer, bool threeVariants = true)
    {
        var idle = threeVariants
            ? "                [Variant] public static partial Command Idle();\n"
            : string.Empty;
        return """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct Command
            {
                [Variant] public static partial Command Move(int destination, float speed);
                [Variant] public static partial Command Stop();
            """ + idle + """
            }

            """ + consumer;
    }

    public static string SingletonSource(string consumer)
        => """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct SingleCommand
            {
                [Variant] public static partial SingleCommand Only();
            }

            """ + consumer;

    private static (string Source, ImmutableArray<ExpectedDiagnostic> Expected) ParseMarkup(
        string annotatedSource)
    {
        var clean = new System.Text.StringBuilder(annotatedSource.Length);
        var expected = ImmutableArray.CreateBuilder<ExpectedDiagnostic>();
        var sourceIndex = 0;
        while (sourceIndex < annotatedSource.Length)
        {
            var open = annotatedSource.IndexOf("[|", sourceIndex, StringComparison.Ordinal);
            if (open < 0)
            {
                clean.Append(annotatedSource, sourceIndex, annotatedSource.Length - sourceIndex);
                break;
            }

            clean.Append(annotatedSource, sourceIndex, open - sourceIndex);
            var idEnd = annotatedSource.IndexOf(':', open + 2);
            var close = idEnd < 0
                ? -1
                : annotatedSource.IndexOf("|]", idEnd + 1, StringComparison.Ordinal);
            if (idEnd < 0 || close < 0)
                throw new XunitException("Malformed guard-flow diagnostic markup.");

            var id = annotatedSource.Substring(open + 2, idEnd - open - 2);
            var text = annotatedSource.Substring(idEnd + 1, close - idEnd - 1);
            if (id is not "SPIRE013" and not "SPIRE014" || text.Length == 0)
                throw new XunitException("Guard-flow markup must identify SPIRE013 or SPIRE014 and a non-empty span.");

            var start = clean.Length;
            clean.Append(text);
            expected.Add(new ExpectedDiagnostic(
                id,
                id == "SPIRE013" ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning,
                start,
                text.Length,
                text));
            sourceIndex = close + 2;
        }

        return (clean.ToString(), expected.ToImmutable());
    }

    private readonly record struct ExpectedDiagnostic(
        string Id,
        DiagnosticSeverity Severity,
        int Start,
        int Length,
        string Text);
}
