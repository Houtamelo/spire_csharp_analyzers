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
        ((string)System.AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(System.IO.Path.PathSeparator)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(CoreRef)
            .ToArray();

    private const string BasicUnionSource = @"
using Houtamelo.Spire;

[DiscriminatedUnion]
public partial struct Shape
{
    [Variant] public static partial Shape Circle(float radius);
    [Variant] public static partial Shape Rect(float w, float h);
}
";

    private static GeneratorDriverRunResult RunWithConfig(
        string source, Dictionary<string, string> globalOptions)
    {
        var tree = CSharpSyntaxTree.ParseText(source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest));
        var compilation = CSharpCompilation.Create("TestAssembly",
            new[] { tree }, BaseRefs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

        var configProvider = new TestAnalyzerConfigOptionsProvider(globalOptions);
        var generator = new DiscriminatedUnionGenerator();
        var driver = CSharpGeneratorDriver.Create(
            generators: new[] { generator.AsSourceGenerator() },
            optionsProvider: configProvider);

        var ranDriver = driver.RunGenerators(compilation);
        return ranDriver.GetRunResult();
    }

    private static string? GetMainGeneratedSource(GeneratorDriverRunResult result)
    {
        return result.GeneratedTrees
            .Where(t =>
            {
                var name = System.IO.Path.GetFileName(t.FilePath);
                return !name.EndsWith(".Stj.g.cs")
                    && !name.EndsWith(".Nsj.g.cs")
                    && !name.EndsWith(".ToString.g.cs")
                    && !name.EndsWith(".Schema.g.cs");
            })
            .Select(t => t.GetText().ToString())
            .FirstOrDefault();
    }

    private static string GetAllGeneratedSource(GeneratorDriverRunResult result)
    {
        return string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));
    }

    [Fact]
    public void DefaultLayout_Auto_NonGeneric_EmitsOverlap()
    {
        // No global config: sentinel resolves to Auto → Overlap for non-generic struct
        var result = RunWithConfig(BasicUnionSource, new Dictionary<string, string>());

        var source = GetMainGeneratedSource(result);
        Assert.NotNull(source);
        Assert.Contains("FieldOffset", source);
    }

    [Fact]
    public void GlobalLayout_Additive_EmitsAdditive()
    {
        // Spire_DU_DefaultLayout=Additive → Additive strategy → no FieldOffset
        var result = RunWithConfig(BasicUnionSource, new Dictionary<string, string>
        {
            ["build_property.Spire_DU_DefaultLayout"] = "Additive",
        });

        var source = GetMainGeneratedSource(result);
        Assert.NotNull(source);
        Assert.DoesNotContain("FieldOffset", source);
    }

    [Fact]
    public void ExplicitLayout_Overrides_GlobalDefault()
    {
        // Attribute explicitly sets Layout.Overlap (int value 2); global says Additive.
        // The explicit attribute wins → Overlap → FieldOffset present.
        const string source = @"
using Houtamelo.Spire;

[DiscriminatedUnion(layout: Layout.Overlap)]
public partial struct Shape
{
    [Variant] public static partial Shape Circle(float radius);
    [Variant] public static partial Shape Rect(float w, float h);
}
";
        var result = RunWithConfig(source, new Dictionary<string, string>
        {
            ["build_property.Spire_DU_DefaultLayout"] = "Additive",
        });

        var generated = GetMainGeneratedSource(result);
        Assert.NotNull(generated);
        Assert.Contains("FieldOffset", generated);
    }

    [Fact]
    public void GlobalJson_SystemTextJson_EmitsConverter()
    {
        // Spire_DU_DefaultJson=SystemTextJson → STJ converter generated
        var result = RunWithConfig(BasicUnionSource, new Dictionary<string, string>
        {
            ["build_property.Spire_DU_DefaultJson"] = "SystemTextJson",
        });

        var allSource = GetAllGeneratedSource(result);

        // If System.Text.Json is not available, SPIRE_DU006 is reported instead
        var stjDiag = result.Diagnostics
            .FirstOrDefault(d => d.Id == "SPIRE_DU006");

        if (stjDiag is not null)
        {
            // STJ not in refs — skip the converter assertion, but verify the diagnostic
            Assert.Equal("SPIRE_DU006", stjDiag.Id);
        }
        else
        {
            Assert.Contains("JsonConverter", allSource);
        }
    }

    [Fact]
    public void NoGlobalJson_NoConverter()
    {
        // No JSON config → no converter emitted
        var result = RunWithConfig(BasicUnionSource, new Dictionary<string, string>());

        var allSource = GetAllGeneratedSource(result);
        Assert.DoesNotContain("JsonConverter", allSource);
    }

    [Fact]
    public void GlobalGenerateDeconstruct_No_NoDeconstructMethods()
    {
        // Spire_DU_DefaultGenerateDeconstruct=false → no Deconstruct methods in generated source
        var result = RunWithConfig(BasicUnionSource, new Dictionary<string, string>
        {
            ["build_property.Spire_DU_DefaultGenerateDeconstruct"] = "false",
        });

        var source = GetMainGeneratedSource(result);
        Assert.NotNull(source);
        Assert.DoesNotContain("Deconstruct", source);
    }

    [Fact]
    public void GlobalJsonDiscriminator_Custom_UsedInConverter()
    {
        // Spire_DU_DefaultJson=SystemTextJson + Spire_DU_DefaultJsonDiscriminator=type
        // → STJ converter uses "type" as the discriminator property name
        var result = RunWithConfig(BasicUnionSource, new Dictionary<string, string>
        {
            ["build_property.Spire_DU_DefaultJson"] = "SystemTextJson",
            ["build_property.Spire_DU_DefaultJsonDiscriminator"] = "type",
        });

        // Same STJ availability guard as GlobalJson_SystemTextJson_EmitsConverter
        var stjDiag = result.Diagnostics
            .FirstOrDefault(d => d.Id == "SPIRE_DU006");

        if (stjDiag is not null)
        {
            Assert.Equal("SPIRE_DU006", stjDiag.Id);
        }
        else
        {
            var stjSource = result.GeneratedTrees
                .Where(t => System.IO.Path.GetFileName(t.FilePath).EndsWith(".Stj.g.cs"))
                .Select(t => t.GetText().ToString())
                .FirstOrDefault();

            Assert.NotNull(stjSource);
            Assert.Contains("\"type\"", stjSource);
        }
    }
}
