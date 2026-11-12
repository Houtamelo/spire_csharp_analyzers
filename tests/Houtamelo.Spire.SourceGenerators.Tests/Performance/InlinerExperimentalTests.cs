using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Houtamelo.Spire;
using Houtamelo.Spire.Analyzers.SourceGenerators.Performance;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.Performance;

public sealed class InlinerExperimentalTests
{
    [Fact]
    public void InlinerSurface_IsMarkedWithTheSharedExperimentalDiagnostic()
    {
        var assembly = typeof(InlinerStructAttribute).Assembly;
        var interfaces = assembly.GetTypes()
            .Where(type => type.IsInterface
                && (string.Equals(type.Name, "IActionInliner", StringComparison.Ordinal)
                    || type.Name.StartsWith("IActionInliner`", StringComparison.Ordinal)
                    || type.Name.StartsWith("IFuncInliner`", StringComparison.Ordinal)))
            .ToArray();

        Assert.Equal(18, interfaces.Length);

        var surface = interfaces
            .Cast<MemberInfo>()
            .Concat(new MemberInfo[]
            {
                typeof(InlinerStructAttribute),
                typeof(InlinableAttribute),
            })
            .ToArray();

        Assert.Equal(20, surface.Length);
        Assert.All(surface, member =>
            Assert.Equal(
                GeneratorTestHelper.InlinerExperimentalDiagnosticId,
                ReadExperimentalDiagnosticId(member)));

        var invokeMethods = interfaces
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .ToArray();
        Assert.Equal(18, invokeMethods.Length);
        Assert.All(invokeMethods, method =>
            Assert.Equal(
                GeneratorTestHelper.InlinerExperimentalDiagnosticId,
                ReadExperimentalDiagnosticId(method)));
    }

    [Fact]
    public void UnsuppressedInlinerAttributeUse_ReportsOnlyTheSharedExperimentalDiagnostic()
    {
        var result = GeneratorTestHelper.RunInlinerStructGenerator(
            InlinerStructSource,
            out var compilation,
            out var generatorDiagnostics,
            path: "experimental-inliner.cs",
            suppressInlinerExperimentalDiagnostic: false);

        if (!HasAccessibleExperimentalAttribute(compilation))
            return;

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);

        var errors = compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.NotEmpty(errors);
        Assert.All(errors, diagnostic =>
            Assert.Equal(
                GeneratorTestHelper.InlinerExperimentalDiagnosticId,
                diagnostic.Id));

        Assert.NotNull(GeneratorTestHelper.GetInlinerGeneratedSource(result));
    }

    [Fact]
    public void UnsuppressedInlinableAttributeUse_ReportsTheSharedExperimentalDiagnostic()
    {
        var result = GeneratorTestHelper.RunInlinableTwinGenerator(
            InlinableSource,
            out var compilation,
            out var generatorDiagnostics,
            path: "experimental-inlinable.cs",
            suppressInlinerExperimentalDiagnostic: false);

        if (!HasAccessibleExperimentalAttribute(compilation))
            return;

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);

        Assert.Contains(
            compilation.GetDiagnostics(),
            diagnostic => diagnostic.Id == GeneratorTestHelper.InlinerExperimentalDiagnosticId);
        Assert.NotNull(GeneratorTestHelper.GetInlinableGeneratedSource(result));
    }

    [Fact]
    public void UnsuppressedInterfaceInvocation_ReportsTheSharedExperimentalDiagnostic()
    {
        var result = GeneratorTestHelper.RunInlinerStructGenerator(
            """
            using Houtamelo.Spire;

            public static class DirectUse
            {
                public static int Run(IFuncInliner<int, int> callback)
                    => callback.Invoke(1);
            }
            """,
            out var compilation,
            out var generatorDiagnostics,
            path: "experimental-interface.cs",
            suppressInlinerExperimentalDiagnostic: false);

        if (!HasAccessibleExperimentalAttribute(compilation))
            return;

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);
        Assert.Contains(
            compilation.GetDiagnostics(),
            diagnostic => diagnostic.Id == GeneratorTestHelper.InlinerExperimentalDiagnosticId);
        Assert.Null(GeneratorTestHelper.GetInlinerGeneratedSource(result));
    }

    [Fact]
    public void ScopedPragma_AllowsDirectInterfaceUseAndRuntimeExecution()
    {
        var result = GeneratorTestHelper.RunInlinerStructGenerator(
            ScopedPragmaSource,
            out var compilation,
            out var generatorDiagnostics,
            path: "scoped-pragma.cs",
            suppressInlinerExperimentalDiagnostic: false);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        var assembly = Emit(compilation);
        var implementationType = assembly.GetType("Scoped.DirectInliner");
        Assert.NotNull(implementationType);
        var implementation = Activator.CreateInstance(implementationType!);
        var invoke = implementationType!.GetMethod("Invoke", new[] { typeof(int) });
        Assert.NotNull(invoke);
        Assert.Equal(8, invoke!.Invoke(implementation, new object[] { 7 }));
    }

    [Fact]
    public void CompilationSuppression_AllowsGeneratedInlinerToEmitAndExecute()
    {
        var result = GeneratorTestHelper.RunInlinerStructGenerator(
            InlinerStructSource,
            out var compilation,
            out var generatorDiagnostics,
            path: "suppressed-inliner.cs",
            suppressInlinerExperimentalDiagnostic: true);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        var generatedSource = GeneratorTestHelper.GetInlinerGeneratedSource(result);
        Assert.NotNull(generatedSource);
        Assert.Equal(
            HasAccessibleExperimentalAttribute(compilation),
            generatedSource!.Contains("Experimental(\"SPIREEXP001\")", StringComparison.Ordinal));

        var assembly = Emit(compilation);
        var inlinerType = assembly.GetType("Runtime.Experimental.Target+IncrementInliner");
        Assert.NotNull(inlinerType);
        var inliner = Activator.CreateInstance(inlinerType!);
        var invoke = inlinerType!.GetMethod("Invoke", new[] { typeof(int) });
        Assert.NotNull(invoke);
        Assert.Equal(8, invoke!.Invoke(inliner, new object[] { 7 }));
    }

    [Fact]
    public void CompilationSuppression_AllowsGeneratedTwinToEmitAndExecute()
    {
        var result = GeneratorTestHelper.RunInlinableTwinGenerator(
            InlinableSource,
            out var compilation,
            out var generatorDiagnostics,
            path: "suppressed-inlinable.cs",
            suppressInlinerExperimentalDiagnostic: true);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);

        var generatedSource = GeneratorTestHelper.GetInlinableGeneratedSource(result);
        Assert.NotNull(generatedSource);
        Assert.Equal(
            HasAccessibleExperimentalAttribute(compilation),
            generatedSource!.Contains("Experimental(\"SPIREEXP001\")", StringComparison.Ordinal));

        var assembly = Emit(compilation);
        var targetType = assembly.GetType("Runtime.Experimental.Target");
        Assert.NotNull(targetType);
        var twin = targetType!.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.IsGenericMethodDefinition);
        var closedTwin = twin.MakeGenericMethod(typeof(Increment));
        Assert.Equal(8, closedTwin.Invoke(null, new object[] { default(Increment), 7 }));
    }

    [Fact]
    public void ScopedSuppression_DoesNotHideUnrelatedCompilerErrors()
    {
        var result = GeneratorTestHelper.RunInlinerStructGenerator(
            UnrelatedErrorSource,
            out var compilation,
            out var generatorDiagnostics,
            path: "unrelated-error.cs",
            suppressInlinerExperimentalDiagnostic: false);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);

        Assert.DoesNotContain(
            compilation.GetDiagnostics(),
            diagnostic => diagnostic.Id == GeneratorTestHelper.InlinerExperimentalDiagnosticId);
        Assert.Contains(compilation.GetDiagnostics(), diagnostic => diagnostic.Id == "CS0103");
        Assert.Null(GeneratorTestHelper.GetInlinerGeneratedSource(result));
    }

    [Fact]
    public void StableSpireApis_DoNotAcquireTheExperimentalDiagnostic()
    {
        var result = GeneratorTestHelper.RunGenerator(
            """
            using Houtamelo.Spire;

            public static class StableApi
            {
                public static System.Type GetAttributeType()
                    => typeof(EnforceInitializationAttribute);

                public static ReferenceStorage AcceptStorage(ReferenceStorage storage)
                    => storage;
            }
            """,
            out var compilation,
            out var diagnostics,
            path: "stable-api.cs");

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(diagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(compilation);
        Assert.DoesNotContain(
            compilation.GetDiagnostics(),
            diagnostic => diagnostic.Id == GeneratorTestHelper.InlinerExperimentalDiagnosticId);
        Assert.Null(GeneratorTestHelper.GetUnionGeneratedSource(result));
    }

    [Fact]
    public void EmittedInlinerMetadata_RemainsExperimentalForDownstreamConsumers()
    {
        var produced = GeneratorTestHelper.RunInlinerStructGenerator(
            InlinerStructSource,
            out var producedCompilation,
            out var generatorDiagnostics,
            path: "published-inliner.cs",
            suppressInlinerExperimentalDiagnostic: true);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(producedCompilation);

        var image = EmitImage(producedCompilation);
        var publishedAssembly = TestHarnessEnvironment.LoadAssembly(image);
        var generatedType = publishedAssembly.GetType(
            "Runtime.Experimental.Target+IncrementInliner");
        Assert.NotNull(generatedType);
        var generatedInvoke = generatedType!.GetMethod("Invoke", new[] { typeof(int) });
        Assert.NotNull(generatedInvoke);
        var hasAccessibleExperimentalAttribute =
            HasAccessibleExperimentalAttribute(producedCompilation);
        Assert.Equal(
            hasAccessibleExperimentalAttribute
                ? GeneratorTestHelper.InlinerExperimentalDiagnosticId
                : null,
            ReadExperimentalDiagnosticId(generatedInvoke!));

        var downstreamTree = CSharpSyntaxTree.ParseText(
            """
            using Runtime.Experimental;

            public static class Downstream
            {
                public static int Run()
                    => default(Target.IncrementInliner).Invoke(7);
            }
            """,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
            path: "downstream.cs");

        var references = TestHarnessEnvironment.FrameworkReferences
            .Append(MetadataReference.CreateFromFile(
                typeof(InlinerStructAttribute).Assembly.Location))
            .Append(MetadataReference.CreateFromImage(image));
        var downstream = CSharpCompilation.Create(
            "DownstreamAssembly",
            new[] { downstreamTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        if (hasAccessibleExperimentalAttribute)
        {
            Assert.Contains(
                downstream.GetDiagnostics(),
                diagnostic => diagnostic.Id == GeneratorTestHelper.InlinerExperimentalDiagnosticId);
        }
        else
        {
            Assert.DoesNotContain(
                downstream.GetDiagnostics(),
                diagnostic => diagnostic.Id == GeneratorTestHelper.InlinerExperimentalDiagnosticId);
        }
    }

    [Fact]
    public void EmittedTwinMetadata_MarksOnlyGeneratedOverloadAndWarnsDownstreamStructCalls()
    {
        var produced = GeneratorTestHelper.RunAllInlinerGenerators(
            CombinedInlinerSource,
            out var producedCompilation,
            out var generatorDiagnostics,
            path: "published-twin.cs",
            suppressInlinerExperimentalDiagnostic: true);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(generatorDiagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(producedCompilation);

        var image = EmitImage(producedCompilation);
        var publishedAssembly = TestHarnessEnvironment.LoadAssembly(image);
        var targetType = publishedAssembly.GetType("Runtime.Experimental.Target");
        Assert.NotNull(targetType);
        var methods = targetType!.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == "Apply")
            .ToArray();
        var ordinaryDelegateMethod = Assert.Single(
            methods,
            method => !method.IsGenericMethodDefinition);
        var generatedTwinMethod = Assert.Single(
            methods,
            method => method.IsGenericMethodDefinition);
        Assert.Null(ReadExperimentalDiagnosticId(ordinaryDelegateMethod));
        Assert.Equal(
            GeneratorTestHelper.InlinerExperimentalDiagnosticId,
            ReadExperimentalDiagnosticId(generatedTwinMethod));
        Assert.Null(ReadExperimentalDiagnosticId(targetType));

        var references = TestHarnessEnvironment.FrameworkReferences
            .Append(MetadataReference.CreateFromFile(
                typeof(InlinerStructAttribute).Assembly.Location))
            .Append(MetadataReference.CreateFromImage(image));
        var experimentalCall = CSharpCompilation.Create(
            "DownstreamTwinAssembly",
            new[]
            {
                CSharpSyntaxTree.ParseText(
                    """
                    using Runtime.Experimental;

                    public static class DownstreamTwin
                    {
                        public static int Run()
                            => Target.Apply(default(Target.IncrementInliner), 7);
                    }
                    """,
                    CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
                    path: "downstream-twin.cs"),
            },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.Contains(
            experimentalCall.GetDiagnostics(),
            diagnostic => diagnostic.Id == GeneratorTestHelper.InlinerExperimentalDiagnosticId);

        var ordinaryCall = CSharpCompilation.Create(
            "DownstreamDelegateAssembly",
            new[]
            {
                CSharpSyntaxTree.ParseText(
                    """
                    using System;
                    using Runtime.Experimental;

                    public static class DownstreamDelegate
                    {
                        public static int Run()
                            => Target.Apply((Func<int, int>)(value => value + 1), 7);
                    }
                    """,
                    CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
                    path: "downstream-delegate.cs"),
            },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.DoesNotContain(
            ordinaryCall.GetDiagnostics(),
            diagnostic => diagnostic.Id == GeneratorTestHelper.InlinerExperimentalDiagnosticId);
    }

    [Fact]
    public async Task DownlevelReferences_DoNotRequireAnAccessibleExperimentalAttribute()
    {
        var references = await ReferenceAssemblies.NetStandard.NetStandard20.ResolveAsync(
            LanguageNames.CSharp,
            CancellationToken.None);
        references = references.Add(MetadataReference.CreateFromFile(
            typeof(InlinerStructAttribute).Assembly.Location));
        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithSpecificDiagnosticOptions(
                ImmutableDictionary<string, ReportDiagnostic>.Empty.SetItem(
                    GeneratorTestHelper.InlinerExperimentalDiagnosticId,
                    ReportDiagnostic.Suppress));

        var inlinerTree = CSharpSyntaxTree.ParseText(
            InlinerStructSource,
            parseOptions,
            path: "downlevel-inliner.cs");
        var inlinerCompilation = CSharpCompilation.Create(
            "DownlevelInlinerAssembly",
            new[] { inlinerTree },
            references,
            options);
        var inlinerDriver = CSharpGeneratorDriver.Create(new InlinerStructGenerator())
            .RunGeneratorsAndUpdateCompilation(
                inlinerCompilation,
                out var generatedInlinerCompilation,
                out var inlinerDiagnostics);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(inlinerDiagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(generatedInlinerCompilation);
        var generatedInlinerSource = GeneratorTestHelper.GetInlinerGeneratedSource(
            inlinerDriver.GetRunResult());
        Assert.NotNull(generatedInlinerSource);
        Assert.DoesNotContain(
            "Experimental(\"SPIREEXP001\")",
            generatedInlinerSource!,
            StringComparison.Ordinal);

        var twinTree = CSharpSyntaxTree.ParseText(
            InlinableSource,
            parseOptions,
            path: "downlevel-inlinable.cs");
        var twinCompilation = CSharpCompilation.Create(
            "DownlevelInlinableAssembly",
            new[] { twinTree },
            references,
            options);
        var twinDriver = CSharpGeneratorDriver.Create(new InlinableTwinGenerator())
            .RunGeneratorsAndUpdateCompilation(
                twinCompilation,
                out var generatedTwinCompilation,
                out var twinDiagnostics);

        GeneratorTestHelper.AssertNoGeneratorDiagnostics(twinDiagnostics);
        GeneratorTestHelper.AssertNoCompilationErrors(generatedTwinCompilation);
        var generatedTwinSource = GeneratorTestHelper.GetInlinableGeneratedSource(
            twinDriver.GetRunResult());
        Assert.NotNull(generatedTwinSource);
        Assert.DoesNotContain(
            "Experimental(\"SPIREEXP001\")",
            generatedTwinSource!,
            StringComparison.Ordinal);
    }

    private static string? ReadExperimentalDiagnosticId(MemberInfo member)
        => CustomAttributeData.GetCustomAttributes(member)
            .Where(attribute => string.Equals(
                attribute.AttributeType.FullName,
                "System.Diagnostics.CodeAnalysis.ExperimentalAttribute",
                StringComparison.Ordinal))
            .Select(attribute => attribute.ConstructorArguments.Single().Value as string)
            .SingleOrDefault();

    private static bool HasAccessibleExperimentalAttribute(Compilation compilation)
    {
        var type = compilation.GetTypeByMetadataName(
            "System.Diagnostics.CodeAnalysis.ExperimentalAttribute");
        return type is not null
            && compilation.IsSymbolAccessibleWithin(type, compilation.Assembly);
    }

    private static Assembly Emit(Compilation compilation)
        => TestHarnessEnvironment.LoadAssembly(EmitImage(compilation));

    private static byte[] EmitImage(Compilation compilation)
    {
        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        return stream.ToArray();
    }

    private readonly struct Increment : IFuncInliner<int, int>
    {
        public int Invoke(int value) => value + 1;
    }

    private const string InlinerStructSource = """
        using Houtamelo.Spire;

        namespace Runtime.Experimental
        {
            public static partial class Target
            {
                [InlinerStruct]
                public static int Increment(int value) => value + 1;
            }
        }
        """;

    private const string InlinableSource = """
        using System;
        using Houtamelo.Spire;

        namespace Runtime.Experimental
        {
            public static partial class Target
            {
                public static int Apply([Inlinable] Func<int, int> callback, int value)
                    => callback(value);
            }
        }
        """;

    private const string CombinedInlinerSource = """
        using System;
        using Houtamelo.Spire;

        namespace Runtime.Experimental
        {
            public static partial class Target
            {
                [InlinerStruct]
                public static int Increment(int value) => value + 1;

                public static int Apply([Inlinable] Func<int, int> callback, int value)
                    => callback(value);
            }
        }
        """;

    private const string ScopedPragmaSource = """
        #pragma warning disable SPIREEXP001
        using Houtamelo.Spire;

        namespace Scoped
        {
            public readonly struct DirectInliner : IFuncInliner<int, int>
            {
                public int Invoke(int value) => value + 1;
            }
        }
        #pragma warning restore SPIREEXP001
        """;

    private const string UnrelatedErrorSource = """
        #pragma warning disable SPIREEXP001
        using Houtamelo.Spire;

        namespace Broken
        {
            public readonly struct DirectInliner : IFuncInliner<int, int>
            {
                public int Invoke(int value) => MissingMethod(value);
            }
        }
        #pragma warning restore SPIREEXP001
        """;
}
