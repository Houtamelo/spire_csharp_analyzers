using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;
using Houtamelo.Spire.CodeFixes;
using Houtamelo.Spire.SourceGenerators.Tests.Unity;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Behavioral;

[Trait("Environment", "Unity")]
public sealed class RecordVariantSerializationConflictTests
{
    [Fact]
    public async Task OtherNamespaceSerializationAttributes_AreNotAcceptedAsUnityMetadata()
    {
        var result = UnityGeneratorTestHarness.Run(OtherNamespaceAttributeSource, includeUnityReferences: true);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        var diagnostics = await Analyze(result.OutputCompilation);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "SPIRE028");

        var record = result.OutputCompilation.GetTypeByMetadataName("OtherAttributeUnion+Value")!;
        var property = record.GetMembers("value").OfType<IPropertySymbol>().Single();
        var field = record.GetMembers().OfType<IFieldSymbol>()
            .Single(candidate => SymbolEqualityComparer.Default.Equals(candidate.AssociatedSymbol, property));
        Assert.Contains(field.GetAttributes(), attribute =>
            attribute.AttributeClass?.ToDisplayString() == "UnityEngine.SerializeField");
        Assert.DoesNotContain(field.GetAttributes(), attribute =>
            attribute.AttributeClass?.ToDisplayString() == "UnityEngine.SerializeReference");

        var reference = result.OutputCompilation.GetTypeByMetadataName("OtherAttributeUnion+Reference")!;
        var referenceProperty = reference.GetMembers("value").OfType<IPropertySymbol>().Single();
        var referenceField = reference.GetMembers().OfType<IFieldSymbol>()
            .Single(candidate => SymbolEqualityComparer.Default.Equals(candidate.AssociatedSymbol, referenceProperty));
        Assert.Contains(referenceField.GetAttributes(), attribute =>
            attribute.AttributeClass?.ToDisplayString() == "UnityEngine.SerializeField");
        Assert.DoesNotContain(referenceField.GetAttributes(), attribute =>
            attribute.AttributeClass?.ToDisplayString() == "UnityEngine.SerializeReference");
    }

    [Fact]
    public async Task OtherUnityReferenceMarker_NonPartialFieldFixUsesUnitySerializeField()
    {
        var result = UnityGeneratorTestHarness.Run(OtherMarkerNonPartialSource, includeUnityReferences: true);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        var diagnostic = Assert.Single(await Analyze(result.OutputCompilation), item => item.Id == "SPIRE028");

        var solution = CreateSolution(result.OutputCompilation);
        var document = solution.Projects.Single().Documents.Single(item => item.Name == "unity-case.cs");
        var actions = await GetActions(document, diagnostic);
        var fieldAction = Assert.Single(actions, action =>
            action.Title.Contains("serialization attributes", StringComparison.OrdinalIgnoreCase));
        var apply = Assert.Single((await fieldAction.GetOperationsAsync(CancellationToken.None))
            .OfType<ApplyChangesOperation>());
        var changedSource = (await apply.ChangedSolution.GetDocument(document.Id)!.GetTextAsync()).ToString();

        Assert.Contains("field: global::UnityEngine.SerializeField", changedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("field: global::UnityEngine.SerializeReference", changedSource, StringComparison.Ordinal);
        Assert.Contains("[Other.UnitySerializeReference]", changedSource, StringComparison.Ordinal);

        var regenerated = UnityGeneratorTestHarness.Run(changedSource, includeUnityReferences: true);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(regenerated);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(regenerated);
        Assert.DoesNotContain(await Analyze(regenerated.OutputCompilation), item => item.Id == "SPIRE028");
        var record = regenerated.OutputCompilation.GetTypeByMetadataName("OtherMarkerUnion+Value")!;
        var property = record.GetMembers("value").OfType<IPropertySymbol>().Single();
        var field = record.GetMembers().OfType<IFieldSymbol>()
            .Single(candidate => SymbolEqualityComparer.Default.Equals(candidate.AssociatedSymbol, property));
        Assert.Contains(field.GetAttributes(), attribute =>
            attribute.AttributeClass?.ToDisplayString() == "UnityEngine.SerializeField");
        Assert.DoesNotContain(field.GetAttributes(), attribute =>
            attribute.AttributeClass?.ToDisplayString() == "UnityEngine.SerializeReference");
    }

    [Fact]
    public async Task PartialRecordGeneratedFieldUsesUnityAttributeDespiteOtherAlias()
    {
        var result = UnityGeneratorTestHarness.Run(PartialGeneratedAttributeSource, includeUnityReferences: true);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        var diagnostics = await Analyze(result.OutputCompilation);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "SPIRE028");

        var record = result.OutputCompilation.GetTypeByMetadataName("GeneratedAttributeUnion+Value")!;
        var property = record.GetMembers("value").OfType<IPropertySymbol>().Single();
        var field = record.GetMembers().OfType<IFieldSymbol>()
            .Single(candidate => SymbolEqualityComparer.Default.Equals(candidate.AssociatedSymbol, property));
        Assert.Contains(field.GetAttributes(), attribute =>
            attribute.AttributeClass?.ToDisplayString() == "UnityEngine.SerializeField");
        Assert.DoesNotContain(field.GetAttributes(), attribute =>
            attribute.AttributeClass?.ToDisplayString() == "Other.SerializeFieldAttribute");
    }

    [Fact]
    public async Task ManagedReferenceMarkerWithWrongFieldAttribute_IsRepairedAndRegenerates()
    {
        var result = UnityGeneratorTestHarness.Run(ManagedReferenceMismatchSource, includeUnityReferences: true);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(result);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(result);
        var diagnostic = Assert.Single(await Analyze(result.OutputCompilation), item => item.Id == "SPIRE028");

        var solution = CreateSolution(result.OutputCompilation);
        var document = solution.Projects.Single().Documents.Single(item => item.Name == "unity-case.cs");
        var actions = await GetActions(document, diagnostic);
        var fieldAction = Assert.Single(actions, action =>
            action.Title.Contains("serialization attributes", StringComparison.OrdinalIgnoreCase));
        var operations = await fieldAction.GetOperationsAsync(CancellationToken.None);
        var apply = Assert.Single(operations.OfType<ApplyChangesOperation>());
        var changedSource = (await apply.ChangedSolution.GetDocument(document.Id)!.GetTextAsync()).ToString();

        Assert.Contains("field: global::UnityEngine.SerializeReference", changedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("field: global::UnityEngine.SerializeField", changedSource, StringComparison.Ordinal);
        Assert.Contains("[UnitySerializeReference]", changedSource, StringComparison.Ordinal);

        var regenerated = UnityGeneratorTestHarness.Run(changedSource, includeUnityReferences: true);
        UnityGeneratorTestHarness.AssertNoGeneratorErrors(regenerated);
        UnityGeneratorTestHarness.AssertNoCompilationErrors(regenerated);
        Assert.DoesNotContain(await Analyze(regenerated.OutputCompilation), item => item.Id == "SPIRE028");

        var record = regenerated.OutputCompilation.GetTypeByMetadataName("ManagedMismatchUnion+Value")!;
        var property = record.GetMembers("value").OfType<IPropertySymbol>().Single();
        var field = record.GetMembers().OfType<IFieldSymbol>()
            .Single(candidate => SymbolEqualityComparer.Default.Equals(candidate.AssociatedSymbol, property));
        Assert.Contains(field.GetAttributes(), attribute =>
            attribute.AttributeClass?.ToDisplayString() == "UnityEngine.SerializeReference");
        Assert.DoesNotContain(field.GetAttributes(), attribute =>
            attribute.AttributeClass?.ToDisplayString() == "UnityEngine.SerializeField");
        Assert.NotNull(UnityGeneratorTestHarness.EmitAndLoad(
                regenerated.OutputCompilation,
                "record variant serialization conflict repair")
            .GetType("ManagedMismatchUnion+Value"));
    }

    private static async Task<ImmutableArray<Microsoft.CodeAnalysis.Diagnostic>> Analyze(Compilation compilation)
        => (await compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new RecordVariantDeclarationAnalyzer()))
            .GetAnalyzerDiagnosticsAsync())
            .Where(diagnostic => diagnostic.Id is "SPIRE028" or "SPIRE029")
            .ToImmutableArray();

    private static async Task<IReadOnlyList<CodeAction>> GetActions(
        Document document,
        Microsoft.CodeAnalysis.Diagnostic diagnostic)
    {
        var actions = new List<CodeAction>();
        await new RecordVariantSerializationCodeFix().RegisterCodeFixesAsync(
            new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), CancellationToken.None));
        return actions;
    }

    private static Solution CreateSolution(Compilation compilation)
    {
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var solution = workspace.CurrentSolution
            .AddProject(projectId, "RecordVariantSerialization", "RecordVariantSerialization", LanguageNames.CSharp)
            .WithProjectParseOptions(projectId, (CSharpParseOptions)compilation.SyntaxTrees.First().Options)
            .WithProjectCompilationOptions(projectId, (CSharpCompilationOptions)compilation.Options);
        foreach (var reference in compilation.References)
            solution = solution.AddMetadataReference(projectId, reference);
        foreach (var tree in compilation.SyntaxTrees)
            solution = solution.AddDocument(DocumentId.CreateNewId(projectId), tree.FilePath, tree.GetText());
        return solution;
    }

    private const string OtherNamespaceAttributeSource = """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;
        namespace Other
        {
            [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter)]
            public sealed class SerializeFieldAttribute : Attribute { }
            [AttributeUsage(AttributeTargets.Field)]
            public sealed class SerializeReferenceAttribute : Attribute { }
            [AttributeUsage(AttributeTargets.Parameter)]
            public sealed class UnitySerializeReferenceAttribute : Attribute { }
        }
        [Serializable, DiscriminatedUnion]
        public partial struct OtherAttributeUnion
        {
            [Variant, Serializable]
            public partial record struct Value(
                [field: global::Other.SerializeField] int value);
            [Variant, Serializable]
            public partial record struct Reference(
                [field: global::Other.SerializeReference] int value);
        }
        """;

    private const string PartialGeneratedAttributeSource = """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;
        using OtherSerializeField = Other.SerializeFieldAttribute;
        namespace Other
        {
            [AttributeUsage(AttributeTargets.Field)]
            public sealed class SerializeFieldAttribute : Attribute { }
        }
        [Serializable, DiscriminatedUnion]
        public partial struct GeneratedAttributeUnion
        {
            [Variant, Serializable]
            public partial record struct Value(int value);
        }
        """;

    private const string OtherMarkerNonPartialSource = """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;
        namespace Other
        {
            [AttributeUsage(AttributeTargets.Parameter)]
            public sealed class UnitySerializeReferenceAttribute : Attribute { }
        }
        [Serializable, DiscriminatedUnion]
        public partial struct OtherMarkerUnion
        {
            [Variant, Serializable]
            public record struct Value([Other.UnitySerializeReference] int value);
        }
        """;

    private const string ManagedReferenceMismatchSource = """
        #nullable enable
        using System;
        using Houtamelo.Spire;
        using UnityEngine;
        [Serializable] public class NodeBase { public int id; }
        [Serializable, DiscriminatedUnion]
        public partial struct ManagedMismatchUnion
        {
            [Variant, Serializable]
            public partial record struct Value(
                [UnitySerializeReference]
                [field: UnityEngine.SerializeField]
                NodeBase? value);
        }
        """;
}
