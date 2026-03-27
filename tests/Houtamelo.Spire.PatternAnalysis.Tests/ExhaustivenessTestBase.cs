using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Houtamelo.Spire.Core;
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
        var (caseSource, sharedSource) = LoadAndParse(caseName);
        var (compilation, switchOp) = await BuildAndExtractSwitch(caseSource, sharedSource, caseName);

        ExhaustivenessResult result;
        switch (switchOp)
        {
            case ISwitchExpressionOperation expr:
                result = ExhaustivenessChecker.Check(compilation, expr);
                break;
            case ISwitchOperation stmt:
                result = ExhaustivenessChecker.Check(compilation, stmt);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unexpected operation type: {switchOp.GetType().Name}");
        }

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
        var (caseSource, sharedSource) = LoadAndParse(caseName);
        var (compilation, switchOp) = await BuildAndExtractSwitch(caseSource, sharedSource, caseName);

        ExhaustivenessResult result;
        switch (switchOp)
        {
            case ISwitchExpressionOperation expr:
                result = ExhaustivenessChecker.Check(compilation, expr);
                break;
            case ISwitchOperation stmt:
                result = ExhaustivenessChecker.Check(compilation, stmt);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unexpected operation type: {switchOp.GetType().Name}");
        }

        if (result.MissingCases.IsEmpty)
        {
            throw new XunitException(
                $"File {caseName}.cs is marked not_exhaustive but the switch was found to be exhaustive.");
        }
    }

    private (string caseSource, string? sharedSource) LoadAndParse(string caseName)
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

        var strippedCase = StripComments(caseContent);
        var strippedShared = sharedContent != null ? StripComments(sharedContent) : null;

        return (strippedCase, strippedShared);
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
