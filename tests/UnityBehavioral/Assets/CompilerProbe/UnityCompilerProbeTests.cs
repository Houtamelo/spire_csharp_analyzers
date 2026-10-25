#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Houtamelo.Spire.Analyzers.Rules;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;

namespace Spire.UnityCompilerProbeTests;

public sealed class UnityCompilerProbeTests
{
    private static readonly ImmutableArray<MetadataReference> BasicReferences = CreateBasicReferences();

    [Test]
    public void CSharpCompilation_EmitsAndExecutesOnMono()
    {
        const string source = """
            public static class ProbeAnswer
            {
                public static int Get() => 42;
            }
            """;

        var tree = Parse(source, "answer.cs");
        var compilation = CreateCompilation("CompilerProbe.Answer", tree);
        Assert.That(compilation.GetDiagnostics(), Is.Empty);

        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        Assert.That(emit.Success, Is.True, FormatDiagnostics(emit.Diagnostics));

        var assembly = Assembly.Load(stream.ToArray());
        var answer = assembly.GetType("ProbeAnswer")!
            .GetMethod("Get", BindingFlags.Public | BindingFlags.Static)!;
        Assert.That(answer.Invoke(null, null), Is.EqualTo(42));
    }

    [Test]
    public void SPIRE001_AnalyzerReportsUnsafeArrayAndAcceptsInitializedControl()
    {
        const string source = """
            using Houtamelo.Spire;

            [EnforceInitialization]
            public struct RequiresInitialization
            {
                public int Value;
            }

            public static class ArrayProbe
            {
                public static RequiresInitialization[] Bad = new RequiresInitialization[2];
                public static RequiresInitialization[] Good =
                [
                    new RequiresInitialization { Value = 1 },
                ];
            }
            """;

        var diagnostics = RunAnalyzer(
            source,
            new SPIRE001ArrayOfEnforceInitializationStructAnalyzer());
        var spireDiagnostics = diagnostics.Where(diagnostic => diagnostic.Id == "SPIRE001").ToArray();

        Assert.That(spireDiagnostics, Has.Length.EqualTo(1));
        Assert.That(spireDiagnostics[0].GetMessage(), Does.Contain("RequiresInitialization"));
        Assert.That(
            diagnostics.Any(diagnostic => diagnostic.Id == "SPIRE001" &&
                diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1 == 11),
            Is.True);
    }

    [Test]
    public void DiscriminatedUnionGenerator_EmitsAssemblyAndFactoryValue()
    {
        const string source = """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct ProbeUnion
            {
                [Variant] public static partial ProbeUnion Some(int value);
                [Variant] public static partial ProbeUnion None();
            }
            """;

        var tree = Parse(source, "union.cs");
        var compilation = CreateCompilation("CompilerProbe.Union", tree);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[] { new DiscriminatedUnionGenerator().AsSourceGenerator() },
            parseOptions: ParseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var generatorDiagnostics);

        Assert.That(generatorDiagnostics, Is.Empty, FormatDiagnostics(generatorDiagnostics));
        Assert.That(outputCompilation.GetDiagnostics(), Is.Empty, FormatDiagnostics(outputCompilation.GetDiagnostics()));

        using var stream = new MemoryStream();
        var emit = outputCompilation.Emit(stream);
        Assert.That(emit.Success, Is.True, FormatDiagnostics(emit.Diagnostics));

        var assembly = Assembly.Load(stream.ToArray());
        var unionType = assembly.GetType("ProbeUnion")!;
        var factory = unionType.GetMethod("Some", BindingFlags.Public | BindingFlags.Static)!;
        var value = factory.Invoke(null, new object[] { 42 })!;

        Assert.That(unionType.GetProperty("kind")?.GetValue(value)?.ToString(), Is.EqualTo("Some"));
        var payload = GetMemberValue(unionType, value, "some")!;
        Assert.That(GetMemberValue(payload.GetType(), payload, "value"), Is.EqualTo(42));
    }

    [Test]
    public void ExhaustivenessAnalyzer_UsesPatternAnalysisOnGeneratedUnion()
    {
        const string source = """
            using Houtamelo.Spire;

            [DiscriminatedUnion]
            public partial struct ExhaustiveProbeUnion
            {
                [Variant] public static partial ExhaustiveProbeUnion Some(int value);
                [Variant] public static partial ExhaustiveProbeUnion None();
            }

            public static class ExhaustiveProbeUse
            {
                public static int Match(ExhaustiveProbeUnion value) => value switch
                {
                    { kind: ExhaustiveProbeUnion.Kind.Some } => 1,
                };
            }
            """;

        var tree = Parse(source, "exhaustiveness.cs");
        var compilation = CreateCompilation("CompilerProbe.Exhaustiveness", tree);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[] { new DiscriminatedUnionGenerator().AsSourceGenerator() },
            parseOptions: ParseOptions);
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var generatorDiagnostics);
        Assert.That(generatorDiagnostics, Is.Empty, FormatDiagnostics(generatorDiagnostics));

        var diagnostics = outputCompilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new ExhaustivenessAnalyzer()))
            .GetAnalyzerDiagnosticsAsync()
            .GetAwaiter()
            .GetResult();

        Assert.That(diagnostics.Count(diagnostic => diagnostic.Id == "SPIRE009"), Is.EqualTo(1));
        Assert.That(diagnostics.Single(diagnostic => diagnostic.Id == "SPIRE009").GetMessage(), Does.Contain("None"));
    }

    [Test]
    public void AdhocWorkspace_ProducesSemanticModelFromDocument()
    {
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId("CompilerProbeProject");
        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "CompilerProbeProject",
            "CompilerProbeProject",
            LanguageNames.CSharp,
            parseOptions: ParseOptions,
            compilationOptions: CompilationOptions,
            metadataReferences: BasicReferences);
        var documentId = DocumentId.CreateNewId(projectId, "workspace.cs");
        var solution = workspace.CurrentSolution
            .AddProject(projectInfo)
            .AddDocument(
                documentId,
                "workspace.cs",
                SourceText.From("public sealed class WorkspaceType { public int Answer => 42; }"));

        Assert.That(workspace.TryApplyChanges(solution), Is.True);
        var document = workspace.CurrentSolution.GetDocument(documentId)!;
        var root = document.GetSyntaxRootAsync().GetAwaiter().GetResult()!;
        var model = document.GetSemanticModelAsync().GetAwaiter().GetResult()!;
        var declaration = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single();

        Assert.That(model.GetDeclaredSymbol(declaration)?.Name, Is.EqualTo("WorkspaceType"));
        var property = declaration.Members.OfType<PropertyDeclarationSyntax>().Single();
        Assert.That(model.GetTypeInfo(property.Type).Type?.SpecialType, Is.EqualTo(SpecialType.System_Int32));
    }

    private static readonly CSharpParseOptions ParseOptions = CSharpParseOptions.Default
        .WithLanguageVersion(LanguageVersion.CSharp12);

    private static readonly CSharpCompilationOptions CompilationOptions = new(
        OutputKind.DynamicallyLinkedLibrary,
        allowUnsafe: false,
        nullableContextOptions: NullableContextOptions.Enable);

    private static SyntaxTree Parse(string source, string path)
        => CSharpSyntaxTree.ParseText(source, ParseOptions, path: path);

    private static CSharpCompilation CreateCompilation(string assemblyName, SyntaxTree tree)
        => CSharpCompilation.Create(
            assemblyName,
            new[] { tree },
            BasicReferences,
            CompilationOptions);

    private static ImmutableArray<Diagnostic> RunAnalyzer(string source, DiagnosticAnalyzer analyzer)
    {
        var compilation = CreateCompilation("CompilerProbe.Analyzer", Parse(source, "analyzer.cs"));
        return compilation
            .WithAnalyzers(ImmutableArray.Create(analyzer))
            .GetAnalyzerDiagnosticsAsync()
            .GetAwaiter()
            .GetResult();
    }

    private static ImmutableArray<MetadataReference> CreateBasicReferences()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic || string.IsNullOrEmpty(assembly.Location))
                continue;

            var fileName = Path.GetFileName(assembly.Location);
            if (fileName.Equals("mscorlib.dll", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("netstandard.dll", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("System.dll", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith("System.", StringComparison.OrdinalIgnoreCase))
            {
                paths.Add(assembly.Location);
            }
        }

        paths.Add(typeof(Houtamelo.Spire.DiscriminatedUnionAttribute).Assembly.Location);
        Assert.That(paths, Is.Not.Empty, "No safe framework metadata references were loaded.");
        return paths
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
    }

    private static object? GetMemberValue(Type type, object instance, string name)
        => type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(instance) ??
           type.GetField(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(instance);

    private static string FormatDiagnostics(IEnumerable<Diagnostic> diagnostics)
        => string.Join(Environment.NewLine, diagnostics.Select(diagnostic => diagnostic.ToString()));
}
