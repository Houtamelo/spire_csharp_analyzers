using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using Xunit.Sdk;

namespace Houtamelo.Spire.SourceGenerators.Tests;

/// <summary>
/// Abstract base class for generator diagnostic tests. Concrete test classes only need
/// to specify the Category. Test cases are discovered automatically from {Category}/cases/*.cs files.
/// </summary>
public abstract class GeneratorDiagnosticTestBase
{
    protected abstract string Category { get; }

    protected virtual string DiagnosticPrefix => "SPIRE_DU";

    protected virtual void RunGenerator(
        string source,
        out ImmutableArray<RoslynDiagnostic> diagnostics)
    {
        GeneratorTestHelper.RunGenerator(source, out _, out diagnostics, path: "case.cs");
    }

    protected virtual bool IsRelevantDiagnostic(RoslynDiagnostic d)
        => d.Id.StartsWith(DiagnosticPrefix, StringComparison.Ordinal);

    [Theory]
    [GeneratorDiagnosticCaseDiscovery("should_fail")]
    public void ShouldFail(string caseName)
    {
        var (caseSource, expectedLines) = LoadAndParse(caseName);

        if (expectedLines.Count == 0)
            throw new InvalidOperationException(
                $"File {caseName}.cs is marked should_fail but has no error markers");

        var strippedSource = StripComments(caseSource);

        RunGenerator(strippedSource, out var diagnostics);

        var relevantDiagnostics = diagnostics
            .Where(IsRelevantDiagnostic)
            .ToList();

        var diagnosticLines = relevantDiagnostics
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
    [GeneratorDiagnosticCaseDiscovery("should_pass")]
    public void ShouldPass(string caseName)
    {
        var (caseSource, expectedLines) = LoadAndParse(caseName);

        if (expectedLines.Count > 0)
            throw new InvalidOperationException(
                $"File {caseName}.cs is marked should_pass but contains error markers on line(s) {FormatLines(expectedLines)}");

        var strippedSource = StripComments(caseSource);

        RunGenerator(strippedSource, out var diagnostics);

        var relevantDiagnostics = diagnostics
            .Where(IsRelevantDiagnostic)
            .ToList();

        if (relevantDiagnostics.Count > 0)
        {
            var lines = relevantDiagnostics
                .Select(d => d.Location.GetLineSpan().StartLinePosition.Line + 1);
            throw new XunitException(
                $"File {caseName}.cs is marked should_pass but {relevantDiagnostics.Count} diagnostic(s) " +
                $"were reported on line(s): {FormatLines(lines)}");
        }
    }

    private (string caseSource, List<int> expectedDiagnosticLines) LoadAndParse(string caseName)
    {
        var casesDir = Path.Combine(AppContext.BaseDirectory, Category, "cases");
        var casePath = Path.Combine(casesDir, $"{caseName}.cs");

        var caseContent = File.ReadAllText(casePath);

        // Parse header (line 1)
        var caseLines = caseContent.Split('\n');
        var firstLine = caseLines.Length > 0 ? caseLines[0].TrimEnd('\r').Trim() : "";
        if (firstLine != "//@ should_fail" && firstLine != "//@ should_pass")
            throw new InvalidOperationException(
                $"File {caseName}.cs is missing //@ should_fail or //@ should_pass header on line 1. " +
                $"Found: \"{firstLine}\"");

        // Parse error markers — line numbers are relative to the case file
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

        return (caseContent, expectedLines);
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

    private static string FormatLines(IEnumerable<int> lines)
        => string.Join(", ", lines.OrderBy(l => l));
}

/// <summary>
/// Custom xUnit DataAttribute that discovers generator diagnostic test case files at runtime.
/// Extracts the category from the test class name convention: {Category}Tests.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class GeneratorDiagnosticCaseDiscoveryAttribute : DataAttribute
{
    private readonly string _expectedHeader;

    public GeneratorDiagnosticCaseDiscoveryAttribute(string expectedHeader)
    {
        _expectedHeader = expectedHeader;
    }

    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        var testClass = testMethod.ReflectedType ?? testMethod.DeclaringType
            ?? throw new InvalidOperationException("Could not determine test class");

        var category = ResolveCategory(testClass);
        var casesDir = Path.Combine(AppContext.BaseDirectory, category, "cases");

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

    private static string ResolveCategory(Type testClass)
    {
        // Prefer the instance's Category property (supports paths with slashes).
        try
        {
            var ctor = testClass.GetConstructor(Type.EmptyTypes);
            if (ctor is not null)
            {
                var instance = ctor.Invoke(null);
                var prop = testClass.GetProperty("Category",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (prop is not null)
                {
                    var value = prop.GetValue(instance) as string;
                    if (!string.IsNullOrEmpty(value))
                        return value!;
                }
            }
        }
        catch
        {
            // Fall through to name-based convention.
        }

        return ExtractCategory(testClass.Name);
    }

    private static string ExtractCategory(string className)
    {
        if (className.EndsWith("Tests", StringComparison.Ordinal))
            return className.Substring(0, className.Length - 5);

        throw new InvalidOperationException(
            $"Test class name '{className}' does not follow the '{{Category}}Tests' convention. " +
            $"Expected name ending with 'Tests'.");
    }
}
