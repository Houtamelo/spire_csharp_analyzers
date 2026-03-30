using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Houtamelo.Spire;
using Houtamelo.Spire.PatternAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Xunit.Sdk;

namespace Houtamelo.Spire.PatternAnalysis.Tests;

/// Abstract base for exhaustiveness integration tests. Concrete test classes specify Category.
/// Test cases are discovered automatically from Integration/{Category}/cases/*.cs files.
public abstract class ExhaustivenessTestBase
{
    protected abstract string Category { get; }

    private static readonly MetadataReference CoreAssemblyReference =
        MetadataReference.CreateFromFile(typeof(EnforceExhaustivenessAttribute).Assembly.Location);

    private static readonly Lazy<Task<ImmutableArray<MetadataReference>>> CachedReferences =
        new(() => ResolveReferencesAsync());

    [Theory]
    [ExhaustivenessTestDiscovery("exhaustive")]
    public async Task Exhaustive(string caseName)
    {
        var (rawContent, sharedContent) = LoadRaw(caseName);
        var markers = ParseMarkers(rawContent);

        if (markers.Count > 0)
            throw new InvalidOperationException(
                $"File {caseName}.cs is marked exhaustive but contains {markers.Count} //~ marker(s). " +
                "Exhaustive cases must not have //~ markers.");

        var caseSource = StripComments(rawContent);
        var sharedSource = sharedContent != null ? StripComments(sharedContent) : null;
        var (compilation, switchOp) = await BuildAndExtractSwitch(caseSource, sharedSource, caseName);

        var result = RunCheck(compilation, switchOp);

        if (!result.MissingCases.IsEmpty)
        {
            var descriptions = result.MissingCases.Select((mc, i) =>
            {
                var constraints = string.Join(", ",
                    mc.Constraints.Select(c => $"{c.Slot}: {c.Remaining}"));
                return $"  [{i}] {constraints}";
            });

            throw new XunitException(
                $"File {caseName}.cs is marked exhaustive but {result.MissingCases.Length} missing case(s) found:" +
                $"{Environment.NewLine}{string.Join(Environment.NewLine, descriptions)}");
        }
    }

    [Theory]
    [ExhaustivenessTestDiscovery("not_exhaustive")]
    public async Task NotExhaustive(string caseName)
    {
        var (rawContent, sharedContent) = LoadRaw(caseName);
        var markers = ParseMarkers(rawContent);
        var sharedSource = sharedContent != null ? StripComments(sharedContent) : null;
        var isStatement = DetectSwitchStatement(rawContent);

        // Step 0: compile original (all markers as comments, stripped) and verify non-exhaustive
        var caseSource0 = StripComments(rawContent);
        var (compilation0, switchOp0) = await BuildAndExtractSwitch(caseSource0, sharedSource, caseName);
        var result0 = RunCheck(compilation0, switchOp0);

        if (result0.MissingCases.IsEmpty)
        {
            throw new XunitException(
                $"File {caseName}.cs is marked not_exhaustive but the switch was found to be exhaustive.");
        }

        // If no markers, fall back to current behavior (just assert not exhaustive)
        if (markers.Count == 0)
            return;

        // Incremental validation: each marker should reduce missing cases
        var previousCount = result0.MissingCases.Length;

        for (int i = 0; i < markers.Count; i++)
        {
            var augmented = CreateAugmentedSource(rawContent, markers, i, isStatement);
            var strippedAugmented = StripComments(augmented);
            var (compilationI, switchOpI) = await BuildAndExtractSwitch(
                strippedAugmented, sharedSource, $"{caseName} (marker {i})");
            var resultI = RunCheck(compilationI, switchOpI);

            var currentCount = resultI.MissingCases.Length;

            if (currentCount >= previousCount)
            {
                throw new XunitException(
                    $"File {caseName}.cs: after converting marker {i} ('{markers[i].pattern}'), " +
                    $"missing cases did not decrease. Previous: {previousCount}, Current: {currentCount}.");
            }

            if (i < markers.Count - 1)
            {
                // Not the last marker — must still be non-exhaustive
                if (currentCount == 0)
                {
                    throw new XunitException(
                        $"File {caseName}.cs: switch became exhaustive after marker {i} ('{markers[i].pattern}'), " +
                        $"but {markers.Count - i - 1} marker(s) remain. Markers may be redundant.");
                }
            }
            else
            {
                // Last marker — must be exactly exhaustive
                if (currentCount != 0)
                {
                    var descriptions = resultI.MissingCases.Select((mc, j) =>
                    {
                        var constraints = string.Join(", ",
                            mc.Constraints.Select(c => $"{c.Slot}: {c.Remaining}"));
                        return $"  [{j}] {constraints}";
                    });

                    throw new XunitException(
                        $"File {caseName}.cs: after converting all {markers.Count} marker(s), " +
                        $"switch is still not exhaustive. {currentCount} missing case(s) remain:" +
                        $"{Environment.NewLine}{string.Join(Environment.NewLine, descriptions)}");
                }
            }

            previousCount = currentCount;
        }
    }

    /// Extracts //~ markers from raw file content. Returns (lineIndex, pattern) pairs.
    private static List<(int lineIndex, string pattern)> ParseMarkers(string rawContent)
    {
        var result = new List<(int, string)>();
        var lines = rawContent.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimEnd('\r').Trim();
            if (trimmed.StartsWith("//~"))
            {
                var pattern = trimmed.Substring(3).Trim();
                if (pattern.Length > 0)
                    result.Add((i, pattern));
            }
        }
        return result;
    }

    /// Creates augmented source by converting markers [0..convertUpToIndex] to switch arms
    /// and leaving the rest as-is (they'll be stripped as comments).
    private static string CreateAugmentedSource(
        string rawContent,
        List<(int lineIndex, string pattern)> markers,
        int convertUpToIndex,
        bool isStatement)
    {
        var lines = rawContent.Split('\n');
        for (int i = 0; i <= convertUpToIndex; i++)
        {
            var (lineIndex, pattern) = markers[i];
            var originalLine = lines[lineIndex];
            // Preserve leading whitespace
            var leadingWhitespace = originalLine.Length - originalLine.TrimStart().Length;
            var indent = originalLine.Substring(0, leadingWhitespace);

            if (isStatement)
                lines[lineIndex] = $"{indent}case {pattern}: break;";
            else
                lines[lineIndex] = $"{indent}{pattern} => default,";
        }
        return string.Join("\n", lines);
    }

    /// Detects whether the raw source uses a switch statement (vs expression).
    private static bool DetectSwitchStatement(string rawContent)
    {
        // Parse and look for switch statement syntax
        var tree = CSharpSyntaxTree.ParseText(StripComments(rawContent));
        var root = tree.GetRoot();
        return root.DescendantNodes().OfType<SwitchStatementSyntax>().Any();
    }

    private static ExhaustivenessResult RunCheck(CSharpCompilation compilation, IOperation switchOp)
    {
        switch (switchOp)
        {
            case ISwitchExpressionOperation expr:
                return ExhaustivenessChecker.Check(compilation, expr);
            case ISwitchOperation stmt:
                return ExhaustivenessChecker.Check(compilation, stmt);
            default:
                throw new InvalidOperationException(
                    $"Unexpected operation type: {switchOp.GetType().Name}");
        }
    }

    private (string rawContent, string? sharedContent) LoadRaw(string caseName)
    {
        var casesDir = Path.Combine(AppContext.BaseDirectory, "Integration", Category, "cases");
        var sharedPath = Path.Combine(casesDir, "_shared.cs");
        var casePath = Path.Combine(casesDir, $"{caseName}.cs");

        var sharedContent = File.Exists(sharedPath) ? File.ReadAllText(sharedPath) : null;
        var caseContent = File.ReadAllText(casePath);

        // Validate header
        var caseLines = caseContent.Split('\n');
        var firstLine = caseLines.Length > 0 ? caseLines[0].TrimEnd('\r').Trim() : "";
        if (firstLine != "//@ exhaustive" && firstLine != "//@ not_exhaustive")
            throw new InvalidOperationException(
                $"File {caseName}.cs is missing //@ exhaustive or //@ not_exhaustive header on line 1. " +
                $"Found: \"{firstLine}\"");

        return (caseContent, sharedContent);
    }

    private async Task<(CSharpCompilation compilation, IOperation switchOp)> BuildAndExtractSwitch(
        string caseSource, string? sharedSource, string caseName)
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

        // Extract switch operation from case file
        var root = caseTree.GetRoot();
        var semanticModel = compilation.GetSemanticModel(caseTree);

        // Try switch expression first, then switch statement
        var switchExprSyntax = root.DescendantNodes().OfType<SwitchExpressionSyntax>().FirstOrDefault();
        if (switchExprSyntax != null)
        {
            var op = semanticModel.GetOperation(switchExprSyntax);
            if (op is ISwitchExpressionOperation switchExprOp)
                return (compilation, switchExprOp);

            throw new InvalidOperationException(
                $"File {caseName}.cs: found SwitchExpressionSyntax but GetOperation returned " +
                $"{op?.GetType().Name ?? "null"} instead of ISwitchExpressionOperation.");
        }

        var switchStmtSyntax = root.DescendantNodes().OfType<SwitchStatementSyntax>().FirstOrDefault();
        if (switchStmtSyntax != null)
        {
            var op = semanticModel.GetOperation(switchStmtSyntax);
            if (op is ISwitchOperation switchStmtOp)
                return (compilation, switchStmtOp);

            throw new InvalidOperationException(
                $"File {caseName}.cs: found SwitchStatementSyntax but GetOperation returned " +
                $"{op?.GetType().Name ?? "null"} instead of ISwitchOperation.");
        }

        throw new InvalidOperationException(
            $"File {caseName}.cs: no switch expression or switch statement found in the case file.");
    }

    private static async Task<ImmutableArray<MetadataReference>> ResolveReferencesAsync()
    {
        var refs = await ReferenceAssemblies.Net.Net80.ResolveAsync(
            LanguageNames.CSharp, CancellationToken.None);
        return refs.Add(CoreAssemblyReference);
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
}

/// Custom xUnit DataAttribute that discovers exhaustiveness test case files at runtime.
/// Category is extracted from the test class via the Category property on ExhaustivenessTestBase.
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class ExhaustivenessTestDiscoveryAttribute : DataAttribute
{
    private readonly string _expectedHeader;

    public ExhaustivenessTestDiscoveryAttribute(string expectedHeader)
    {
        _expectedHeader = expectedHeader;
    }

    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        var testClass = testMethod.ReflectedType ?? testMethod.DeclaringType
            ?? throw new InvalidOperationException("Could not determine test class");

        var category = ExtractCategory(testClass);
        var casesDir = Path.Combine(AppContext.BaseDirectory, "Integration", category, "cases");

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

    private static string ExtractCategory(Type testClass)
    {
        // Instantiate the test class to read the Category property.
        // The class must have a parameterless constructor (xUnit requirement anyway).
        if (!typeof(ExhaustivenessTestBase).IsAssignableFrom(testClass))
            throw new InvalidOperationException(
                $"Test class '{testClass.Name}' does not inherit from ExhaustivenessTestBase.");

        var instance = (ExhaustivenessTestBase)Activator.CreateInstance(testClass)!;

        // Use reflection to access the protected Category property
        var prop = typeof(ExhaustivenessTestBase).GetProperty(
            "Category", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (string)prop.GetValue(instance)!;
    }
}
