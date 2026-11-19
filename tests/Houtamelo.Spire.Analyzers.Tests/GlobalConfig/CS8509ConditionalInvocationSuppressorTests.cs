using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Houtamelo.Spire;
using Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
#if SPIRE_UNITY_TEST_HOST
using Spire.Tests.Hosting;
#endif

namespace Houtamelo.Spire.Analyzers.Tests.GlobalConfig;

public sealed class CS8509ConditionalInvocationSuppressorTests
{
    private static readonly MetadataReference CoreAssemblyReference =
        MetadataReference.CreateFromFile(typeof(EnforceExhaustivenessAttribute).Assembly.Location);

    private static readonly MetadataReference AnalyzerAssemblyReference =
        MetadataReference.CreateFromFile(typeof(CS8509Suppressor).Assembly.Location);

    [Fact]
    public async Task ConditionalInvocation_AllHairRoutesCovered_SuppressesCompilerWarning()
    {
        await AssertSuppressedAsync("""
            #nullable enable
            using System;
            using Houtamelo.Spire;

            class C
            {
                [EnforceExhaustiveness]
                private enum HairRoute { Back, Front, Color }
                private enum AdjustmentTarget { HairBack, HairFront, HairColor }

                private event Action<AdjustmentTarget>? on_adjustment_selected;

                private void Apply(HairRoute route)
                {
                    on_adjustment_selected?.Invoke(route switch
                    {
                        HairRoute.Back  => AdjustmentTarget.HairBack,
                        HairRoute.Front => AdjustmentTarget.HairFront,
                        HairRoute.Color => AdjustmentTarget.HairColor,
                    });
                }
            }
            """);
    }

    [Fact]
    public async Task ConditionalInvocation_ExpressionBodiedSwitch_SuppressesCompilerWarning()
    {
        await AssertSuppressedAsync("""
            #nullable enable
            using System;
            using Houtamelo.Spire;

            [EnforceExhaustiveness]
            enum HairRoute { Back, Front, Color }
            enum AdjustmentTarget { HairBack, HairFront, HairColor }

            class C
            {
                public Action<AdjustmentTarget>? on_adjustment_selected;

                private static AdjustmentTarget Select(HairRoute route) => route switch
                {
                    HairRoute.Back  => AdjustmentTarget.HairBack,
                    HairRoute.Front => AdjustmentTarget.HairFront,
                    HairRoute.Color => AdjustmentTarget.HairColor,
                };

                public void Apply(HairRoute route)
                    => on_adjustment_selected?.Invoke(Select(route));
            }
            """);
    }

    [Fact]
    public async Task ConditionalInvocation_LocalSwitchThenInvoke_SuppressesCompilerWarning()
    {
        await AssertSuppressedAsync("""
            #nullable enable
            using System;
            using Houtamelo.Spire;

            [EnforceExhaustiveness]
            enum HairRoute { Back, Front, Color }
            enum AdjustmentTarget { HairBack, HairFront, HairColor }

            class C
            {
                public Action<AdjustmentTarget>? on_adjustment_selected;

                public void Apply(HairRoute route)
                {
                    var target = route switch
                    {
                        HairRoute.Back  => AdjustmentTarget.HairBack,
                        HairRoute.Front => AdjustmentTarget.HairFront,
                        HairRoute.Color => AdjustmentTarget.HairColor,
                    };
                    on_adjustment_selected?.Invoke(target);
                }
            }
            """);
    }

    [Fact]
    public async Task DirectInvocation_AllHairRoutesCovered_SuppressesCompilerWarning()
    {
        await AssertSuppressedAsync("""
            #nullable enable
            using System;
            using Houtamelo.Spire;

            [EnforceExhaustiveness]
            enum HairRoute { Back, Front, Color }
            enum AdjustmentTarget { HairBack, HairFront, HairColor }

            class C
            {
                public Action<AdjustmentTarget>? on_adjustment_selected;

                public void Apply(HairRoute route)
                {
                    on_adjustment_selected!.Invoke(route switch
                    {
                        HairRoute.Back  => AdjustmentTarget.HairBack,
                        HairRoute.Front => AdjustmentTarget.HairFront,
                        HairRoute.Color => AdjustmentTarget.HairColor,
                    });
                }
            }
            """);
    }

    [Fact]
    public async Task ConditionalInvocation_InLambda_AllHairRoutesCovered_SuppressesCompilerWarning()
    {
        await AssertSuppressedAsync("""
            #nullable enable
            using System;
            using Houtamelo.Spire;

            [EnforceExhaustiveness]
            enum HairRoute { Back, Front, Color }
            enum AdjustmentTarget { HairBack, HairFront, HairColor }

            class C
            {
                public Action<AdjustmentTarget>? on_adjustment_selected;

                public Action<HairRoute> MakeCallback()
                    => route => on_adjustment_selected?.Invoke(route switch
                    {
                        HairRoute.Back  => AdjustmentTarget.HairBack,
                        HairRoute.Front => AdjustmentTarget.HairFront,
                        HairRoute.Color => AdjustmentTarget.HairColor,
                    });
            }
            """);
    }

    [Fact]
    public async Task ConditionalInvocation_MissingHairRoute_LeavesCompilerWarningVisible()
    {
        await AssertUnsuppressedAsync("""
            #nullable enable
            using System;
            using Houtamelo.Spire;

            [EnforceExhaustiveness]
            enum HairRoute { Back, Front, Color }
            enum AdjustmentTarget { HairBack, HairFront, HairColor }

            class C
            {
                public Action<AdjustmentTarget>? on_adjustment_selected;

                public void Apply(HairRoute route)
                {
                    on_adjustment_selected?.Invoke(route switch
                    {
                        HairRoute.Back  => AdjustmentTarget.HairBack,
                        HairRoute.Front => AdjustmentTarget.HairFront,
                    });
                }
            }
            """);
    }

    [Fact]
    public async Task ConditionalInvocation_GuardedHairRoute_LeavesCompilerWarningVisible()
    {
        await AssertUnsuppressedAsync("""
            #nullable enable
            using System;
            using Houtamelo.Spire;

            [EnforceExhaustiveness]
            enum HairRoute { Back, Front, Color }
            enum AdjustmentTarget { HairBack, HairFront, HairColor }

            class C
            {
                public Action<AdjustmentTarget>? on_adjustment_selected;

                private static bool ShouldHandleColor() => false;

                public void Apply(HairRoute route)
                {
                    on_adjustment_selected?.Invoke(route switch
                    {
                        HairRoute.Back  => AdjustmentTarget.HairBack,
                        HairRoute.Front => AdjustmentTarget.HairFront,
                        HairRoute.Color when ShouldHandleColor() => AdjustmentTarget.HairColor,
                    });
                }
            }
            """);
    }

    [Fact]
    public async Task ConditionalInvocation_NullableHairRouteWithoutNullArm_LeavesCompilerWarningVisible()
    {
        await AssertUnsuppressedAsync("""
            #nullable enable
            using System;
            using Houtamelo.Spire;

            [EnforceExhaustiveness]
            enum HairRoute { Back, Front, Color }
            enum AdjustmentTarget { HairBack, HairFront, HairColor }

            class C
            {
                public Action<AdjustmentTarget>? on_adjustment_selected;

                public void Apply(HairRoute? route)
                {
                    on_adjustment_selected?.Invoke(route switch
                    {
                        HairRoute.Back  => AdjustmentTarget.HairBack,
                        HairRoute.Front => AdjustmentTarget.HairFront,
                        HairRoute.Color => AdjustmentTarget.HairColor,
                    });
                }
            }
            """);
    }

    [Fact]
    public async Task MixedSwitches_SuppressOnlyTheExhaustiveConditionalInvocation()
    {
        var (compilerDiagnostics, diagnostics) = await AnalyzeAsync("""
            #nullable enable
            using System;
            using Houtamelo.Spire;

            [EnforceExhaustiveness]
            enum HairRoute { Back, Front, Color }
            enum AdjustmentTarget { HairBack, HairFront, HairColor }

            class C
            {
                public Action<AdjustmentTarget>? on_adjustment_selected;

                public void Apply(HairRoute route)
                {
                    on_adjustment_selected?.Invoke(route switch
                    {
                        HairRoute.Back  => AdjustmentTarget.HairBack,
                        HairRoute.Front => AdjustmentTarget.HairFront,
                        HairRoute.Color => AdjustmentTarget.HairColor,
                    });

                    on_adjustment_selected?.Invoke(route switch
                    {
                        HairRoute.Back  => AdjustmentTarget.HairBack,
                        HairRoute.Front => AdjustmentTarget.HairFront,
                    });
                }
            }
            """);

        AssertNoCompilerErrors(compilerDiagnostics);
        var baseline = compilerDiagnostics
            .Where(IsExhaustivenessDiagnostic)
            .OrderBy(d => d.Location.SourceSpan.Start)
            .ToArray();
        Assert.Equal(2, baseline.Length);
        Assert.Equal("CS8524", baseline[0].Id);
        Assert.Equal("CS8509", baseline[1].Id);
        AssertDiagnosticState(diagnostics, baseline[0], suppressed: true);
        AssertDiagnosticState(diagnostics, baseline[1], suppressed: false);
        Assert.DoesNotContain(diagnostics, d => d.Id == "AD0001");
    }

    [Fact]
    public async Task NestedSwitch_ExhaustiveOuterSuppressesOnlyOuterWarning()
    {
        var (compilerDiagnostics, diagnostics) = await AnalyzeAsync("""
            #nullable enable
            using System;
            using Houtamelo.Spire;

            [EnforceExhaustiveness]
            enum HairRoute { Back, Front, Color }
            enum InnerRoute { Zero, One }
            enum AdjustmentTarget { HairBack, HairFront, HairColor }

            class C
            {
                public Action<AdjustmentTarget>? on_adjustment_selected;

                public void Apply(HairRoute route, InnerRoute inner)
                {
                    on_adjustment_selected?.Invoke(route switch
                    {
                        HairRoute.Back => inner switch
                        {
                            InnerRoute.Zero => AdjustmentTarget.HairBack,
                        },
                        HairRoute.Front => AdjustmentTarget.HairFront,
                        HairRoute.Color => AdjustmentTarget.HairColor,
                    });
                }
            }
            """);

        AssertNoCompilerErrors(compilerDiagnostics);
        var baseline = compilerDiagnostics
            .Where(IsExhaustivenessDiagnostic)
            .OrderBy(d => d.Location.SourceSpan.Start)
            .ToArray();
        Assert.Equal(2, baseline.Length);
        Assert.Equal("CS8524", baseline[0].Id);
        Assert.Equal("CS8509", baseline[1].Id);
        AssertDiagnosticState(diagnostics, baseline[0], suppressed: true);
        AssertDiagnosticState(diagnostics, baseline[1], suppressed: false);
        Assert.DoesNotContain(diagnostics, d => d.Id == "AD0001");
    }

    [Fact]
    public async Task NestedSwitch_IncompleteOuterSuppressesOnlyExhaustiveInnerWarning()
    {
        var (compilerDiagnostics, diagnostics) = await AnalyzeAsync("""
            #nullable enable
            using System;
            using Houtamelo.Spire;

            [EnforceExhaustiveness]
            enum HairRoute { Back, Front, Color }
            enum InnerRoute { Zero, One }
            enum AdjustmentTarget { HairBack, HairFront, HairColor }

            class C
            {
                public Action<AdjustmentTarget>? on_adjustment_selected;

                public void Apply(HairRoute route, InnerRoute inner)
                {
                    on_adjustment_selected?.Invoke(route switch
                    {
                        HairRoute.Back => inner switch
                        {
                            InnerRoute.Zero => AdjustmentTarget.HairBack,
                            InnerRoute.One  => AdjustmentTarget.HairFront,
                        },
                        HairRoute.Front => AdjustmentTarget.HairFront,
                    });
                }
            }
            """);

        AssertNoCompilerErrors(compilerDiagnostics);
        var baseline = compilerDiagnostics
            .Where(IsExhaustivenessDiagnostic)
            .OrderBy(d => d.Location.SourceSpan.Start)
            .ToArray();
        Assert.Equal(2, baseline.Length);
        Assert.Equal("CS8509", baseline[0].Id);
        Assert.Equal("CS8524", baseline[1].Id);
        AssertDiagnosticState(diagnostics, baseline[0], suppressed: false);
        AssertDiagnosticState(diagnostics, baseline[1], suppressed: true);
        Assert.DoesNotContain(diagnostics, d => d.Id == "AD0001");
    }

    [Fact]
    public async Task ConditionalInvocation_NonEnumSubject_LeavesIndeterminateWarningVisible()
    {
        await AssertUnsuppressedAsync("""
            #nullable enable
            using System;

            class C
            {
                public Action<int>? on_adjustment_selected;

                public void Apply(string route)
                {
                    on_adjustment_selected?.Invoke(route switch
                    {
                        "back"  => 1,
                        "front" => 2,
                        "color" => 3,
                    });
                }
            }
            """);
    }

    private static async Task AssertSuppressedAsync(string source)
    {
        var (compilerDiagnostics, diagnostics) = await AnalyzeAsync(source);
        AssertNoCompilerErrors(compilerDiagnostics);
        Assert.Contains(compilerDiagnostics, IsExhaustivenessDiagnostic);
        Assert.DoesNotContain(diagnostics, d => d.Id == "AD0001");

        var exhaustivenessDiagnostics = diagnostics
            .Where(IsExhaustivenessDiagnostic)
            .ToArray();
        Assert.NotEmpty(exhaustivenessDiagnostics);
        Assert.Contains(exhaustivenessDiagnostics, d => d.IsSuppressed);
        Assert.DoesNotContain(exhaustivenessDiagnostics, d => !d.IsSuppressed);
    }

    private static async Task AssertUnsuppressedAsync(string source)
    {
        var (compilerDiagnostics, diagnostics) = await AnalyzeAsync(source);
        AssertNoCompilerErrors(compilerDiagnostics);
        Assert.Contains(compilerDiagnostics, IsExhaustivenessDiagnostic);
        Assert.DoesNotContain(diagnostics, d => d.Id == "AD0001");
        Assert.Contains(diagnostics, d => IsExhaustivenessDiagnostic(d) && !d.IsSuppressed);
    }

    private static void AssertDiagnosticState(
        ImmutableArray<Diagnostic> diagnostics,
        Diagnostic baseline,
        bool suppressed)
    {
        var matches = diagnostics
            .Where(d => d.Id == baseline.Id
                && d.Location.SourceSpan.Equals(baseline.Location.SourceSpan))
            .ToArray();
        Assert.NotEmpty(matches);
        Assert.All(matches, diagnostic => Assert.Equal(suppressed, diagnostic.IsSuppressed));
    }

    private static async Task<(
        ImmutableArray<Diagnostic> CompilerDiagnostics,
        ImmutableArray<Diagnostic> Diagnostics)> AnalyzeAsync(string source)
    {
#if SPIRE_UNITY_TEST_HOST
        var references = UnityTestHost.FrameworkReferences;
#else
        var references = await ReferenceAssemblies.Net.Net80.ResolveAsync(
            LanguageNames.CSharp, CancellationToken.None);
#endif
        references = references
            .Add(CoreAssemblyReference)
            .Add(AnalyzerAssemblyReference);

        var tree = CSharpSyntaxTree.ParseText(
            source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview),
            path: "conditional-invocation.cs");
        var compilation = CSharpCompilation.Create(
            "ConditionalInvocationSuppressorTests",
            new[] { tree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var compilerDiagnostics = compilation.GetDiagnostics();
        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new CS8509Suppressor()),
            new CompilationWithAnalyzersOptions(
                new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty),
                onAnalyzerException: null,
                concurrentAnalysis: true,
                logAnalyzerExecutionTime: false,
                reportSuppressedDiagnostics: true));

        return (compilerDiagnostics, await withAnalyzers.GetAllDiagnosticsAsync());
    }

    private static bool IsExhaustivenessDiagnostic(Diagnostic diagnostic)
        => diagnostic.Id is "CS8509" or "CS8524" or "CS8655";

    private static void AssertNoCompilerErrors(ImmutableArray<Diagnostic> diagnostics)
    {
        var errors = diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.True(
            errors.Length == 0,
            "Test source must compile without errors:\n" +
            string.Join("\n", errors.Select(d => $"{d.Id}: {d.GetMessage()}")));
    }
}
