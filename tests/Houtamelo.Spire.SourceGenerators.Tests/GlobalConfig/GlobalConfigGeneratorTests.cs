using System;
using System.Collections.Generic;
using System.Linq;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.GlobalConfig;

public sealed class GlobalConfigGeneratorTests
{
    private static readonly MetadataReference CoreRef =
        MetadataReference.CreateFromFile(typeof(Houtamelo.Spire.DiscriminatedUnionAttribute).Assembly.Location);

    private static readonly MetadataReference[] BaseRefs =
        TestHarnessEnvironment.FrameworkReferences.Append(CoreRef).ToArray();

    private const string BasicUnionSource = """
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct Shape
        {
            [Variant] public static partial Shape Circle(float radius);
            [Variant] public static partial Shape Rect(float w, float h);
        }
        """;

    private const string ReferenceUnionSource = """
        #nullable enable
        using Houtamelo.Spire;
        [DiscriminatedUnion]
        public partial struct Message
        {
            [Variant] public static partial Message Text(string text);
            [Variant] public static partial Message Tag(object tag);
            [Variant] public static partial Message Empty();
        }
        """;

    private static GeneratorDriverRunResult RunWithConfig(
        string source, Dictionary<string, string> globalOptions)
    {
        var (result, _) = RunWithConfigAndCompilation(source, globalOptions);
        return result;
    }

    private static (GeneratorDriverRunResult Result, Compilation OutputCompilation)
        RunWithConfigAndCompilation(string source, Dictionary<string, string> globalOptions)
    {
        var tree = CSharpSyntaxTree.ParseText(
            source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest));
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { tree },
            BaseRefs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var configProvider = new TestAnalyzerConfigOptionsProvider(globalOptions);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: new[] { new DiscriminatedUnionGenerator().AsSourceGenerator() },
            optionsProvider: configProvider);
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out _);
        return (driver.GetRunResult(), outputCompilation);
    }

    private static string GetMainGeneratedSource(GeneratorDriverRunResult result)
        => string.Join("\n", result.GeneratedTrees
            .Where(tree => !System.IO.Path.GetFileName(tree.FilePath).Contains(".Stj.")
                && !System.IO.Path.GetFileName(tree.FilePath).Contains(".Nsj.")
                && !System.IO.Path.GetFileName(tree.FilePath).Contains(".Schema."))
            .Select(tree => tree.GetText().ToString()));

    [Fact]
    public void DefaultReferenceStorageAutoGeneratesVariantPayloads()
    {
        var result = RunWithConfig(BasicUnionSource, new Dictionary<string, string>());
        var source = GetMainGeneratedSource(result);
        Assert.Contains("static class ShapeTypes", source, StringComparison.Ordinal);
        Assert.Contains("struct Circle", source, StringComparison.Ordinal);
        Assert.Contains("struct Rect", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GlobalReferenceStorageSharedUsesObjectSlots()
    {
        var (result, compilation) = RunWithConfigAndCompilation(ReferenceUnionSource, new Dictionary<string, string>
        {
            ["build_property.Spire_DU_DefaultReferenceStorage"] = "Shared",
        });
        Assert.Contains(
            GetStorageFields(compilation, "Message"),
            field => field.Type.SpecialType == SpecialType.System_Object);
        Assert.DoesNotContain("FieldOffset", GetMainGeneratedSource(result), StringComparison.Ordinal);
    }

    [Fact]
    public void ExplicitTypedReferenceStorageOverridesGlobalShared()
    {
        const string source = """
            #nullable enable
            using Houtamelo.Spire;
            [DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Typed)]
            public partial struct Message
            {
                [Variant] public static partial Message Text(string text);
                [Variant] public static partial Message Tag(object tag);
            }
            """;
        var (_, compilation) = RunWithConfigAndCompilation(source, new Dictionary<string, string>
        {
            ["build_property.Spire_DU_DefaultReferenceStorage"] = "Shared",
        });
        var storageFields = GetStorageFields(compilation, "Message");
        Assert.Contains(storageFields, field => field.Type.SpecialType == SpecialType.System_String);
        Assert.Contains(storageFields, field => field.Type.SpecialType == SpecialType.System_Object);
    }

    [Fact]
    public void ExplicitReadGlobalCfgInheritsGlobalReferenceStorage()
    {
        const string source = """
            #nullable enable
            using Houtamelo.Spire;
            [DiscriminatedUnion(ReferenceStorage = ReferenceStorage.ReadGlobalCfg)]
            public partial struct Message
            {
                [Variant] public static partial Message Text(string text);
                [Variant] public static partial Message Tag(object tag);
            }
            """;
        var (_, compilation) = RunWithConfigAndCompilation(source, new Dictionary<string, string>
        {
            ["build_property.Spire_DU_DefaultReferenceStorage"] = "Shared",
        });
        Assert.Contains(
            GetStorageFields(compilation, "Message"),
            field => field.Type.SpecialType == SpecialType.System_Object);
    }

    [Fact]
    public void GlobalJsonSystemTextJsonEmitsConverterWhenReferenced()
    {
        var result = RunWithConfig(BasicUnionSource, new Dictionary<string, string>
        {
            ["build_property.Spire_DU_DefaultJson"] = "SystemTextJson",
        });
        var allSource = string.Join("\n", result.GeneratedTrees.Select(tree => tree.GetText().ToString()));
        var missingReference = result.Diagnostics.Any(diagnostic => diagnostic.Id == "SPIRE_DU006");
        if (!missingReference)
            Assert.Contains("JsonConverter", allSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ExplicitNoneDisablesGlobalJsonConverter()
    {
        const string source = """
            using Houtamelo.Spire;
            [DiscriminatedUnion(json: JsonLibrary.None)]
            public partial struct Shape
            {
                [Variant] public static partial Shape Circle(float radius);
            }
            """;
        var result = RunWithConfig(source, new Dictionary<string, string>
        {
            ["build_property.Spire_DU_DefaultJson"] = "SystemTextJson",
        });
        Assert.DoesNotContain(result.GeneratedTrees,
            tree => System.IO.Path.GetFileName(tree.FilePath).EndsWith(".Stj.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void PayloadsAlwaysHaveTypedDeconstructMethods()
    {
        var result = RunWithConfig(BasicUnionSource, new Dictionary<string, string>
        {
            ["build_property.Spire_DU_DefaultReferenceStorage"] = "Typed",
        });
        Assert.Contains("void Deconstruct", GetMainGeneratedSource(result), StringComparison.Ordinal);
    }

    private static IReadOnlyList<IFieldSymbol> GetStorageFields(
        Compilation compilation,
        string unionName)
    {
        var fields = new List<IFieldSymbol>();
        var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        void Visit(INamedTypeSymbol type)
        {
            if (!visited.Add(type))
                return;

            fields.AddRange(type.GetMembers().OfType<IFieldSymbol>()
                .Where(field => !field.IsStatic && !field.IsImplicitlyDeclared));
            foreach (var nested in type.GetTypeMembers())
                Visit(nested);
        }

        var union = compilation.GetTypeByMetadataName(unionName);
        if (union is null)
            return fields;

        foreach (var container in AllNamedTypes(compilation)
            .Where(type => type.Name.StartsWith(union.Name + "Types", StringComparison.Ordinal) &&
                SymbolEqualityComparer.Default.Equals(type.ContainingSymbol, union.ContainingSymbol)))
        {
            var fieldsContainer = container.GetTypeMembers("Fields").SingleOrDefault();
            if (fieldsContainer is not null)
                Visit(fieldsContainer);
        }

        return fields;
    }

    private static IReadOnlyList<INamedTypeSymbol> AllNamedTypes(Compilation compilation)
    {
        var types = new List<INamedTypeSymbol>();

        void VisitNamespace(INamespaceSymbol ns)
        {
            foreach (var type in ns.GetTypeMembers())
                VisitType(type);
            foreach (var child in ns.GetNamespaceMembers())
                VisitNamespace(child);
        }

        void VisitType(INamedTypeSymbol type)
        {
            types.Add(type);
            foreach (var nested in type.GetTypeMembers())
                VisitType(nested);
        }

        VisitNamespace(compilation.GlobalNamespace);
        return types;
    }
}
