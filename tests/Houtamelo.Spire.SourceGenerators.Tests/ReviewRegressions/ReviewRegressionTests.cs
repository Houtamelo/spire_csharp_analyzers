using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Houtamelo.Spire;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;
using Houtamelo.Spire.CodeFixes;
using Houtamelo.Spire.SourceGenerators.Tests.Unity;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Newtonsoft.Json;
using StjSerializer = System.Text.Json.JsonSerializer;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.ReviewRegressions;

/// Regression coverage for the final variant-payload review findings.
public sealed class ReviewRegressionTests
{
    [Fact]
    public async Task AccessorOwnership_DoesNotExpandFromAnotherVariantParameter()
    {
        GeneratorTestHelper.RunGenerator(
            AccessorParameterCollisionSource,
            out var compilation,
            out var generatorDiagnostics,
            path: "accessor-ownership.cs");
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        var diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new FieldAccessSafetyAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        var fieldDiagnostics = diagnostics
            .Where(diagnostic => diagnostic.Id is "SPIRE013" or "SPIRE014")
            .ToArray();

        var diagnostic = Assert.Single(fieldDiagnostics);
        Assert.Equal("SPIRE013", diagnostic.Id);
        Assert.Contains("a", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void JsonNamesWithQuotesEscapesAndControlsCompileAndRoundTrip()
    {
        var discriminator = "type\"\\\n\t\u0001\u0085\u2028\u2029";
        var variantName = "value\"\\\n\t\u0002\u0085\u2028\u2029";
        var fieldName = "number\"\\\n\t\u0003\u0085\u2028\u2029";
        var source = EscapedJsonSource(
            "EscapedJson",
            discriminator,
            variantName,
            fieldName,
            includeAttributeDiscriminator: true);

        var generated = CompileAndLoad(source);
        Assert.Contains(
            generated.Result.GeneratedTrees,
            tree => Path.GetFileName(tree.FilePath).EndsWith(".Schema.g.cs", StringComparison.Ordinal));

        AssertJsonRoundTrip(
            generated.Assembly,
            "EscapedJson",
            discriminator,
            variantName,
            fieldName);
    }

    [Fact]
    public void GlobalJsonDiscriminatorWithQuotesEscapesAndControlsCompilesAndRoundTrips()
    {
        var discriminator = "global\"\\\n\t\u0004\u0085\u2028\u2029";
        var variantName = "global-value\"\\\n\t\u0005\u0085\u2028\u2029";
        var fieldName = "global-number\"\\\n\t\u0006\u0085\u2028\u2029";
        var source = EscapedJsonSource(
            "GlobalEscapedJson",
            discriminator,
            variantName,
            fieldName,
            includeAttributeDiscriminator: false);

        var generated = CompileAndLoad(
            source,
            new Dictionary<string, string>
            {
                ["build_property.Spire_DU_DefaultJsonDiscriminator"] = discriminator,
            });

        AssertJsonRoundTrip(
            generated.Assembly,
            "GlobalEscapedJson",
            discriminator,
            variantName,
            fieldName);
    }

    [Fact]
    public async Task RecordWildcardExpansion_UsesValidTypeAndPropertyPatterns()
    {
        var source = RecordWildcardSource;
        GeneratorTestHelper.RunGenerator(
            source,
            out var outputCompilation,
            out var generatorDiagnostics,
            path: "record-wildcard.cs");
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(outputCompilation);

        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var solution = workspace.CurrentSolution
            .AddProject(projectId, "RecordWildcardReview", "RecordWildcardReview", LanguageNames.CSharp)
            .WithProjectCompilationOptions(projectId, (CSharpCompilationOptions)outputCompilation.Options)
            .WithProjectParseOptions(projectId, (CSharpParseOptions)outputCompilation.SyntaxTrees.First().Options);
        foreach (var reference in outputCompilation.References)
            solution = solution.AddMetadataReference(projectId, reference);

        DocumentId? userDocumentId = null;
        foreach (var tree in outputCompilation.SyntaxTrees)
        {
            var documentId = DocumentId.CreateNewId(projectId);
            solution = solution.AddDocument(documentId, Path.GetFileName(tree.FilePath), tree.GetText());
            if (tree.FilePath.EndsWith("record-wildcard.cs", StringComparison.Ordinal))
                userDocumentId = documentId;
        }

        Assert.NotNull(userDocumentId);
        var document = solution.GetDocument(userDocumentId!)!;
        var root = await document.GetSyntaxRootAsync();
        Assert.NotNull(root);
        var wildcard = Assert.Single(
            root!.DescendantNodes().OfType<DiscardPatternSyntax>(),
            pattern => pattern.Parent is SwitchExpressionArmSyntax);

        var actions = new List<CodeAction>();
        var context = new CodeRefactoringContext(
            document,
            wildcard.Span,
            action => actions.Add(action),
            CancellationToken.None);
        await new ExpandWildcardRefactoring().ComputeRefactoringsAsync(context);
        var action = Assert.Single(actions);
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var applyOperation = operations.OfType<ApplyChangesOperation>().Single();

        // Inspect the refactoring's immutable result without depending on workspace application.
        var changedDocument = applyOperation.ChangedSolution.GetDocument(userDocumentId!)!;
        var fixedSource = (await changedDocument.GetTextAsync()).ToString();
        Assert.Contains("Result.Count", fixedSource, StringComparison.Ordinal);
        Assert.Contains("Result.Text", fixedSource, StringComparison.Ordinal);
        Assert.Contains("Value", fixedSource, StringComparison.Ordinal);
        Assert.Contains("Message", fixedSource, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(fixedSource, "=> \"other\""));

        GeneratorTestHelper.RunGenerator(
            fixedSource,
            out var fixedCompilation,
            out var fixedGeneratorDiagnostics,
            path: "record-wildcard.cs");
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(fixedGeneratorDiagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(fixedCompilation);
    }

    [Fact]
    public async Task NullablePayloadPositionalFix_UsesVarAndCompiles()
    {
        GeneratorTestHelper.RunGenerator(
            NullablePayloadPositionalSource,
            out var outputCompilation,
            out var generatorDiagnostics,
            path: "nullable-positional.cs");
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);

        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var solution = workspace.CurrentSolution
            .AddProject(projectId, "NullablePositionalReview", "NullablePositionalReview", LanguageNames.CSharp)
            .WithProjectCompilationOptions(projectId, (CSharpCompilationOptions)outputCompilation.Options)
            .WithProjectParseOptions(projectId, (CSharpParseOptions)outputCompilation.SyntaxTrees.First().Options);
        foreach (var reference in outputCompilation.References)
            solution = solution.AddMetadataReference(projectId, reference);

        DocumentId? userDocumentId = null;
        foreach (var tree in outputCompilation.SyntaxTrees)
        {
            var documentId = DocumentId.CreateNewId(projectId);
            solution = solution.AddDocument(documentId, Path.GetFileName(tree.FilePath), tree.GetText());
            if (tree.FilePath.EndsWith("nullable-positional.cs", StringComparison.Ordinal))
                userDocumentId = documentId;
        }

        Assert.NotNull(userDocumentId);
        var project = solution.GetProject(projectId)!;
        var compilation = (await project.GetCompilationAsync())!;
        var diagnostics = await compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new TypeSafetyAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
        var diagnostic = Assert.Single(diagnostics.Where(item => item.Id == "SPIRE011"));
        Assert.Equal("int?", diagnostic.Properties["ExpectedType"]);

        var document = solution.GetDocument(userDocumentId!)!;
        var actions = new List<CodeAction>();
        var context = new CodeFixContext(
            document,
            diagnostic,
            (action, _) => actions.Add(action),
            CancellationToken.None);
        await new FixFieldTypeCodeFix().RegisterCodeFixesAsync(context);
        var action = Assert.Single(actions);
        var operations = await action.GetOperationsAsync(CancellationToken.None);
        var applyOperation = operations.OfType<ApplyChangesOperation>().Single();

        // Inspect the code action's immutable result without depending on workspace application.
        var changedDocument = applyOperation.ChangedSolution.GetDocument(userDocumentId!)!;
        var fixedSource = (await changedDocument.GetTextAsync()).ToString();
        var fixedRoot = CSharpSyntaxTree.ParseText(fixedSource).GetRoot();
        Assert.Single(
            fixedRoot.DescendantNodes().OfType<VarPatternSyntax>(),
            pattern => pattern.Designation.ToString() == "value");
        Assert.DoesNotContain(
            fixedRoot.DescendantNodes().OfType<DeclarationPatternSyntax>(),
            pattern => pattern.Type.ToString() == "int?" && pattern.Designation.ToString() == "value");

        GeneratorTestHelper.RunGenerator(
            fixedSource,
            out var fixedCompilation,
            out var fixedGeneratorDiagnostics,
            path: "nullable-positional.cs");
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(fixedGeneratorDiagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(fixedCompilation);
    }

    private static void AssertJsonRoundTrip(
        Assembly assembly,
        string unionName,
        string discriminator,
        string variantName,
        string fieldName)
    {
        var unionType = assembly.GetType(unionName, throwOnError: true)!;
        var value = unionType.GetMethod("Value", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, new object[] { 37 })!;

        var stjJson = StjSerializer.Serialize(value, unionType);
        using (var stjDocument = JsonDocument.Parse(stjJson))
        {
            var root = stjDocument.RootElement;
            Assert.Equal(variantName, root.GetProperty(discriminator).GetString());
            Assert.Equal(37, root.GetProperty(fieldName).GetInt32());
        }

        var stjRestored = StjSerializer.Deserialize(stjJson, unionType)!;
        Assert.Equal(37, ReadPayloadField(stjRestored, "value", "number"));

        var nsjJson = JsonConvert.SerializeObject(value);
        var nsjRoot = Newtonsoft.Json.Linq.JObject.Parse(nsjJson);
        Assert.Equal(variantName, (string?)nsjRoot[discriminator]);
        Assert.Equal(37, (int?)nsjRoot[fieldName]);

        var nsjRestored = JsonConvert.DeserializeObject(nsjJson, unionType)!;
        Assert.Equal(37, ReadPayloadField(nsjRestored, "value", "number"));
    }

    private static object? ReadPayloadField(object union, string accessorName, string fieldName)
    {
        var payload = union.GetType().GetProperty(accessorName)!.GetValue(union)!;
        return payload.GetType().GetProperty(fieldName)!.GetValue(payload);
    }

    private static (Assembly Assembly, GeneratorDriverRunResult Result) CompileAndLoad(
        string source,
        Dictionary<string, string>? globalOptions = null)
    {
        var references = TestHarnessEnvironment.RuntimeReferences
            .Append(MetadataReference.CreateFromFile(
                typeof(JsonConvert).Assembly.Location))
            .Append(MetadataReference.CreateFromFile(
                typeof(StjSerializer).Assembly.Location))
            .Distinct(new MetadataReferenceComparer())
            .ToArray();
        var tree = CSharpSyntaxTree.ParseText(
            source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
            path: "json-review.cs");
        var compilation = CSharpCompilation.Create(
            "ReviewRegressionJson",
            new[] { tree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver;
        if (globalOptions is null)
        {
            driver = CSharpGeneratorDriver.Create(new DiscriminatedUnionGenerator());
        }
        else
        {
            driver = CSharpGeneratorDriver.Create(
                generators: new[] { new DiscriminatedUnionGenerator().AsSourceGenerator() },
                optionsProvider: new GlobalConfig.TestAnalyzerConfigOptionsProvider(globalOptions));
        }

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics);
        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(outputCompilation);

        using var stream = new MemoryStream();
        var emit = outputCompilation.Emit(stream);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        return (TestHarnessEnvironment.LoadAssembly(stream.ToArray()), driver.GetRunResult());
    }

    private static string EscapedJsonSource(
        string unionName,
        string discriminator,
        string variantName,
        string fieldName,
        bool includeAttributeDiscriminator)
    {
        var discriminatorArgument = includeAttributeDiscriminator
            ? $", jsonDiscriminator: {CSharpStringLiteral(discriminator)}"
            : string.Empty;
        return $$"""
            using Houtamelo.Spire;
            [DiscriminatedUnion(
                json: JsonLibrary.SystemTextJson | JsonLibrary.NewtonsoftJson{{discriminatorArgument}})]
            public partial struct {{unionName}}
            {
                [Variant, JsonName({{CSharpStringLiteral(variantName)}})]
                public static partial {{unionName}} Value(
                    [JsonName({{CSharpStringLiteral(fieldName)}})] int number);
            }
            """;
    }

    private static string CSharpStringLiteral(string value)
    {
        var builder = new StringBuilder(value.Length + 2);
        builder.Append('"');
        foreach (var character in value)
        {
            switch (character)
            {
                case '\\': builder.Append("\\\\"); break;
                case '"': builder.Append("\\\""); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                case '\0': builder.Append("\\0"); break;
                case '\u0085': builder.Append("\\u0085"); break;
                case '\u2028': builder.Append("\\u2028"); break;
                case '\u2029': builder.Append("\\u2029"); break;
                default:
                    if (character < ' ')
                        builder.Append("\\u").Append(((int)character).ToString("X4"));
                    else
                        builder.Append(character);
                    break;
            }
        }
        return builder.Append('"').ToString();
    }

    private static int CountOccurrences(string value, string needle)
    {
        var count = 0;
        var offset = 0;
        while ((offset = value.IndexOf(needle, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += needle.Length;
        }
        return count;
    }

    private static string AccessorParameterCollisionSource => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct Collision
        {
            [Variant] public static partial Collision A(int value);
            [Variant] public static partial Collision B(int a);
        }
        static class Reader
        {
            public static int Valid(Collision value)
            {
                if (value.kind == Collision.Kind.A)
                    return value.a.value;
                return Collision.A(1).a.value;
            }

            public static int Wrong(Collision value)
            {
                if (value.kind == Collision.Kind.B)
                    return value.a.value;
                return 0;
            }
        }
        """;

    private static string RecordWildcardSource => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public abstract partial record Result
        {
            public sealed partial record Count(int Value) : Result;
            public sealed partial record Text(string Message) : Result;
        }
        static class Reader
        {
            public static string Describe(Result result)
            {
                var value = "outer";
                return result switch
                {
                    _ => "other",
                };
            }
        }
        """;

    private static string NullablePayloadPositionalSource => """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct NullablePair
        {
            [Variant] public static partial NullablePair Data(int? value, string label);
            [Variant] public static partial NullablePair Empty();
        }
        static class Reader
        {
            public static int Read(NullablePair pair) => pair switch
            {
                { kind: NullablePair.Kind.Data, data: (string value, string label) } =>
                    value.GetValueOrDefault() + label.Length,
                { kind: NullablePair.Kind.Empty } => 0,
            };
        }
        """;

    private sealed class MetadataReferenceComparer : IEqualityComparer<MetadataReference>
    {
        public bool Equals(MetadataReference? left, MetadataReference? right)
            => string.Equals(left?.Display, right?.Display, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(MetadataReference reference)
            => StringComparer.OrdinalIgnoreCase.GetHashCode(reference.Display ?? string.Empty);
    }
}
