using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Testing;
using Spire.Analyzers.Utils;
using Spire.Analyzers.Utils.FlowAnalysis;
using Xunit;

namespace Spire.Analyzers.Tests.FlowAnalysis;

public class FlowStateWalkerTests
{
    // ── InitState tracking ──────────────────────────────────────────

    [Fact]
    public async Task DefaultThenAllFieldsAssigned_InitStateIsInitialized()
    {
        var ctx = await Analyze(@"
            [Spire.MustBeInit]
            struct S { public int X; public int Y; public S(int x, int y) { X = x; Y = y; } }
            class C
            {
                static void Use(S s) { }
                void M()
                {
                    var s = default(S);
                    s.X = 1;
                    s.Y = 2;
                    Use(s);
                }
            }
        ", "M", "s");

        var state = ctx.StateAtInvocation("Use");
        Assert.NotNull(state);
        Assert.Equal(InitState.Initialized, state!.Value.InitState);
    }

    [Fact]
    public async Task DefaultThenPartialFieldAssigned_InitStateIsMaybeDefault()
    {
        var ctx = await Analyze(@"
            [Spire.MustBeInit]
            struct S { public int X; public int Y; public S(int x, int y) { X = x; Y = y; } }
            class C
            {
                static void Use(S s) { }
                void M()
                {
                    var s = default(S);
                    s.X = 1;
                    Use(s);
                }
            }
        ", "M", "s");

        var state = ctx.StateAtInvocation("Use");
        Assert.NotNull(state);
        Assert.Equal(InitState.MaybeDefault, state!.Value.InitState);
    }

    [Fact]
    public async Task DefaultThenCtorReassign_InitStateIsInitialized()
    {
        var ctx = await Analyze(@"
            [Spire.MustBeInit]
            struct S { public int X; public S(int x) { X = x; } }
            class C
            {
                static void Use(S s) { }
                void M()
                {
                    var s = default(S);
                    s = new S(42);
                    Use(s);
                }
            }
        ", "M", "s");

        var state = ctx.StateAtInvocation("Use");
        Assert.NotNull(state);
        Assert.Equal(InitState.Initialized, state!.Value.InitState);
    }

    [Fact]
    public async Task DefaultNoReassign_InitStateIsDefault()
    {
        var ctx = await Analyze(@"
            [Spire.MustBeInit]
            struct S { public int X; public S(int x) { X = x; } }
            class C
            {
                static void Use(S s) { }
                void M()
                {
                    var s = default(S);
                    Use(s);
                }
            }
        ", "M", "s");

        var state = ctx.StateAtInvocation("Use");
        Assert.NotNull(state);
        Assert.Equal(InitState.Default, state!.Value.InitState);
    }

    [Fact]
    public async Task BranchedInit_OnePathDefault_InitStateIsMaybeDefault()
    {
        var ctx = await Analyze(@"
            [Spire.MustBeInit]
            struct S { public int X; public S(int x) { X = x; } }
            class C
            {
                static void Use(S s) { }
                void M(bool cond)
                {
                    S s;
                    if (cond)
                        s = new S(1);
                    else
                        s = default;
                    Use(s);
                }
            }
        ", "M", "s");

        var state = ctx.StateAtInvocation("Use");
        Assert.NotNull(state);
        Assert.Equal(InitState.MaybeDefault, state!.Value.InitState);
    }

    [Fact]
    public async Task BranchedInit_BothPathsInitialized_InitStateIsInitialized()
    {
        var ctx = await Analyze(@"
            [Spire.MustBeInit]
            struct S { public int X; public S(int x) { X = x; } }
            class C
            {
                static void Use(S s) { }
                void M(bool cond)
                {
                    S s;
                    if (cond)
                        s = new S(1);
                    else
                        s = new S(2);
                    Use(s);
                }
            }
        ", "M", "s");

        var state = ctx.StateAtInvocation("Use");
        Assert.NotNull(state);
        Assert.Equal(InitState.Initialized, state!.Value.InitState);
    }

    [Fact]
    public async Task ParameterOfTrackedType_StartsInitialized()
    {
        var ctx = await Analyze(@"
            [Spire.MustBeInit]
            struct S { public int X; public S(int x) { X = x; } }
            class C
            {
                static void Use(S s) { }
                void M(S s)
                {
                    Use(s);
                }
            }
        ", "M", "s");

        var state = ctx.StateAtInvocation("Use");
        Assert.NotNull(state);
        Assert.Equal(InitState.Initialized, state!.Value.InitState);
    }

    [Fact]
    public async Task NewParameterlessOnMustBeInit_InitStateIsDefault()
    {
        var ctx = await Analyze(@"
            [Spire.MustBeInit]
            struct S { public int X; public S(int x) { X = x; } }
            class C
            {
                static void Use(S s) { }
                void M()
                {
                    var s = new S();
                    Use(s);
                }
            }
        ", "M", "s");

        var state = ctx.StateAtInvocation("Use");
        Assert.NotNull(state);
        Assert.Equal(InitState.Default, state!.Value.InitState);
    }

    // ── Field-level tracking ────────────────────────────────────────

    [Fact]
    public async Task FieldStates_TrackIndividualFields()
    {
        var ctx = await Analyze(@"
            [Spire.MustBeInit]
            struct S { public int X; public int Y; public S(int x, int y) { X = x; Y = y; } }
            class C
            {
                static void Use(S s) { }
                void M()
                {
                    var s = default(S);
                    s.X = 1;
                    Use(s);
                }
            }
        ", "M", "s");

        var state = ctx.StateAtInvocation("Use");
        Assert.NotNull(state);
        Assert.Equal(2, state!.Value.FieldStates.Length);
        Assert.Equal(InitState.Initialized, state.Value.FieldStates[0]); // X
        Assert.Equal(InitState.Default, state.Value.FieldStates[1]); // Y
    }

    [Fact]
    public async Task FieldStates_CtorSetsAllFields()
    {
        var ctx = await Analyze(@"
            [Spire.MustBeInit]
            struct S { public int X; public int Y; public S(int x, int y) { X = x; Y = y; } }
            class C
            {
                static void Use(S s) { }
                void M()
                {
                    var s = new S(1, 2);
                    Use(s);
                }
            }
        ", "M", "s");

        var state = ctx.StateAtInvocation("Use");
        Assert.NotNull(state);
        Assert.All(state!.Value.FieldStates, f => Assert.Equal(InitState.Initialized, f));
    }

    // ── Cross-method reasoning ──────────────────────────────────────

    [Fact]
    public async Task MethodReturningMustBeInitType_AssumedInitialized()
    {
        var ctx = await Analyze(@"
            [Spire.MustBeInit]
            struct S { public int X; public S(int x) { X = x; } }
            class C
            {
                static S Create() => new S(1);
                static void Use(S s) { }
                void M()
                {
                    var s = Create();
                    Use(s);
                }
            }
        ", "M", "s");

        var state = ctx.StateAtInvocation("Use");
        Assert.NotNull(state);
        Assert.Equal(InitState.Initialized, state!.Value.InitState);
    }

    // ── Helper infrastructure ───────────────────────────────────────

    private static async Task<AnalysisContext> Analyze(string source, string methodName, string localName)
    {
        var refs = await ReferenceAssemblies.Net.Net80.ResolveAsync(
            LanguageNames.CSharp, CancellationToken.None);
        var coreRef = MetadataReference.CreateFromFile(typeof(Spire.MustBeInitAttribute).Assembly.Location);

        var tree = CSharpSyntaxTree.ParseText(source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest));
        var compilation = CSharpCompilation.Create("Test", new[] { tree },
            refs.Add(coreRef),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.Empty(errors);

        var model = compilation.GetSemanticModel(tree);
        var methodDecl = tree.GetRoot().DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.Text == methodName);

        var cfg = ControlFlowGraph.Create(methodDecl, model)!;

        var mustBeInitType = compilation.GetTypeByMetadataName("Spire.MustBeInitAttribute")!;

        var initTypes = new List<INamedTypeSymbol>();
        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var sm = compilation.GetSemanticModel(syntaxTree);
            foreach (var typeDecl in syntaxTree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (sm.GetDeclaredSymbol(typeDecl) is INamedTypeSymbol ts
                    && MustBeInitChecks.HasMustBeInitAttribute(ts, mustBeInitType))
                    initTypes.Add(ts);
            }
        }

        var fieldMap = TrackedSymbolSet.BuildFieldMap(initTypes);
        var symbols = new TrackedSymbolSet(mustBeInitType, fieldMap);
        var methodSymbol = model.GetDeclaredSymbol(methodDecl)!;

        var result = FlowStateWalker.Analyze(cfg, symbols, methodSymbol);

        // Resolve the local/parameter symbol
        ISymbol? targetSymbol = null;
        // Check parameters first
        if (methodSymbol is IMethodSymbol ms)
            targetSymbol = ms.Parameters.FirstOrDefault(p => p.Name == localName);
        // Then check locals in CFG regions
        targetSymbol ??= FindLocal(cfg.Root, localName);

        Assert.NotNull(targetSymbol);

        return new AnalysisContext(result, cfg, targetSymbol!);
    }

    private static ILocalSymbol? FindLocal(ControlFlowRegion region, string name)
    {
        foreach (var local in region.Locals)
        {
            if (local.Name == name)
                return local;
        }

        foreach (var nested in region.NestedRegions)
        {
            var found = FindLocal(nested, name);
            if (found is not null)
                return found;
        }

        return null;
    }

    private sealed class AnalysisContext
    {
        public FlowAnalysisResult Result { get; }
        public ControlFlowGraph Cfg { get; }
        public ISymbol Variable { get; }

        public AnalysisContext(FlowAnalysisResult result, ControlFlowGraph cfg, ISymbol variable)
        {
            Result = result;
            Cfg = cfg;
            Variable = variable;
        }

        /// Finds an IInvocationOperation by method name in the CFG and returns the
        /// variable state at that program point (state BEFORE the invocation).
        public VariableState? StateAtInvocation(string methodName)
        {
            foreach (var block in Cfg.Blocks)
            {
                if (block.Kind != BasicBlockKind.Block) continue;
                foreach (var op in block.Operations)
                {
                    var invocation = FindInvocationRecursive(op, methodName);
                    if (invocation is not null)
                        return Result.GetStateAt(invocation, Variable);
                }
            }

            return null;
        }

        private static IInvocationOperation? FindInvocationRecursive(IOperation op, string name)
        {
            if (op is IInvocationOperation inv && inv.TargetMethod.Name == name)
                return inv;

            foreach (var child in op.ChildOperations)
            {
                var found = FindInvocationRecursive(child, name);
                if (found is not null) return found;
            }

            return null;
        }
    }
}
