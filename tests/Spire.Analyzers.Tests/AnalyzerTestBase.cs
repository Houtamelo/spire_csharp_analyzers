using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Xunit.Sdk;

namespace Spire.Analyzers.Tests;

/// <summary>
/// Abstract base class for analyzer tests. Concrete test classes only need to specify the RuleId.
/// Test cases are discovered automatically from {RuleId}/cases/*.cs files.
/// </summary>
public abstract class AnalyzerTestBase<TAnalyzer> where TAnalyzer : DiagnosticAnalyzer, new()
{
    protected abstract string RuleId { get; }

    private static readonly MetadataReference AnalyzerAssemblyReference =
        MetadataReference.CreateFromFile(typeof(TAnalyzer).Assembly.Location);

    private static readonly MetadataReference CoreAssemblyReference =
        MetadataReference.CreateFromFile(typeof(Spire.MustBeInitAttribute).Assembly.Location);

    private static readonly Lazy<Task<ImmutableArray<MetadataReference>>> CachedReferences =
        new(() => ResolveReferencesAsync());

    [Theory]
    [TestCaseDiscovery("should_fail")]
    public async Task ShouldFail(string caseName)
    {
        var (caseSource, sharedSource, expectedLines) = LoadAndParse(caseName);

        if (expectedLines.Count == 0)
            throw new InvalidOperationException(
                $"File {caseName}.cs is marked should_fail but has no error markers");

        var diagnostics = await GetAnalyzerDiagnosticsAsync(caseSource, sharedSource);
        var ruleDiagnostics = diagnostics.Where(d => d.Id == RuleId).ToList();

        var diagnosticLines = ruleDiagnostics
            .Select(d => d.Location.GetLineSpan().StartLinePosition.Line + 1)
            .ToHashSet();

        var unmatchedMarkers = expectedLines.Where(l => !diagnosticLines.Contains(l)).ToList();
        if (unmatchedMarkers.Count > 0)
        {
            throw new XunitException(
                $"File {caseName}.cs: error markers on line(s) {FormatLines(unmatchedMarkers)} produced no diagnostics. " +
                $"Expected diagnostics on {FormatLines(expectedLines)}, got on {FormatLines(diagnosticLines)}.");
        }

        var unexpectedDiags = diagnosticLines.Except(expectedLines).ToList();
        if (unexpectedDiags.Count > 0)
        {
            throw new XunitException(
                $"File {caseName}.cs: unexpected diagnostics on line(s) {FormatLines(unexpectedDiags)} " +
                $"that are not marked with //~ ERROR.");
        }
    }

    [Theory]
    [TestCaseDiscovery("should_pass")]
    public async Task ShouldPass(string caseName)
    {
        var (caseSource, sharedSource, expectedLines) = LoadAndParse(caseName);

        if (expectedLines.Count > 0)
            throw new InvalidOperationException(
                $"File {caseName}.cs is marked should_pass but contains error markers on line(s) {FormatLines(expectedLines)}");

        var diagnostics = await GetAnalyzerDiagnosticsAsync(caseSource, sharedSource);
        var ruleDiagnostics = diagnostics.Where(d => d.Id == RuleId).ToList();

        if (ruleDiagnostics.Count > 0)
        {
            var lines = ruleDiagnostics
                .Select(d => d.Location.GetLineSpan().StartLinePosition.Line + 1);
            throw new XunitException(
                $"File {caseName}.cs is marked should_pass but {ruleDiagnostics.Count} diagnostic(s) " +
                $"were reported on line(s): {FormatLines(lines)}");
        }
    }

    private (string caseSource, string? sharedSource, List<int> expectedDiagnosticLines) LoadAndParse(string caseName)
    {
        var casesDir = Path.Combine(AppContext.BaseDirectory, RuleId, "cases");
        var sharedPath = Path.Combine(casesDir, "_shared.cs");
        var casePath = Path.Combine(casesDir, $"{caseName}.cs");

        var sharedContent = File.Exists(sharedPath) ? File.ReadAllText(sharedPath) : null;
        var caseContent = File.ReadAllText(casePath);

        // Parse header (line 1)
        var caseLines = caseContent.Split('\n');
        var firstLine = caseLines.Length > 0 ? caseLines[0].TrimEnd('\r').Trim() : "";
        if (firstLine != "//@ should_fail" && firstLine != "//@ should_pass")
            throw new InvalidOperationException(
                $"File {caseName}.cs is missing //@ should_fail or //@ should_pass header on line 1. " +
                $"Found: \"{firstLine}\"");

        // Parse error markers — line numbers are relative to the case file (no offset needed)
        var expectedLines = new List<int>();
        for (int i = 0; i < caseLines.Length; i++)
        {
            var lineNumber = i + 1; // 1-based within case file
            var marker = ParseErrorMarker(caseLines[i]);

            if (marker == MarkerKind.ThisLine)
                expectedLines.Add(lineNumber);
            else if (marker == MarkerKind.PreviousLine)
                expectedLines.Add(lineNumber - 1);
        }

        // Strip comments from each file independently
        var strippedCase = StripComments(caseContent);
        var strippedShared = sharedContent != null ? StripComments(sharedContent) : null;

        return (strippedCase, strippedShared, expectedLines);
    }

    private enum MarkerKind { None, ThisLine, PreviousLine }

    private static MarkerKind ParseErrorMarker(string line)
    {
        var idx = line.IndexOf("//~", StringComparison.Ordinal);
        if (idx < 0) return MarkerKind.None;

        var pos = idx + 3;

        if (pos < line.Length && line[pos] == '^')
        {
            pos++;
            var rest = pos < line.Length ? line.Substring(pos).TrimStart() : "";
            return rest.StartsWith("ERROR", StringComparison.Ordinal)
                ? MarkerKind.PreviousLine
                : MarkerKind.None;
        }
        else
        {
            var rest = pos < line.Length ? line.Substring(pos).TrimStart() : "";
            return rest.StartsWith("ERROR", StringComparison.Ordinal)
                ? MarkerKind.ThisLine
                : MarkerKind.None;
        }
    }

    private static string StripComments(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetRoot();
        var chars = source.ToCharArray();

        foreach (var trivia in root.DescendantTrivia())
        {
            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
            {
                BlankSpan(chars, trivia.Span);
            }
            else if (trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                     trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            {
                BlankSpan(chars, trivia.FullSpan);
            }
        }

        return new string(chars);
    }

    private static void BlankSpan(char[] chars, Microsoft.CodeAnalysis.Text.TextSpan span)
    {
        for (int i = span.Start; i < span.End && i < chars.Length; i++)
        {
            if (chars[i] != '\n' && chars[i] != '\r')
                chars[i] = ' ';
        }
    }

    private static async Task<ImmutableArray<MetadataReference>> ResolveReferencesAsync()
    {
        var refs = await ReferenceAssemblies.Net.Net80.ResolveAsync(
            LanguageNames.CSharp, CancellationToken.None);
        return refs.Add(AnalyzerAssemblyReference).Add(CoreAssemblyReference);
    }

    private static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(
        string caseSource, string? sharedSource)
    {
        var references = await CachedReferences.Value;
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);

        var caseTree = CSharpSyntaxTree.ParseText(caseSource, parseOptions, path: "case.cs");

        var trees = new List<SyntaxTree> { caseTree };
        if (sharedSource != null)
            trees.Add(CSharpSyntaxTree.ParseText(sharedSource, parseOptions, path: "_shared.cs"));

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            trees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Fail fast if the test code doesn't compile
        var compilerErrors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        if (compilerErrors.Count > 0)
        {
            var errorMessages = string.Join(Environment.NewLine,
                compilerErrors.Select(d =>
                {
                    var span = d.Location.GetLineSpan();
                    return $"  {span.Path}({span.StartLinePosition.Line + 1}): {d.Id}: {d.GetMessage()}";
                }));

            throw new InvalidOperationException(
                $"Test code has compilation errors:{Environment.NewLine}{errorMessages}");
        }

        var analyzer = new TAnalyzer();
        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(analyzer));

        var allDiagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();

        // Only return diagnostics from the case file, not from _shared.cs
        return allDiagnostics
            .Where(d => d.Location.SourceTree == caseTree || d.Location == Location.None)
            .ToImmutableArray();
    }

    private static string FormatLines(IEnumerable<int> lines)
        => string.Join(", ", lines.OrderBy(l => l));
}

/// <summary>
/// Custom xUnit DataAttribute that discovers test case files at runtime.
/// Extracts the rule ID from the test class name convention: {RuleId}Tests.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class TestCaseDiscoveryAttribute : DataAttribute
{
    private readonly string _expectedHeader;

    public TestCaseDiscoveryAttribute(string expectedHeader)
    {
        _expectedHeader = expectedHeader;
    }

    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        var testClass = testMethod.ReflectedType ?? testMethod.DeclaringType
            ?? throw new InvalidOperationException("Could not determine test class");

        var ruleId = ExtractRuleId(testClass.Name);
        var casesDir = Path.Combine(AppContext.BaseDirectory, ruleId, "cases");

        if (!Directory.Exists(casesDir))
            yield break;

        var headerKind = $"//@ {_expectedHeader}";

        foreach (var file in Directory.GetFiles(casesDir, "*.cs").OrderBy(f => f))
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            if (fileName == "_shared") continue;

            using var reader = new StreamReader(file);
            var firstLine = reader.ReadLine()?.Trim() ?? "";
            if (firstLine == headerKind)
            {
                yield return new object[] { fileName };
            }
        }
    }

    private static string ExtractRuleId(string className)
    {
        // Handle generic base class name like "AnalyzerTestBase`1" — skip it
        // The test class name follows {RuleId}Tests convention
        if (className.EndsWith("Tests", StringComparison.Ordinal))
            return className.Substring(0, className.Length - 5);

        throw new InvalidOperationException(
            $"Test class name '{className}' does not follow the '{{RuleId}}Tests' convention. " +
            $"Expected name ending with 'Tests'.");
    }
}
