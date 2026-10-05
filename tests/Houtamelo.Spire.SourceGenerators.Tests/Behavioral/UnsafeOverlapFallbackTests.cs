using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Houtamelo.Spire.Analyzers.SourceGenerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Behavioral;

public sealed class UnsafeOverlapFallbackTests : BehavioralTestBase
{
    private static readonly MetadataReference CoreReference =
        MetadataReference.CreateFromFile(typeof(Houtamelo.Spire.EnforceInitializationAttribute).Assembly.Location);

    private static readonly Lazy<Task<ImmutableArray<MetadataReference>>> FallbackReferences =
        new(ResolveFallbackReferencesAsync);

    [Fact]
    public async Task FallbackCompilation_UsesOldReferences()
    {
        var result = await GenerateAsync(FallbackOffsetSource);

        AssertSuccessfulGeneration(result);
        Assert.DoesNotContain("InlineArray", string.Join("\n", result.GeneratedSources));

        var assembly = EmitAndLoad(result.OutputCompilation);
        Assert.NotNull(assembly.GetType("FallbackOffsetUnion"));
    }

    [Fact]
    public async Task OffsetZeroAndNonzeroFields_RoundTrip()
    {
        var result = await GenerateAsync(FallbackOffsetSource);
        AssertSuccessfulGeneration(result);

        var assembly = EmitAndLoad(result.OutputCompilation);
        var unionType = GetType(assembly, "FallbackOffsetUnion");

        var zero = InvokeFactory(unionType, "Zero", 17);
        Assert.Equal(GetKindValue(unionType, "Zero"), ReadKind(zero));
        Assert.Equal(17, ReadProperty(zero, "value"));

        var wide = InvokeFactory(unionType, "Wide", 1_234_567_890_123L, -9_876_543_210L);
        Assert.Equal(GetKindValue(unionType, "Wide"), ReadKind(wide));
        Assert.Equal(1_234_567_890_123L, ReadProperty(wide, "first"));
        Assert.Equal(-9_876_543_210L, ReadProperty(wide, "second"));
        var wideObject = InvokeDeconstruct(wide, 3);
        Assert.Equal(GetKindValue(unionType, "Wide"), Convert.ToInt32(wideObject[0]));
        Assert.Equal(1_234_567_890_123L, wideObject[1]);
        Assert.Equal(-9_876_543_210L, wideObject[2]);

        var tail = InvokeFactory(unionType, "Tail", -1234.5, 87654321);
        Assert.Equal(GetKindValue(unionType, "Tail"), ReadKind(tail));
        Assert.Equal(-1234.5, ReadProperty(tail, "tail"));
        Assert.Equal(87654321, ReadProperty(tail, "code"));
        var tailObject = InvokeDeconstruct(tail, 3);
        Assert.Equal(GetKindValue(unionType, "Tail"), Convert.ToInt32(tailObject[0]));
        Assert.Equal(-1234.5, tailObject[1]);
        Assert.Equal(87654321, tailObject[2]);
    }

    [Fact]
    public async Task FieldlessFactory_HasKindAndNullObjectPayload()
    {
        var result = await GenerateAsync(FallbackOffsetSource);
        AssertSuccessfulGeneration(result);

        var assembly = EmitAndLoad(result.OutputCompilation);
        var unionType = GetType(assembly, "FallbackOffsetUnion");
        var empty = InvokeFactory(unionType, "Empty");

        Assert.Equal(GetKindValue(unionType, "Empty"), ReadKind(empty));
        var deconstructed = InvokeDeconstruct(empty, 2);
        Assert.Equal(GetKindValue(unionType, "Empty"), Convert.ToInt32(deconstructed[0]));
        Assert.Null(deconstructed[1]);
    }

    [Fact]
    public async Task Deconstruct_UsesTypedAndObjectOverloads()
    {
        var result = await GenerateAsync(FallbackDeconstructSource);
        AssertSuccessfulGeneration(result);

        var assembly = EmitAndLoad(result.OutputCompilation);
        var unionType = GetType(assembly, "FallbackDeconstructUnion");

        var pair = InvokeFactory(unionType, "Pair", 987_654_321_012L, -42);
        var typed = InvokeDeconstruct(pair, 3);
        Assert.Equal(GetKindValue(unionType, "Pair"), Convert.ToInt32(typed[0]));
        Assert.Equal(987_654_321_012L, typed[1]);
        Assert.Equal(-42, typed[2]);

        var unmatchedObject = InvokeDeconstruct(pair, 2);
        Assert.Equal(GetKindValue(unionType, "Pair"), Convert.ToInt32(unmatchedObject[0]));
        Assert.Null(unmatchedObject[1]);

        var intOne = InvokeFactory(unionType, "IntOne", 123456);
        var intPayload = InvokeDeconstruct(intOne, 2);
        Assert.Equal(GetKindValue(unionType, "IntOne"), Convert.ToInt32(intPayload[0]));
        Assert.Equal(123456, intPayload[1]);

        var textOne = InvokeFactory(unionType, "TextOne", "fallback");
        var textPayload = InvokeDeconstruct(textOne, 2);
        Assert.Equal(GetKindValue(unionType, "TextOne"), Convert.ToInt32(textPayload[0]));
        Assert.Equal("fallback", textPayload[1]);

        var empty = InvokeFactory(unionType, "Empty");
        var emptyPayload = InvokeDeconstruct(empty, 2);
        Assert.Equal(GetKindValue(unionType, "Empty"), Convert.ToInt32(emptyPayload[0]));
        Assert.Null(emptyPayload[1]);
    }

    [Fact]
    public async Task ManagedOnlyFields_PreserveValuesAndNull()
    {
        var result = await GenerateAsync(FallbackManagedSource);
        AssertSuccessfulGeneration(result);

        var assembly = EmitAndLoad(result.OutputCompilation);
        var unionType = GetType(assembly, "FallbackManagedUnion");

        var text = InvokeFactory(unionType, "Text", "managed text");
        Assert.Equal("managed text", ReadProperty(text, "text"));
        var textPayload = InvokeDeconstruct(text, 2);
        Assert.Same("managed text", textPayload[1]);

        var exception = new InvalidOperationException("managed exception");
        var error = InvokeFactory(unionType, "Error", exception);
        Assert.Same(exception, ReadProperty(error, "exception"));
        var errorPayload = InvokeDeconstruct(error, 2);
        Assert.Same(exception, errorPayload[1]);

        var context = new object();
        var payload = InvokeFactory(unionType, "Payload", context);
        Assert.Same(context, ReadProperty(payload, "context"));

        var nullText = InvokeFactory(unionType, "Text", (object)null!);
        Assert.Null(ReadProperty(nullText, "text"));

        var empty = InvokeFactory(unionType, "Empty");
        Assert.Null(InvokeDeconstruct(empty, 2)[1]);
    }

    [Fact]
    public async Task MixedFields_KeepManagedAndUnmanagedStorageSeparate()
    {
        var result = await GenerateAsync(FallbackMixedSource);
        AssertSuccessfulGeneration(result);

        var assembly = EmitAndLoad(result.OutputCompilation);
        var unionType = GetType(assembly, "FallbackMixedUnion");

        var packet = InvokeFactory(unionType, "Packet", 77, "packet");
        Assert.Equal(77, ReadProperty(packet, "sequence"));
        Assert.Equal("packet", ReadProperty(packet, "label"));

        var scalar = InvokeFactory(unionType, "Scalar", -9876.25);
        Assert.Equal(-9876.25, ReadProperty(scalar, "magnitude"));

        var notice = InvokeFactory(unionType, "Notice", "notice");
        Assert.Equal("notice", ReadProperty(notice, "message"));

        var empty = InvokeFactory(unionType, "Empty");
        Assert.Equal(GetKindValue(unionType, "Empty"), ReadKind(empty));
    }

    [Fact]
    public async Task ValueCopy_InitPropertiesDoNotAliasFallbackStorage()
    {
        var result = await GenerateAsync(FallbackValueSemanticsSource);
        AssertSuccessfulGeneration(result);

        var assembly = EmitAndLoad(result.OutputCompilation);
        var runnerType = GetType(assembly, "FallbackValueSemantics");
        var values = (long[])InvokeMethod(runnerType, "CopyAndModifyByteFields")!;

        Assert.Equal(111L, values[0]);
        Assert.Equal(222L, values[1]);
        Assert.Equal(333L, values[2]);
        Assert.Equal(444L, values[3]);

        var managedValues = (object[])InvokeMethod(runnerType, "CopyAndModifyManagedField")!;
        Assert.Equal(7, managedValues[0]);
        Assert.Equal(8, managedValues[1]);
        Assert.Equal("original", managedValues[2]);
        Assert.Equal("changed", managedValues[3]);
    }

    [Fact]
    public async Task EntirelyFieldlessUnion_GeneratesWithoutBufferStorage()
    {
        var result = await GenerateAsync(FallbackFieldlessSource);
        AssertSuccessfulGeneration(result);

        var assembly = EmitAndLoad(result.OutputCompilation);
        var unionType = GetType(assembly, "FallbackFieldlessUnion");

        var empty = InvokeFactory(unionType, "Empty");
        Assert.Equal(GetKindValue(unionType, "Empty"), ReadKind(empty));

        var other = InvokeFactory(unionType, "Other");
        Assert.Equal(GetKindValue(unionType, "Other"), ReadKind(other));
    }

    [Fact]
    public async Task SmallestBuffer_ByteRoundTripAndDefault()
    {
        var result = await GenerateAsync(FallbackSmallestSource);
        AssertSuccessfulGeneration(result);

        var assembly = EmitAndLoad(result.OutputCompilation);
        var unionType = GetType(assembly, "FallbackSmallestUnion");

        var value = InvokeFactory(unionType, "Value", (byte)0xA5);
        Assert.Equal(GetKindValue(unionType, "Value"), ReadKind(value));
        Assert.Equal((byte)0xA5, ReadProperty(value, "value"));

        var empty = InvokeFactory(unionType, "Empty");
        Assert.Equal(GetKindValue(unionType, "Empty"), ReadKind(empty));
        Assert.Equal((byte)0, ReadProperty(empty, "value"));
    }

    [Fact]
    public async Task GetOnlyFallback_NetCoreApp31_CompilesAndRuns()
    {
        var result = await GenerateWithoutInitAsync(FallbackSmallestSource);
        AssertSuccessfulGeneration(result);

        var assembly = EmitAndLoad(result.OutputCompilation);
        var unionType = GetType(assembly, "FallbackSmallestUnion");

        var value = InvokeFactory(unionType, "Value", (byte)0x5A);
        Assert.Equal(GetKindValue(unionType, "Value"), ReadKind(value));
        Assert.Equal((byte)0x5A, ReadProperty(value, "value"));

        var deconstructed = InvokeDeconstruct(value, 2);
        Assert.Equal(GetKindValue(unionType, "Value"), Convert.ToInt32(deconstructed[0]));
        Assert.Equal((byte)0x5A, deconstructed[1]);
    }

    [Fact]
    public async Task InitFallback_NetCoreApp31_LocalShim_CompilesAndRuns()
    {
        var result = await GenerateWithInitAsync(FallbackValueSemanticsWithShimSource);
        AssertSuccessfulGeneration(result);

        var assembly = EmitAndLoad(result.OutputCompilation);
        var runnerType = GetType(assembly, "FallbackValueSemantics");
        var values = (long[])InvokeMethod(runnerType, "CopyAndModifyByteFields")!;

        Assert.Equal(111L, values[0]);
        Assert.Equal(222L, values[1]);
        Assert.Equal(333L, values[2]);
        Assert.Equal(444L, values[3]);

        var managedValues = (object[])InvokeMethod(runnerType, "CopyAndModifyManagedField")!;
        Assert.Equal(7, managedValues[0]);
        Assert.Equal(8, managedValues[1]);
        Assert.Equal("original", managedValues[2]);
        Assert.Equal("changed", managedValues[3]);
    }

    [Fact]
    public async Task UnsafeDisabled_ReportsSPIRE_DU009()
    {
        var result = await GenerateAsync(FallbackOffsetSource, allowUnsafe: false);

        var unsafeDiagnostics = result.GeneratorDiagnostics
            .Where(diagnostic => diagnostic.Id == "SPIRE_DU009")
            .ToList();

        Assert.Single(unsafeDiagnostics);
        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void ModernCompilation_StillUsesInlineArray()
    {
        var runResult = GeneratorTestHelper.RunGenerator(
            BasicShapeSource("Layout.UnsafeOverlap"),
            out var outputCompilation,
            out var diagnostics);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning);
        Assert.NotNull(outputCompilation.GetTypeByMetadataName(
            "System.Runtime.CompilerServices.InlineArrayAttribute"));

        var generated = string.Join("\n", runResult.GeneratedTrees.Select(tree => tree.GetText().ToString()));
        Assert.Contains("InlineArray", generated);

        var compilationErrors = outputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToList();
        Assert.True(compilationErrors.Count == 0, FormatDiagnostics(
            "Modern compilation diagnostics", compilationErrors));

        var assembly = EmitAndLoad(outputCompilation);
        Assert.NotNull(GetType(assembly, "Shape"));
    }

    private static async Task<GenerationResult> GenerateAsync(
        string source,
        bool allowUnsafe = true)
    {
        var references = await FallbackReferences.Value;
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp10);
        var tree = CSharpSyntaxTree.ParseText(source, parseOptions, path: "fallback.cs");
        var compilation = CSharpCompilation.Create(
            $"UnsafeOverlapFallback_{Guid.NewGuid():N}",
            new[] { tree },
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: allowUnsafe,
                nullableContextOptions: NullableContextOptions.Enable));

        var inlineArraySymbol = compilation.GetTypeByMetadataName(
            "System.Runtime.CompilerServices.InlineArrayAttribute");
        Assert.True(inlineArraySymbol is null,
            "The net6 target unexpectedly exposes InlineArrayAttribute. References:\n" +
            string.Join("\n", references.Select(reference => reference.Display)));

        var generator = new DiscriminatedUnionGenerator();
        var driver = CSharpGeneratorDriver.Create(
            generators: new[] { generator.AsSourceGenerator() },
            parseOptions: parseOptions)
            .RunGeneratorsAndUpdateCompilation(
                compilation,
                out var outputCompilation,
                out var generatorDiagnostics);

        var runResult = driver.GetRunResult();
        var generatedSources = runResult.GeneratedTrees
            .Select(tree => tree.GetText().ToString())
            .ToImmutableArray();

        return new GenerationResult(
            outputCompilation,
            generatorDiagnostics,
            generatedSources);
    }

    private static Task<GenerationResult> GenerateWithoutInitAsync(string source)
        => GenerateNetCoreApp31Async(source, requireAccessibleInit: false);

    private static Task<GenerationResult> GenerateWithInitAsync(string source)
        => GenerateNetCoreApp31Async(source, requireAccessibleInit: true);

    private static async Task<GenerationResult> GenerateNetCoreApp31Async(
        string source,
        bool requireAccessibleInit)
    {
        var references = await ReferenceAssemblies.NetCore.NetCoreApp31.ResolveAsync(
            LanguageNames.CSharp,
            CancellationToken.None);
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp10);
        var tree = CSharpSyntaxTree.ParseText(source, parseOptions, path: "legacy-fallback.cs");
        var referencesWithCore = references.Add(CoreReference);
        var compilation = CSharpCompilation.Create(
            $"UnsafeOverlapGetOnly_{Guid.NewGuid():N}",
            new[] { tree },
            referencesWithCore,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true,
                nullableContextOptions: NullableContextOptions.Enable));

        Assert.Null(compilation.GetTypeByMetadataName(
            "System.Runtime.CompilerServices.InlineArrayAttribute"));
        Assert.NotNull(compilation.GetTypeByMetadataName(
            "System.Runtime.CompilerServices.Unsafe"));

        var referencedOrSourceInit = compilation.GetTypeByMetadataName(
            "System.Runtime.CompilerServices.IsExternalInit");
        var sourceInit = compilation.Assembly.GetTypeByMetadataName(
            "System.Runtime.CompilerServices.IsExternalInit");
        if (requireAccessibleInit)
        {
            Assert.NotNull(sourceInit);
            Assert.True(
                compilation.IsSymbolAccessibleWithin(sourceInit!, compilation.Assembly),
                $"The source IsExternalInit shim must be accessible. " +
                $"Assembly: {sourceInit!.ContainingAssembly.Identity}; " +
                $"Accessibility: {sourceInit.DeclaredAccessibility}");
        }
        else
        {
            Assert.True(
                referencedOrSourceInit is null
                    || !compilation.IsSymbolAccessibleWithin(referencedOrSourceInit, compilation.Assembly),
                $"IsExternalInit must be inaccessible to the target. " +
                $"Assembly: {referencedOrSourceInit?.ContainingAssembly?.Identity}; " +
                $"Accessibility: {referencedOrSourceInit?.DeclaredAccessibility}");
        }

        var generator = new DiscriminatedUnionGenerator();
        var driver = CSharpGeneratorDriver.Create(
            generators: new[] { generator.AsSourceGenerator() },
            parseOptions: parseOptions)
            .RunGeneratorsAndUpdateCompilation(
                compilation,
                out var outputCompilation,
                out var generatorDiagnostics);

        var generatedSources = driver.GetRunResult().GeneratedTrees
            .Select(generatedTree => generatedTree.GetText().ToString())
            .ToImmutableArray();

        return new GenerationResult(
            outputCompilation,
            generatorDiagnostics,
            generatedSources);
    }

    private static async Task<ImmutableArray<MetadataReference>> ResolveFallbackReferencesAsync()
    {
        var references = await ReferenceAssemblies.Net.Net60.ResolveAsync(
            LanguageNames.CSharp,
            CancellationToken.None);

        var probeTree = CSharpSyntaxTree.ParseText(
            "public sealed class UnsafeReferenceProbe { }",
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp10));
        var probeCompilation = CSharpCompilation.Create(
            "UnsafeReferenceProbe",
            new[] { probeTree },
            references);
        if (probeCompilation.GetTypeByMetadataName(
                "System.Runtime.CompilerServices.Unsafe") is null)
        {
            throw new InvalidOperationException(
                "ReferenceAssemblies.Net.Net60 did not resolve System.Runtime.CompilerServices.Unsafe.");
        }

        return references.Add(CoreReference);
    }

    private static void AssertSuccessfulGeneration(GenerationResult result)
    {
        var generatorErrors = result.GeneratorDiagnostics
            .Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
            .ToList();
        Assert.True(generatorErrors.Count == 0, FormatDiagnostics(
            "Generator diagnostics", generatorErrors));

        var compilationErrors = result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToList();
        Assert.True(compilationErrors.Count == 0, FormatDiagnostics(
            "Compilation diagnostics", compilationErrors));
    }

    private static Assembly EmitAndLoad(Compilation compilation)
    {
        using var stream = new MemoryStream();
        EmitResult emitResult = compilation.Emit(stream);
        var errors = emitResult.Diagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToList();
        Assert.True(emitResult.Success, FormatDiagnostics("Emit diagnostics", errors));

        return Assembly.Load(stream.ToArray());
    }

    private static string FormatDiagnostics(string title, IEnumerable<RoslynDiagnostic> diagnostics)
        => $"{title}:{Environment.NewLine}{string.Join(Environment.NewLine,
            diagnostics.Select(diagnostic => $"  {diagnostic.Id}: {diagnostic.GetMessage()}"))}";

    private sealed record GenerationResult(
        Compilation OutputCompilation,
        ImmutableArray<RoslynDiagnostic> GeneratorDiagnostics,
        ImmutableArray<string> GeneratedSources);

    private const string FallbackOffsetSource = """
        #nullable enable
        using Houtamelo.Spire;

        [DiscriminatedUnion(Layout.UnsafeOverlap)]
        public partial struct FallbackOffsetUnion
        {
            [Variant] public static partial FallbackOffsetUnion Zero(int value);
            [Variant] public static partial FallbackOffsetUnion Wide(long first, long second);
            [Variant] public static partial FallbackOffsetUnion Tail(double tail, int code);
            [Variant] public static partial FallbackOffsetUnion Empty();
        }
        """;

    private const string FallbackDeconstructSource = """
        #nullable enable
        using Houtamelo.Spire;

        [DiscriminatedUnion(Layout.UnsafeOverlap)]
        public partial struct FallbackDeconstructUnion
        {
            [Variant] public static partial FallbackDeconstructUnion IntOne(int value);
            [Variant] public static partial FallbackDeconstructUnion TextOne(string text);
            [Variant] public static partial FallbackDeconstructUnion Pair(long left, int right);
            [Variant] public static partial FallbackDeconstructUnion Empty();
        }
        """;

    private const string FallbackManagedSource = """
        #nullable enable
        using System;
        using Houtamelo.Spire;

        [DiscriminatedUnion(Layout.UnsafeOverlap)]
        public partial struct FallbackManagedUnion
        {
            [Variant] public static partial FallbackManagedUnion Text(string text);
            [Variant] public static partial FallbackManagedUnion Error(Exception exception);
            [Variant] public static partial FallbackManagedUnion Payload(object context);
            [Variant] public static partial FallbackManagedUnion Empty();
        }
        """;

    private const string FallbackMixedSource = """
        #nullable enable
        using Houtamelo.Spire;

        [DiscriminatedUnion(Layout.UnsafeOverlap)]
        public partial struct FallbackMixedUnion
        {
            [Variant] public static partial FallbackMixedUnion Packet(int sequence, string label);
            [Variant] public static partial FallbackMixedUnion Scalar(double magnitude);
            [Variant] public static partial FallbackMixedUnion Notice(string message);
            [Variant] public static partial FallbackMixedUnion Empty();
        }
        """;

    private const string FallbackValueSemanticsSource = """
        #nullable enable
        using System;
        using Houtamelo.Spire;

        [DiscriminatedUnion(Layout.UnsafeOverlap)]
        public partial struct FallbackValueUnion
        {
            [Variant] public static partial FallbackValueUnion Wide(long first, long second);
            [Variant] public static partial FallbackValueUnion Packet(int sequence, string label);
        }

        public static class FallbackValueSemantics
        {
            public static long[] CopyAndModifyByteFields()
            {
                var original = FallbackValueUnion.Wide(111L, 222L);
                var copy = original with { first = 333L, second = 444L };
                return new[] { original.first, original.second, copy.first, copy.second };
            }

            public static object[] CopyAndModifyManagedField()
            {
                var original = FallbackValueUnion.Packet(7, "original");
                var copy = original with { sequence = 8, label = "changed" };
                return new object[] { original.sequence, copy.sequence, original.label, copy.label };
            }
        }
        """;

    private const string FallbackValueSemanticsWithShimSource =
        FallbackValueSemanticsSource + """

        namespace System.Runtime.CompilerServices
        {
            internal static class IsExternalInit
            {
            }
        }
        """;

    private const string FallbackFieldlessSource = """
        #nullable enable
        using Houtamelo.Spire;

        [DiscriminatedUnion(Layout.UnsafeOverlap)]
        public partial struct FallbackFieldlessUnion
        {
            [Variant] public static partial FallbackFieldlessUnion Empty();
            [Variant] public static partial FallbackFieldlessUnion Other();
        }
        """;

    private const string FallbackSmallestSource = """
        #nullable enable
        using Houtamelo.Spire;

        [DiscriminatedUnion(Layout.UnsafeOverlap)]
        public partial struct FallbackSmallestUnion
        {
            [Variant] public static partial FallbackSmallestUnion Value(byte value);
            [Variant] public static partial FallbackSmallestUnion Empty();
        }
        """;

}
