using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Xunit;

namespace UnityStructCatalog.Tests;

public sealed class UnityStructCatalogCliTests
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task Catalog_ClassifiesPrimitiveEnumAndNestedStructs()
    {
        using var fixture = TestFixture.Create();
        fixture.WriteSource("Types.cs", """
            namespace Example;

            public enum Kind : byte
            {
                First,
                Second,
            }

            public struct PrimitiveValue
            {
                public int Number;
                private Kind Kind;
            }

            public class Outer
            {
                public struct Inner
                {
                    public long Number;
                }
            }
            """);
        fixture.WriteSource("bin/ExcludedFromBin.cs", "namespace Example; public struct ExcludedFromBin { public string Value; }");
        fixture.WriteSource("obj/ExcludedFromObj.cs", "namespace Example; public struct ExcludedFromObj { public string Value; }");
        fixture.WriteSource(".git/ExcludedFromGit.cs", "namespace Example; public struct ExcludedFromGit { public string Value; }");
        fixture.WriteFile(
            Path.Combine(fixture.SourceDirectory, "not-csharp.txt"),
            "namespace Example; public struct ExcludedByExtension { public string Value; }");

        var result = await fixture.RunCatalogAsync();
        result.AssertSuccess();
        var catalog = fixture.ReadCatalog();

        AssertStatus(catalog.All, "Example.PrimitiveValue", "unmanaged");
        AssertStatus(catalog.All, "Example.Outer.Inner", "unmanaged");
        Assert.DoesNotContain(catalog.All, entry => entry.Name == "Example.Kind");
        Assert.DoesNotContain(catalog.All, entry => entry.Name.Contains("Excluded", StringComparison.Ordinal));
        AssertFilteredSubsets(catalog);
        Assert.Equal(catalog.All.Count, catalog.Summary.GetProperty("totalStructs").GetInt32());
        Assert.Equal(catalog.Unmanaged.Count, catalog.Summary.GetProperty("unmanaged").GetInt32());
        Assert.Equal(catalog.Managed.Count, catalog.Summary.GetProperty("managed").GetInt32());
        Assert.Equal(catalog.Unresolved.Count, catalog.Summary.GetProperty("unresolved").GetInt32());
        Assert.True(catalog.Summary.GetProperty("sourceFiles").GetInt32() >= 1);
        Assert.True(catalog.Summary.TryGetProperty("unparsedDeclarations", out _));
        Assert.True(catalog.Summary.TryGetProperty("structDeclarations", out _));
    }

    [Fact]
    public async Task Catalog_ClassifiesDirectAndTransitiveManagedFieldsIncludingPrivateStorage()
    {
        using var fixture = TestFixture.Create();
        fixture.WriteSource("Managed.cs", """
            namespace Example;

            public interface IService
            {
            }

            public delegate void Handler();

            public struct DirectManaged
            {
                private string Text;
                private object Data;
                private int[] Values;
                private Handler Callback;
                private IService Service;
                private static string IgnoredStatic;
                private const string IgnoredConst = "constant";
            }

            public struct TransitiveManaged
            {
                public DirectManaged Value;
            }

            public struct PrivateValue
            {
                private int Value;
            }
            """);

        var result = await fixture.RunCatalogAsync();
        result.AssertSuccess();
        var catalog = fixture.ReadCatalog();

        var direct = AssertStatus(catalog.All, "Example.DirectManaged", "managed");
        AssertStatus(catalog.All, "Example.TransitiveManaged", "managed");
        var privateValue = AssertStatus(catalog.All, "Example.PrivateValue", "unmanaged");

        Assert.Contains(direct.Fields, field => field.Name == "Text");
        Assert.Contains(direct.Fields, field => field.Name == "Data");
        Assert.Contains(direct.Fields, field => field.Name == "Values");
        Assert.Contains(direct.Fields, field => field.Name == "Callback");
        Assert.Contains(direct.Fields, field => field.Name == "Service");
        Assert.DoesNotContain(direct.Fields, field => field.Name == "IgnoredStatic");
        Assert.DoesNotContain(direct.Fields, field => field.Name == "IgnoredConst");
        Assert.Contains(privateValue.Fields, field => field.Name == "Value");
    }

    [Fact]
    public async Task Catalog_ClassifiesImplicitPropertyAndEventStorageButIgnoresComputedGetter()
    {
        using var fixture = TestFixture.Create();
        fixture.WriteSource("Members.cs", """
            namespace Example;

            public delegate void Handler();

            public struct PropertyAndEvent
            {
                public string AutoProperty { get; set; }
                public event Handler Happened;
                public int Computed => 42;
            }
            """);

        var result = await fixture.RunCatalogAsync();
        result.AssertSuccess();
        var entry = AssertStatus(fixture.ReadCatalog().All, "Example.PropertyAndEvent", "managed");

        var autoProperty = Assert.Single(entry.Fields, field => field.Name.Contains("AutoProperty", StringComparison.Ordinal));
        Assert.True(autoProperty.IsImplicit);
        var eventField = Assert.Single(entry.Fields, field => field.Name.Contains("Happened", StringComparison.Ordinal));
        Assert.True(eventField.IsImplicit);
        Assert.DoesNotContain(entry.Fields, field => field.Name.Contains("Computed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Catalog_ClassifiesFieldLikeEventsAsStorageAndCustomEventsAsStorageFree()
    {
        using var fixture = TestFixture.Create();
        fixture.WriteSource("Events.cs", """
            namespace Example;

            public delegate void Handler();

            public struct FieldLikeEventOnly
            {
                public event Handler Happened;
            }

            public struct CustomEvent
            {
                public event Handler Happened
                {
                    add { }
                    remove { }
                }
            }
            """);

        var result = await fixture.RunCatalogAsync();
        result.AssertSuccess();
        var catalog = fixture.ReadCatalog();

        var fieldLike = AssertStatus(catalog.All, "Example.FieldLikeEventOnly", "managed");
        var field = Assert.Single(fieldLike.Fields, candidate => candidate.Name.Contains("Happened", StringComparison.Ordinal));
        Assert.True(field.IsImplicit);

        var custom = AssertStatus(catalog.All, "Example.CustomEvent", "unmanaged");
        Assert.DoesNotContain(custom.Fields, candidate => candidate.Name.Contains("Happened", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Catalog_MergesPartialDeclarationsAndUsesFullyQualifiedNestedNames()
    {
        using var fixture = TestFixture.Create();
        fixture.WriteSource("Part1.cs", """
            namespace Example;

            public partial struct Combined
            {
                public int Number;
            }

            public class Container
            {
                public partial struct Nested
                {
                    public int First;
                }
            }
            """);
        fixture.WriteSource("nested/Part2.cs", """
            namespace Example;

            public partial struct Combined
            {
                public long Other;
            }

            public partial class Container
            {
                public partial struct Nested
                {
                    public short Second;
                }
            }
            """);

        var result = await fixture.RunCatalogAsync();
        result.AssertSuccess();
        var catalog = fixture.ReadCatalog();
        var combined = AssertStatus(catalog.All, "Example.Combined", "unmanaged");
        var nested = AssertStatus(catalog.All, "Example.Container.Nested", "unmanaged");

        Assert.Equal(2, combined.Declarations.Count);
        Assert.Contains(combined.Declarations, declaration => declaration.File == "Part1.cs");
        Assert.Contains(combined.Declarations, declaration => declaration.File == "nested/Part2.cs");
        Assert.Contains(combined.Fields, field => field.Name == "Number");
        Assert.Contains(combined.Fields, field => field.Name == "Other");
        Assert.Contains(nested.Fields, field => field.Name == "First");
        Assert.Contains(nested.Fields, field => field.Name == "Second");
    }

    [Fact]
    public async Task Catalog_ResolvesGenericConstraintsAndClosedConstructedFields()
    {
        using var fixture = TestFixture.Create();
        fixture.WriteSource("Generics.cs", """
            namespace Example;

            public struct UnmanagedGeneric<T>
                where T : unmanaged
            {
                public T Value;
            }

            public struct UnconstrainedGeneric<T>
            {
                public T Value;
            }

            public struct GenericHolder<T>
            {
                public UnconstrainedGeneric<T> Value;
            }

            public struct ClosedUnmanaged
            {
                public UnconstrainedGeneric<int> Value;
            }

            public struct ClosedManaged
            {
                public UnconstrainedGeneric<string> Value;
            }

            public ref struct RefLike
            {
                public int Value;
            }
            """);
        fixture.WriteSource("NestedGenerics.cs", """
            namespace Example.Nested;

            public struct MultiConstraint<T, U>
                where T : unmanaged
                where U : unmanaged
            {
                public T First;
                public U Second;
            }
            """);

        var result = await fixture.RunCatalogAsync();
        result.AssertSuccess();
        var catalog = fixture.ReadCatalog();

        var constrained = AssertStatus(catalog.All, "Example.UnmanagedGeneric<T>", "unmanaged");
        Assert.True(constrained.IsGeneric);
        var unconstrained = AssertStatus(catalog.All, "Example.UnconstrainedGeneric<T>", "unresolved");
        Assert.True(unconstrained.IsGeneric);
        AssertStatus(catalog.All, "Example.GenericHolder<T>", "unresolved");
        AssertStatus(catalog.All, "Example.ClosedUnmanaged", "unmanaged");
        AssertStatus(catalog.All, "Example.ClosedManaged", "managed");
        var refLike = AssertStatus(catalog.All, "Example.RefLike", "unmanaged");
        Assert.True(refLike.IsRefLike);
        var multiConstraint = Assert.Single(catalog.All, entry =>
            entry.Name.Contains("MultiConstraint", StringComparison.Ordinal));
        Assert.Equal("unmanaged", multiConstraint.Status);
        Assert.True(multiConstraint.IsGeneric);
        Assert.DoesNotContain(catalog.All, entry => entry.Name.EndsWith(".where", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Catalog_ClassifiesPointersAndFixedBuffersAsUnmanaged()
    {
        using var fixture = TestFixture.Create();
        fixture.WriteSource("UnsafeTypes.cs", """
            namespace Example;

            public unsafe struct PointerAndFixed
            {
                public int* Pointer;
                public fixed byte Buffer[16];
            }
            """);

        var result = await fixture.RunCatalogAsync();
        result.AssertSuccess();
        var entry = AssertStatus(fixture.ReadCatalog().All, "Example.PointerAndFixed", "unmanaged");

        Assert.Contains(entry.Fields, field => field.Name == "Pointer" && !field.IsFixedBuffer);
        Assert.Contains(entry.Fields, field => field.Name == "Buffer" && field.IsFixedBuffer);
    }

    [Fact]
    public async Task Catalog_ClassifiesByFieldTypesRegardlessOfLayoutAttributesAndRecordSyntax()
    {
        using var fixture = TestFixture.Create();
        fixture.WriteSource("Layouts.cs", """
            using System.Runtime.InteropServices;

            namespace Example;

            [StructLayout(LayoutKind.Explicit)]
            public struct ExplicitValue
            {
                [FieldOffset(0)]
                public int Value;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct SequentialManaged
            {
                public string Value;
            }

            public struct DefaultValue
            {
                public double Value;
            }

            public record struct RecordValue(int X, int Y);
            """);

        var result = await fixture.RunCatalogAsync();
        result.AssertSuccess();
        var catalog = fixture.ReadCatalog();

        AssertStatus(catalog.All, "Example.ExplicitValue", "unmanaged");
        AssertStatus(catalog.All, "Example.SequentialManaged", "managed");
        AssertStatus(catalog.All, "Example.DefaultValue", "unmanaged");
        AssertStatus(catalog.All, "Example.RecordValue", "unmanaged");
    }

    [Fact]
    public async Task Catalog_FlattensConditionalBranchesAndDoesNotParseCommentsOrStrings()
    {
        using var fixture = TestFixture.Create();
        fixture.WriteSource("Conditional.cs", """
            namespace Example;

            // struct CommentFake { public string Value; }
            /* struct BlockCommentFake { public string Value; } */
            public static class TextHolder
            {
                public const string Text = "struct StringFake { public string Value; }";
            }

            #if NEVER_DEFINED
            public struct HiddenManaged
            {
                public int Value;
            }
            #endif

            public struct BranchField
            {
            #if NEVER_DEFINED
                public string Hidden;
            #endif
                public int Visible;
            }
            """);

        var result = await fixture.RunCatalogAsync();
        result.AssertSuccess();
        var catalog = fixture.ReadCatalog();

        AssertStatus(catalog.All, "Example.HiddenManaged", "unmanaged");
        var branch = AssertStatus(catalog.All, "Example.BranchField", "managed");
        Assert.Contains(branch.Fields, field => field.Name == "Hidden");
        Assert.Contains(branch.Fields, field => field.Name == "Visible");
        Assert.DoesNotContain(catalog.All, entry => entry.Name.Contains("Fake", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Catalog_ReportsUnknownFieldsCyclesAndDuplicateDeclarationsAsUnresolved()
    {
        using var fixture = TestFixture.Create();
        fixture.WriteSource("Invalid.cs", """
            namespace Example;

            public struct UnknownField
            {
                public MissingType Value;
            }

            public struct CycleA
            {
                public CycleB Value;
            }

            public struct CycleB
            {
                public CycleA Value;
            }

            public struct Duplicate
            {
                public int Value;
            }

            public struct Duplicate
            {
                public int Other;
            }
            """);

        var result = await fixture.RunCatalogAsync();
        result.AssertSuccess();
        var catalog = fixture.ReadCatalog();

        AssertStatus(catalog.All, "Example.UnknownField", "unresolved");
        AssertStatus(catalog.All, "Example.CycleA", "unresolved");
        AssertStatus(catalog.All, "Example.CycleB", "unresolved");
        var duplicates = catalog.All.Where(entry => entry.Name == "Example.Duplicate").ToArray();
        Assert.NotEmpty(duplicates);
        Assert.All(duplicates, entry => Assert.Equal("unresolved", entry.Status));
    }

    [Fact]
    public async Task Catalog_RetainsMalformedStructDeclarationsAsUnresolved()
    {
        using var fixture = TestFixture.Create();
        fixture.WriteSource("Malformed.cs", """
            namespace Example;

            public struct Malformed
            {
                public int Value;
            """);

        var result = await fixture.RunCatalogAsync();
        result.AssertSuccess();
        var catalog = fixture.ReadCatalog();

        AssertStatus(catalog.All, "Example.Malformed", "unresolved");
        Assert.True(catalog.Summary.TryGetProperty("unparsedDeclarations", out _));
    }

    [Fact]
    public async Task Catalog_TreatsAlternativeConditionalAliasesAsUnresolved()
    {
        using var fixture = TestFixture.Create();
        fixture.WriteSource("Aliases.cs", """
            #if A
            using Alias = int;
            #else
            using Alias = string;
            #endif

            namespace Example;

            public struct UsesAlias
            {
                public Alias Value;
            }
            """);

        var result = await fixture.RunCatalogAsync();
        result.AssertSuccess();
        AssertStatus(fixture.ReadCatalog().All, "Example.UsesAlias", "unresolved");
    }

    [Fact]
    public async Task Catalog_UsesExternalMetadataReferencesToClassifyFieldTypes()
    {
        using var fixture = TestFixture.Create();
        var referenceProject = fixture.RootPath("reference-project");
        var referenceBuild = fixture.RootPath("reference-build");
        Directory.CreateDirectory(referenceProject);
        fixture.WriteFile(Path.Combine(referenceProject, "ExternalTypes.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>disable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <NoWarn>CS0169</NoWarn>
              </PropertyGroup>
            </Project>
            """);
        fixture.WriteFile(Path.Combine(referenceProject, "Types.cs"), """
            namespace ExternalTypes;

            public struct ExternalUnmanaged
            {
                public int Value;
            }

            public struct ExternalManaged
            {
                private string Value;
            }
            """);
        fixture.WriteFile(Path.Combine(referenceProject, "NestedTypes.cs"), """
            namespace ExternalTypes.Nested;

            public struct ExternalUnmanagedGeneric<T>
                where T : unmanaged
            {
                public T Value;
            }
            """);

        var build = await RunProcessAsync(
            "dotnet",
            fixture.RepositoryRoot,
            "build",
            Path.Combine(referenceProject, "ExternalTypes.csproj"),
            "--configuration",
            "Release",
            "--nologo",
            "--output",
            referenceBuild,
            "-p:NuGetAudit=false");
        Assert.True(build.ExitCode == 0, build.CombinedOutput);

        fixture.WriteSource("Input.cs", """
            namespace Example;

            public struct ReferencedFields
            {
                public ExternalTypes.ExternalUnmanaged Value;
                public ExternalTypes.ExternalManaged Managed;
            }

            public struct ReferencedGeneric
            {
                public ExternalTypes.Nested.ExternalUnmanagedGeneric<int> Value;
            }
            """);

        var result = await fixture.RunCatalogAsync(
            "--reference",
            referenceBuild);
        result.AssertSuccess();
        var catalog = fixture.ReadCatalog();
        AssertStatus(catalog.All, "Example.ReferencedFields", "managed");
        AssertStatus(catalog.All, "Example.ReferencedGeneric", "unmanaged");
    }

    [Fact]
    public async Task Catalog_RejectsMissingSourceEmptySourceAndMissingReferenceWithoutSuccessReport()
    {
        using var missingSourceFixture = TestFixture.Create();
        var missingSource = missingSourceFixture.RootPath("does-not-exist");
        var missingSourceResult = await missingSourceFixture.RunCatalogAtAsync(
            sourceDirectory: missingSource,
            outputDirectory: missingSourceFixture.RootPath("missing-source-output"));
        AssertFailureWithoutReport(missingSourceResult, missingSourceFixture.RootPath("missing-source-output"));

        using var emptyFixture = TestFixture.Create();
        var emptyResult = await emptyFixture.RunCatalogAsync();
        AssertFailureWithoutReport(emptyResult, emptyFixture.OutputDirectory);

        using var missingReferenceFixture = TestFixture.Create();
        missingReferenceFixture.WriteSource("Input.cs", "namespace Example; public struct Value { public int Number; }");
        var missingReferenceResult = await missingReferenceFixture.RunCatalogAsync(
            "--reference",
            missingReferenceFixture.RootPath("missing-reference.dll"));
        AssertFailureWithoutReport(missingReferenceResult, missingReferenceFixture.OutputDirectory);
    }

    [Fact]
    public async Task Catalog_RejectsMissingEmptyAndIncompleteFrameworkDirectoriesWithoutSuccessReport()
    {
        using var missingFixture = TestFixture.Create();
        missingFixture.WriteSource("Input.cs", "namespace Example; public struct Value { public int Number; }");
        var missingResult = await missingFixture.RunCatalogAsync(
            "--framework",
            missingFixture.RootPath("missing-framework"));
        AssertFailureWithoutReport(missingResult, missingFixture.OutputDirectory);

        using var emptyFixture = TestFixture.Create();
        emptyFixture.WriteSource("Input.cs", "namespace Example; public struct Value { public int Number; }");
        var emptyFramework = emptyFixture.RootPath("empty-framework");
        Directory.CreateDirectory(emptyFramework);
        var emptyResult = await emptyFixture.RunCatalogAsync("--framework", emptyFramework);
        AssertFailureWithoutReport(emptyResult, emptyFixture.OutputDirectory);

        using var incompleteFixture = TestFixture.Create();
        incompleteFixture.WriteSource("Input.cs", "namespace Example; public struct Value { public int Number; }");
        var incompleteFramework = incompleteFixture.RootPath("incomplete-framework");
        Directory.CreateDirectory(incompleteFramework);
        File.Copy(
            typeof(JsonDocument).Assembly.Location,
            Path.Combine(incompleteFramework, "System.Text.Json.dll"));
        var incompleteResult = await incompleteFixture.RunCatalogAsync(
            "--framework",
            incompleteFramework);
        AssertFailureWithoutReport(incompleteResult, incompleteFixture.OutputDirectory);
    }

    [Fact]
    public async Task Catalog_RejectsMalformedAssemblyInReferenceDirectoryWithoutSuccessReport()
    {
        using var fixture = TestFixture.Create();
        fixture.WriteSource("Input.cs", "public struct Value { public int Number; }");
        var references = fixture.RootPath("references");
        Directory.CreateDirectory(references);
        File.WriteAllText(Path.Combine(references, "Broken.dll"), "This is not a PE image.");

        var result = await fixture.RunCatalogAsync("--reference", references);

        AssertFailureWithoutReport(result, fixture.OutputDirectory);
    }

    [Fact]
    public async Task Catalog_UsesSelectedFrameworkAssembliesAndExcludesHostAndRoslynReferences()
    {
        using var fixture = TestFixture.Create();
        fixture.WriteSource("Framework.cs", """
            namespace Example;

            public struct GuidValue
            {
                public System.Guid Value;
            }
            """);
        var frameworkDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

        var result = await fixture.RunCatalogAsync("--framework", frameworkDirectory);
        result.AssertSuccess();
        var catalog = fixture.ReadCatalog();

        AssertStatus(catalog.All, "Example.GuidValue", "unmanaged");
        Assert.Equal(
            Path.GetFullPath(frameworkDirectory),
            Path.GetFullPath(catalog.Summary.GetProperty("framework").GetString()!));
        Assert.True(catalog.Summary.TryGetProperty("structDeclarations", out _));

        var references = catalog.Summary.GetProperty("references")
            .EnumerateArray()
            .Select(reference => reference.GetString()!)
            .ToArray();
        Assert.Contains(
            references,
            reference => Path.GetFileName(reference).Equals("System.Private.CoreLib.dll", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            references,
            reference => Path.GetFileName(reference).Equals("UnityStructCatalog.dll", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            references,
            reference => reference.Contains("Roslyn", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Catalog_FiltersByEffectiveAccessibilityAndGenericFlagsIndependently()
    {
        using var fixture = TestFixture.Create();
        fixture.WriteSource("Filters.cs", """
            namespace Example;

            public struct PublicValue
            {
                public int Value;
            }

            internal struct InternalValue
            {
                public int Value;
            }

            public struct PublicManaged
            {
                public string Value;
            }

            internal struct InternalManaged
            {
                public string Value;
            }

            public struct PublicUnresolved
            {
                private MissingType Value;
            }

            internal struct InternalUnresolved
            {
                private MissingType Value;
            }

            public struct GenericPublic<T>
                where T : unmanaged
            {
                public T Value;
            }

            public struct GenericManaged<T>
            {
                public string Value;
            }

            public struct GenericUnknown<T>
            {
                public T Value;
            }

            internal struct InternalPayload
            {
                public int Value;
            }

            public struct PublicWithInternalPayload
            {
                private InternalPayload Value;
            }

            public struct PublicWithClosedGeneric
            {
                private GenericPublic<int> Value;
            }

            public struct PublicWithClosedManaged
            {
                private GenericManaged<string> Value;
            }

            public struct PublicWithUnresolvedPayload
            {
                private MissingPayload Value;
            }

            public struct PublicWithGenericStatic
            {
                private static GenericPublic<int> StaticValue;
                public int Value;
            }

            public ref struct PublicRefLike
            {
                public int Value;
            }

            public class PublicOuter
            {
                public struct PublicNested
                {
                    public int Value;
                }

                internal struct InternalNested
                {
                    public int Value;
                }

                private struct PrivateNested
                {
                    public int Value;
                }

                protected struct ProtectedNested
                {
                    public int Value;
                }

                private protected struct PrivateProtectedNested
                {
                    public int Value;
                }

                protected internal struct ProtectedInternalNested
                {
                    public int Value;
                }
            }

            internal class InternalOuter
            {
                public struct PublicNestedInInternal
                {
                    public int Value;
                }
            }

            public class GenericOuter<T>
                where T : unmanaged
            {
                public struct NestedNonGeneric
                {
                    public int Value;
                }
            }

            public class PayloadContainer
            {
                private struct PrivatePayload
                {
                    public int Value;
                }

                public struct PublicWithPrivatePayload
                {
                    private PrivatePayload Value;
                }
            }
            """);

        var defaultOutput = fixture.RootPath("filter-default-output");
        var publicOnlyOutput = fixture.RootPath("filter-public-output");
        var excludeGenericsOutput = fixture.RootPath("filter-generics-output");
        var bothOutput = fixture.RootPath("filter-both-output");

        var defaultResult = await fixture.RunCatalogWithArgumentsAsync(
            "--output",
            defaultOutput,
            "--source",
            fixture.SourceDirectory);
        defaultResult.AssertSuccess();
        var defaultCatalog = fixture.ReadCatalog(defaultOutput);

        var publicOnlyResult = await fixture.RunCatalogWithArgumentsAsync(
            "--public-only",
            "--output",
            publicOnlyOutput,
            "--source",
            fixture.SourceDirectory);
        publicOnlyResult.AssertSuccess();
        var publicOnlyCatalog = fixture.ReadCatalog(publicOnlyOutput);

        var excludeGenericsResult = await fixture.RunCatalogWithArgumentsAsync(
            "--exclude-generics",
            "--source",
            fixture.SourceDirectory,
            "--output",
            excludeGenericsOutput);
        excludeGenericsResult.AssertSuccess();
        var excludeGenericsCatalog = fixture.ReadCatalog(excludeGenericsOutput);

        var bothResult = await fixture.RunCatalogWithArgumentsAsync(
            "--source",
            fixture.SourceDirectory,
            "--exclude-generics",
            "--output",
            bothOutput,
            "--public-only");
        bothResult.AssertSuccess();
        var bothCatalog = fixture.ReadCatalog(bothOutput);

        var scannedStructs = defaultCatalog.All.Count;
        AssertFilterReport(defaultCatalog, defaultOutput, scannedStructs, publicOnly: false, excludeGenerics: false);
        AssertFilterReport(publicOnlyCatalog, publicOnlyOutput, scannedStructs, publicOnly: true, excludeGenerics: false);
        AssertFilterReport(excludeGenericsCatalog, excludeGenericsOutput, scannedStructs, publicOnly: false, excludeGenerics: true);
        AssertFilterReport(bothCatalog, bothOutput, scannedStructs, publicOnly: true, excludeGenerics: true);

        AssertStatus(defaultCatalog.All, "Example.PublicValue", "unmanaged");
        AssertStatus(defaultCatalog.All, "Example.PublicManaged", "managed");
        AssertStatus(defaultCatalog.All, "Example.PublicUnresolved", "unresolved");
        AssertStatus(defaultCatalog.All, "Example.PublicWithInternalPayload", "unmanaged");
        AssertStatus(defaultCatalog.All, "Example.PublicWithClosedGeneric", "unmanaged");
        AssertStatus(defaultCatalog.All, "Example.PublicWithClosedManaged", "managed");
        var staticGeneric = AssertStatus(defaultCatalog.All, "Example.PublicWithGenericStatic", "unmanaged");
        Assert.False(staticGeneric.IsGeneric);
        var refLike = AssertStatus(defaultCatalog.All, "Example.PublicRefLike", "unmanaged");
        Assert.True(refLike.IsRefLike);

        Assert.Contains(defaultCatalog.All, entry => entry.Name == "Example.InternalValue");
        Assert.Contains(defaultCatalog.All, entry => entry.Name == "Example.PublicOuter.PrivateNested");
        Assert.Contains(defaultCatalog.All, entry => entry.Name == "Example.PayloadContainer.PrivatePayload");
        Assert.Contains(defaultCatalog.All, entry => entry.Name.Contains("NestedNonGeneric", StringComparison.Ordinal));

        Assert.Contains(publicOnlyCatalog.All, entry => entry.Name == "Example.PublicValue");
        Assert.Contains(publicOnlyCatalog.All, entry => entry.Name == "Example.PublicOuter.PublicNested");
        Assert.Contains(publicOnlyCatalog.All, entry => entry.Name == "Example.PublicWithClosedGeneric");
        Assert.Contains(publicOnlyCatalog.All, entry => entry.Name == "Example.PublicWithClosedManaged");
        Assert.Contains(publicOnlyCatalog.All, entry => entry.Name.Contains("NestedNonGeneric", StringComparison.Ordinal));
        Assert.DoesNotContain(publicOnlyCatalog.All, entry => entry.Name == "Example.InternalValue");
        Assert.DoesNotContain(publicOnlyCatalog.All, entry => entry.Name == "Example.InternalManaged");
        Assert.DoesNotContain(publicOnlyCatalog.All, entry => entry.Name == "Example.InternalUnresolved");
        Assert.DoesNotContain(publicOnlyCatalog.All, entry => entry.Name.Contains("InternalNested", StringComparison.Ordinal));
        Assert.DoesNotContain(publicOnlyCatalog.All, entry => entry.Name.Contains("PrivateNested", StringComparison.Ordinal));
        Assert.DoesNotContain(publicOnlyCatalog.All, entry => entry.Name.Contains("ProtectedNested", StringComparison.Ordinal));
        Assert.DoesNotContain(publicOnlyCatalog.All, entry => entry.Name == "Example.InternalOuter.PublicNestedInInternal");
        Assert.DoesNotContain(publicOnlyCatalog.All, entry => entry.Name == "Example.PayloadContainer.PrivatePayload");

        Assert.DoesNotContain(excludeGenericsCatalog.All, entry => entry.Name == "Example.GenericPublic<T>");
        Assert.DoesNotContain(excludeGenericsCatalog.All, entry => entry.Name == "Example.GenericManaged<T>");
        Assert.DoesNotContain(excludeGenericsCatalog.All, entry => entry.Name == "Example.GenericUnknown<T>");
        Assert.DoesNotContain(excludeGenericsCatalog.All, entry => entry.Name.Contains("NestedNonGeneric", StringComparison.Ordinal));
        Assert.Contains(excludeGenericsCatalog.All, entry => entry.Name == "Example.InternalValue");
        Assert.Contains(excludeGenericsCatalog.All, entry => entry.Name == "Example.PublicOuter.PrivateNested");
        Assert.Contains(excludeGenericsCatalog.All, entry => entry.Name == "Example.PayloadContainer.PrivatePayload");
        Assert.Contains(excludeGenericsCatalog.All, entry => entry.Name == "Example.PublicWithClosedGeneric");

        Assert.DoesNotContain(bothCatalog.All, entry => entry.Name == "Example.InternalValue");
        Assert.DoesNotContain(bothCatalog.All, entry => entry.Name == "Example.GenericPublic<T>");
        Assert.DoesNotContain(bothCatalog.All, entry => entry.Name.Contains("NestedNonGeneric", StringComparison.Ordinal));
        Assert.Contains(bothCatalog.All, entry => entry.Name == "Example.PublicWithClosedGeneric");
        Assert.Contains(bothCatalog.All, entry => entry.Name == "Example.PublicWithClosedManaged");
        Assert.Contains(bothCatalog.All, entry => entry.Name == "Example.PublicRefLike");
    }

    [Fact]
    public async Task Catalog_FiltersByMinimumInstanceFieldsAfterClassificationAndPreservesDependencies()
    {
        using var fixture = TestFixture.Create();
        fixture.WriteSource("FieldCounts.cs", """
            namespace Example;

            public struct EmptyMarker
            {
            }

            public struct OneField
            {
                public int Value;
            }

            public struct OneInstanceManyStatic
            {
                public int Value;
                private static int StaticOne;
                private static string StaticTwo;
                private const int ConstantOne = 1;
                private const string ConstantTwo = "constant";
            }

            public struct TwoInstance
            {
                private int Hidden;
                public string Name { get; set; }
            }

            internal struct InternalOneField
            {
                public int Value;
            }

            public struct PublicWithInternalDependency
            {
                private InternalOneField Dependency;
                public int Other;
            }

            public delegate void Handler();

            public struct EventAndInt
            {
                public event Handler Happened;
                public int Value;
            }

            public struct EventOnly
            {
                public event Handler Happened;
            }

            public record struct RecordPair(int X, int Y);

            public struct GenericTwo<T>
            {
                public T First;
                public int Second;
            }

            internal struct InternalTwo
            {
                public int First;
                public int Second;
            }
            """);

        var defaultOutput = fixture.RootPath("field-count-default-output");
        var zeroOutput = fixture.RootPath("field-count-zero-output");
        var minimumTwoOutput = fixture.RootPath("field-count-minimum-two-output");
        var publicMinimumTwoOutput = fixture.RootPath("field-count-public-minimum-two-output");
        var genericMinimumTwoOutput = fixture.RootPath("field-count-generic-minimum-two-output");

        var defaultResult = await fixture.RunCatalogWithArgumentsAsync(
            "--source",
            fixture.SourceDirectory,
            "--output",
            defaultOutput);
        defaultResult.AssertSuccess();
        var defaultCatalog = fixture.ReadCatalog(defaultOutput);

        var zeroResult = await fixture.RunCatalogWithArgumentsAsync(
            "--min-instance-fields",
            "0",
            "--output",
            zeroOutput,
            "--source",
            fixture.SourceDirectory);
        zeroResult.AssertSuccess();
        var zeroCatalog = fixture.ReadCatalog(zeroOutput);

        var minimumTwoResult = await fixture.RunCatalogWithArgumentsAsync(
            "--source",
            fixture.SourceDirectory,
            "--min-instance-fields",
            "2",
            "--output",
            minimumTwoOutput);
        minimumTwoResult.AssertSuccess();
        var minimumTwoCatalog = fixture.ReadCatalog(minimumTwoOutput);

        var publicMinimumTwoResult = await fixture.RunCatalogWithArgumentsAsync(
            "--public-only",
            "--output",
            publicMinimumTwoOutput,
            "--min-instance-fields",
            "2",
            "--source",
            fixture.SourceDirectory);
        publicMinimumTwoResult.AssertSuccess();
        var publicMinimumTwoCatalog = fixture.ReadCatalog(publicMinimumTwoOutput);

        var genericMinimumTwoResult = await fixture.RunCatalogWithArgumentsAsync(
            "--exclude-generics",
            "--min-instance-fields",
            "2",
            "--source",
            fixture.SourceDirectory,
            "--output",
            genericMinimumTwoOutput);
        genericMinimumTwoResult.AssertSuccess();
        var genericMinimumTwoCatalog = fixture.ReadCatalog(genericMinimumTwoOutput);

        var scannedStructs = defaultCatalog.All.Count;
        AssertFilterReport(defaultCatalog, defaultOutput, scannedStructs, publicOnly: false, excludeGenerics: false);
        AssertFilterReport(zeroCatalog, zeroOutput, scannedStructs, publicOnly: false, excludeGenerics: false);
        AssertFilterReport(minimumTwoCatalog, minimumTwoOutput, scannedStructs, publicOnly: false, excludeGenerics: false, minimumInstanceFields: 2);
        AssertFilterReport(publicMinimumTwoCatalog, publicMinimumTwoOutput, scannedStructs, publicOnly: true, excludeGenerics: false, minimumInstanceFields: 2);
        AssertFilterReport(genericMinimumTwoCatalog, genericMinimumTwoOutput, scannedStructs, publicOnly: false, excludeGenerics: true, minimumInstanceFields: 2);

        Assert.Equal(
            defaultCatalog.All.Select(entry => entry.Name).ToHashSet(StringComparer.Ordinal),
            zeroCatalog.All.Select(entry => entry.Name).ToHashSet(StringComparer.Ordinal));
        Assert.Equal(
            defaultCatalog.All.Select(entry => entry.Status).OrderBy(status => status).ToArray(),
            zeroCatalog.All.Select(entry => entry.Status).OrderBy(status => status).ToArray());

        Assert.DoesNotContain(minimumTwoCatalog.All, entry => entry.Name == "Example.EmptyMarker");
        Assert.DoesNotContain(minimumTwoCatalog.All, entry => entry.Name == "Example.OneField");
        Assert.DoesNotContain(minimumTwoCatalog.All, entry => entry.Name == "Example.OneInstanceManyStatic");
        Assert.DoesNotContain(minimumTwoCatalog.All, entry => entry.Name == "Example.EventOnly");
        Assert.Contains(minimumTwoCatalog.All, entry => entry.Name == "Example.TwoInstance");
        Assert.Contains(minimumTwoCatalog.All, entry => entry.Name == "Example.PublicWithInternalDependency");
        Assert.Contains(minimumTwoCatalog.All, entry => entry.Name == "Example.EventAndInt");
        Assert.Contains(minimumTwoCatalog.All, entry => entry.Name == "Example.RecordPair");
        Assert.Contains(minimumTwoCatalog.All, entry => entry.Name == "Example.GenericTwo<T>");
        Assert.Contains(minimumTwoCatalog.All, entry => entry.Name == "Example.InternalTwo");

        var twoInstance = Assert.Single(minimumTwoCatalog.All, entry => entry.Name == "Example.TwoInstance");
        Assert.Equal("managed", twoInstance.Status);
        Assert.Contains(twoInstance.Fields, field => field.Name == "Hidden");
        Assert.Contains(twoInstance.Fields, field => field.Name.Contains("Name", StringComparison.Ordinal));
        var eventAndInt = Assert.Single(minimumTwoCatalog.All, entry => entry.Name == "Example.EventAndInt");
        Assert.Equal(2, eventAndInt.Fields.Count);
        Assert.Contains(eventAndInt.Fields, field => field.Name.Contains("Happened", StringComparison.Ordinal));
        Assert.Contains(eventAndInt.Fields, field => field.Name == "Value");
        var recordPair = Assert.Single(minimumTwoCatalog.All, entry => entry.Name == "Example.RecordPair");
        Assert.Equal(2, recordPair.Fields.Count);

        Assert.DoesNotContain(publicMinimumTwoCatalog.All, entry => entry.Name == "Example.InternalTwo");
        Assert.Contains(publicMinimumTwoCatalog.All, entry => entry.Name == "Example.PublicWithInternalDependency");
        Assert.Contains(publicMinimumTwoCatalog.All, entry => entry.Name == "Example.GenericTwo<T>");
        Assert.DoesNotContain(genericMinimumTwoCatalog.All, entry => entry.Name == "Example.GenericTwo<T>");
        Assert.Contains(genericMinimumTwoCatalog.All, entry => entry.Name == "Example.InternalTwo");
    }

    [Fact]
    public async Task Catalog_RejectsInvalidMinimumInstanceFieldsValuesWithoutSuccessReport()
    {
        using var fixture = TestFixture.Create();
        fixture.WriteSource("Input.cs", "namespace Example; public struct Value { public int Number; }");

        var negativeOutput = fixture.RootPath("minimum-negative-output");
        var negativeResult = await fixture.RunCatalogWithArgumentsAsync(
            "--source",
            fixture.SourceDirectory,
            "--output",
            negativeOutput,
            "--min-instance-fields",
            "-1");
        AssertFailureWithoutReport(negativeResult, negativeOutput);

        var nonIntegerOutput = fixture.RootPath("minimum-noninteger-output");
        var nonIntegerResult = await fixture.RunCatalogWithArgumentsAsync(
            "--min-instance-fields",
            "two",
            "--source",
            fixture.SourceDirectory,
            "--output",
            nonIntegerOutput);
        AssertFailureWithoutReport(nonIntegerResult, nonIntegerOutput);

        var missingValueOutput = fixture.RootPath("minimum-missing-value-output");
        var missingValueResult = await fixture.RunCatalogWithArgumentsAsync(
            "--source",
            fixture.SourceDirectory,
            "--output",
            missingValueOutput,
            "--min-instance-fields");
        AssertFailureWithoutReport(missingValueResult, missingValueOutput);
    }

    [Fact]
    public async Task Catalog_RejectsUnknownFilterFlagsAndMissingOptionValuesWithoutSuccessReport()
    {
        using var fixture = TestFixture.Create();
        fixture.WriteSource("Input.cs", "namespace Example; public struct Value { public int Number; }");

        var unknownOutput = fixture.RootPath("unknown-output");
        var unknownResult = await fixture.RunCatalogWithArgumentsAsync(
            "--source",
            fixture.SourceDirectory,
            "--output",
            unknownOutput,
            "--not-a-real-filter");
        AssertFailureWithoutReport(unknownResult, unknownOutput);

        var missingOutputValue = fixture.RootPath("missing-output-value");
        var missingOutputValueResult = await fixture.RunCatalogWithArgumentsAsync(
            "--source",
            fixture.SourceDirectory,
            "--output");
        AssertFailureWithoutReport(missingOutputValueResult, missingOutputValue);

        var missingSourceValue = fixture.RootPath("missing-source-value");
        var missingSourceValueResult = await fixture.RunCatalogWithArgumentsAsync(
            "--source",
            "--public-only",
            "--output",
            missingSourceValue);
        AssertFailureWithoutReport(missingSourceValueResult, missingSourceValue);
    }

    private static CatalogEntry AssertStatus(
        IReadOnlyList<CatalogEntry> entries,
        string name,
        string status)
    {
        var entry = Assert.Single(entries, candidate => candidate.Name == name);
        Assert.Equal(status, entry.Status);
        return entry;
    }

    private static void AssertFilteredSubsets(Catalog catalog)
    {
        var allNames = catalog.All.Select(entry => entry.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var filtered in new[] { catalog.Unmanaged, catalog.Managed, catalog.Unresolved })
        {
            Assert.All(filtered, entry => Assert.Contains(entry.Name, allNames));
        }

        Assert.Equal(catalog.All.Count, catalog.Unmanaged.Count + catalog.Managed.Count + catalog.Unresolved.Count);
    }

    private static void AssertFilterReport(
        Catalog catalog,
        string outputDirectory,
        int scannedStructs,
        bool publicOnly,
        bool excludeGenerics,
        int minimumInstanceFields = 0)
    {
        AssertFilteredSubsets(catalog);
        Assert.Equal(catalog.All.Count, catalog.Summary.GetProperty("totalStructs").GetInt32());
        Assert.Equal(catalog.All.Count, catalog.Summary.GetProperty("structDeclarations").GetInt32());
        Assert.Equal(catalog.Unmanaged.Count, catalog.Summary.GetProperty("unmanaged").GetInt32());
        Assert.Equal(catalog.Managed.Count, catalog.Summary.GetProperty("managed").GetInt32());
        Assert.Equal(catalog.Unresolved.Count, catalog.Summary.GetProperty("unresolved").GetInt32());
        Assert.Equal(scannedStructs, catalog.Summary.GetProperty("scannedStructs").GetInt32());
        Assert.Equal(publicOnly, catalog.Summary.GetProperty("publicOnly").GetBoolean());
        Assert.Equal(excludeGenerics, catalog.Summary.GetProperty("excludeGenerics").GetBoolean());
        Assert.Equal(minimumInstanceFields, catalog.Summary.GetProperty("minimumInstanceFields").GetInt32());

        var unmanagedTsv = File.ReadAllLines(Path.Combine(outputDirectory, "unmanaged.tsv"));
        Assert.NotEmpty(unmanagedTsv);
        Assert.Equal("type\tgeneric\tref-like\tdeclarations", unmanagedTsv[0]);
        var tsvNames = unmanagedTsv
            .Skip(1)
            .Select(line => line.Split('\t')[0])
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(catalog.Unmanaged.Select(entry => entry.Name).ToHashSet(StringComparer.Ordinal), tsvNames);
    }

    private static void AssertFailureWithoutReport(ProcessResult result, string outputDirectory)
    {
        Assert.False(result.TimedOut, result.CombinedOutput);
        Assert.NotEqual(0, result.ExitCode);
        Assert.False(File.Exists(Path.Combine(outputDirectory, "summary.json")), result.CombinedOutput);
        Assert.False(File.Exists(Path.Combine(outputDirectory, "all.json")), result.CombinedOutput);
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string executable,
        string workingDirectory,
        params string[] arguments)
    {
        var info = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        info.Environment["CI"] = "true";
        info.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        info.Environment["EnableSourceControlManagerQueries"] = "false";
        var nugetCache = Path.Combine(workingDirectory, "tmp", "unity-struct-catalog-test-nuget-cache");
        Directory.CreateDirectory(nugetCache);
        info.Environment["NUGET_HTTP_CACHE_PATH"] = nugetCache;
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = info };
        Assert.True(process.Start(), $"Could not start '{executable}'.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        var timedOut = false;
        using (var cancellation = new CancellationTokenSource(ProcessTimeout))
        {
            try
            {
                await process.WaitForExitAsync(cancellation.Token);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                timedOut = true;
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }

        return new ProcessResult(
            process.ExitCode,
            await stdout,
            await stderr,
            timedOut);
    }

    private sealed class TestFixture : IDisposable
    {
        private TestFixture(string repositoryRoot, string root)
        {
            RepositoryRoot = repositoryRoot;
            Root = root;
            SourceDirectory = Path.Combine(root, "source");
            OutputDirectory = Path.Combine(root, "output");
            Directory.CreateDirectory(SourceDirectory);
        }

        public string RepositoryRoot { get; }
        public string Root { get; }
        public string SourceDirectory { get; }
        public string OutputDirectory { get; }

        public static TestFixture Create()
        {
            var repositoryRoot = FindRepositoryRoot();
            var root = Path.Combine(
                repositoryRoot,
                "tmp",
                "unity-struct-catalog-tests",
                Guid.NewGuid().ToString("N"));
            return new TestFixture(repositoryRoot, root);
        }

        public string RootPath(string relativePath) => Path.Combine(Root, relativePath);

        public void WriteSource(string relativePath, string source)
            => WriteFile(Path.Combine(SourceDirectory, relativePath), source);

        public void WriteFile(string path, string contents)
        {
            var directory = Path.GetDirectoryName(path);
            if (directory is not null)
                Directory.CreateDirectory(directory);
            File.WriteAllText(path, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        public async Task<ProcessResult> RunCatalogAsync(
            params string[] extraArguments)
            => await RunCatalogAtAsync(SourceDirectory, OutputDirectory, extraArguments);

        public async Task<ProcessResult> RunCatalogWithArgumentsAsync(
            params string[] arguments)
        {
            var commandArguments = new List<string> { GetCatalogAssemblyPath(RepositoryRoot) };
            commandArguments.AddRange(arguments);
            return await RunProcessAsync(
                "dotnet",
                RepositoryRoot,
                commandArguments.ToArray());
        }

        public async Task<ProcessResult> RunCatalogAtAsync(
            string sourceDirectory,
            string outputDirectory,
            params string[] extraArguments)
        {
            var arguments = new List<string>
            {
                "--source",
                sourceDirectory,
                "--output",
                outputDirectory,
            };
            arguments.AddRange(extraArguments);
            arguments.Insert(0, GetCatalogAssemblyPath(RepositoryRoot));
            return await RunProcessAsync(
                "dotnet",
                RepositoryRoot,
                arguments.ToArray());
        }

        public Catalog ReadCatalog(string? outputDirectory = null)
        {
            outputDirectory ??= OutputDirectory;
            return new(
                ReadEntries(outputDirectory, "all.json"),
                ReadEntries(outputDirectory, "unmanaged.json"),
                ReadEntries(outputDirectory, "managed.json"),
                ReadEntries(outputDirectory, "unresolved.json"),
                JsonDocument.Parse(File.ReadAllText(Path.Combine(outputDirectory, "summary.json"))).RootElement.Clone());
        }

        private IReadOnlyList<CatalogEntry> ReadEntries(string outputDirectory, string fileName)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(outputDirectory, fileName)));
            return document.RootElement
                .EnumerateArray()
                .Select(CatalogEntry.Parse)
                .ToArray();
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }

    private sealed record Catalog(
        IReadOnlyList<CatalogEntry> All,
        IReadOnlyList<CatalogEntry> Unmanaged,
        IReadOnlyList<CatalogEntry> Managed,
        IReadOnlyList<CatalogEntry> Unresolved,
        JsonElement Summary);

    private sealed record CatalogEntry(
        string Name,
        string Status,
        IReadOnlyList<string> Reasons,
        IReadOnlyList<Declaration> Declarations,
        IReadOnlyList<Field> Fields,
        bool IsGeneric,
        bool IsRefLike)
    {
        public static CatalogEntry Parse(JsonElement entry)
        {
            var declarations = entry.GetProperty("declarations")
                .EnumerateArray()
                .Select(declaration => new Declaration(
                    declaration.GetProperty("file").GetString()!,
                    declaration.GetProperty("line").GetInt32()))
                .ToArray();
            var reasons = entry.GetProperty("reasons")
                .EnumerateArray()
                .Select(reason => reason.GetString()!)
                .ToArray();
            var fields = entry.GetProperty("fields")
                .EnumerateArray()
                .Select(field => new Field(
                    field.GetProperty("name").GetString()!,
                    field.GetProperty("type").GetString()!,
                    field.GetProperty("isImplicit").GetBoolean(),
                    field.GetProperty("isFixedBuffer").GetBoolean()))
                .ToArray();
            return new CatalogEntry(
                entry.GetProperty("name").GetString()!,
                entry.GetProperty("status").GetString()!,
                reasons,
                declarations,
                fields,
                entry.GetProperty("isGeneric").GetBoolean(),
                entry.GetProperty("isRefLike").GetBoolean());
        }
    }

    private sealed record Declaration(string File, int Line);

    private sealed record Field(string Name, string Type, bool IsImplicit, bool IsFixedBuffer);

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut)
    {
        public string CombinedOutput => StandardOutput + Environment.NewLine + StandardError;

        public void AssertSuccess()
        {
            Assert.False(TimedOut, CombinedOutput);
            Assert.True(ExitCode == 0, CombinedOutput);
        }
    }

    private static string GetCatalogAssemblyPath(string repositoryRoot)
    {
        var overridePath = Environment.GetEnvironmentVariable("UNITY_STRUCT_CATALOG_DLL");
        if (!string.IsNullOrWhiteSpace(overridePath))
            return overridePath;

        var configuration = Environment.GetEnvironmentVariable("UNITY_STRUCT_CATALOG_CONFIGURATION");
        if (string.IsNullOrWhiteSpace(configuration))
        {
            configuration = AppContext.BaseDirectory.Contains(
                Path.DirectorySeparatorChar + "Release" + Path.DirectorySeparatorChar,
                StringComparison.Ordinal)
                ? "Release"
                : "Debug";
        }

        return Path.Combine(
            repositoryRoot,
            "tools",
            "UnityStructCatalog",
            "bin",
            configuration,
            "net10.0",
            "UnityStructCatalog.dll");
    }

    private static string FindRepositoryRoot()
    {
        foreach (var startingPath in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var directory = new DirectoryInfo(startingPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "global.json")) &&
                    Directory.Exists(Path.Combine(directory.FullName, "tools")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
