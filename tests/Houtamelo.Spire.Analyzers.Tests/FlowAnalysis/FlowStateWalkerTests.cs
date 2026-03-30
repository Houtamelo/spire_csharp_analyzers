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
using Houtamelo.Spire.Analyzers.Utils;
using Houtamelo.Spire.Analyzers.Utils.FlowAnalysis;
using Houtamelo.Spire;
using Xunit;

namespace Houtamelo.Spire.Analyzers.Tests.FlowAnalysis;

public class FlowStateWalkerTests
{
    // ── InitState tracking ──────────────────────────────────────────

    [Fact]
    public async Task DefaultThenAllFieldsAssigned_InitStateIsInitialized()
    {
        var ctx = await Analyze(@"
            [Houtamelo.Spire.EnforceInitialization]
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
            [Houtamelo.Spire.EnforceInitialization]
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
            [Houtamelo.Spire.EnforceInitialization]
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
            [Houtamelo.Spire.EnforceInitialization]
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
            [Houtamelo.Spire.EnforceInitialization]
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
            [Houtamelo.Spire.EnforceInitialization]
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
            [Houtamelo.Spire.EnforceInitialization]
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
    public async Task NewParameterlessOnEnforceInitialization_InitStateIsDefault()
    {
        var ctx = await Analyze(@"
            [Houtamelo.Spire.EnforceInitialization]
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
            [Houtamelo.Spire.EnforceInitialization]
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
            [Houtamelo.Spire.EnforceInitialization]
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
    public async Task MethodReturningEnforceInitializationType_AssumedInitialized()
    {
        var ctx = await Analyze(@"
            [Houtamelo.Spire.EnforceInitialization]
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

    // ── Complex scenario ──────────────────────────────────────────────

    [Fact]
    public async Task ComplexScenario_NestedBranches_FieldByField_CtorReassign_MethodCall()
    {
        // 3-field struct, nested if/else, switch with ctor reassign + method call + partial init.
        // Tests: field-level tracking through branches, merge at join points,
        //        cross-method return assumption, partial init detection after switch.
        //
        //  default(Pkt)                    → Id=Def, Seq=Def, Pay=Def
        //  pkt.Id = 1                      → Id=Init, Seq=Def, Pay=Def
        //  ┌─ if (urgent)
        //  │   pkt.Seq = NextSeq()         → Id=Init, Seq=Init, Pay=Def
        //  │   ┌─ if (hasPayload)
        //  │   │   pkt.Payload = 3.14      → Id=Init, Seq=Init, Pay=Init ← FULLY INIT
        //  │   │   Send(pkt)               state: Initialized
        //  │   └─ else
        //  │       pkt.Payload = 0.0       → Id=Init, Seq=Init, Pay=Init ← FULLY INIT
        //  │       Send(pkt)               (same call name — only first found, both are Init)
        //  └─ else
        //      switch (mode)
        //        case 0: pkt = new Pkt(…)  → all Init
        //        case 1: pkt = Create()    → all Init (cross-method)
        //        default: pkt.Seq = 99     → Id=Init, Seq=Init, Pay=Def
        //      Log(pkt)                    state: MaybeDefault (default branch didn't init Payload)
        //  Finalize(pkt)                   state: MaybeDefault (merge of urgent=all-init + else=maybe)

        var ctx = await Analyze(@"
            [Houtamelo.Spire.EnforceInitialization]
            struct Pkt
            {
                public int Id;
                public int Seq;
                public double Payload;
                public Pkt(int id, int seq, double payload) { Id = id; Seq = seq; Payload = payload; }
            }
            class C
            {
                static int NextSeq() => 42;
                static Pkt Create() => new Pkt(1, 2, 3.0);
                static void Send(Pkt p) { }
                static void Log(Pkt p) { }
                static void AfterInnerMerge(Pkt p) { }
                static void Finalize(Pkt p) { }

                void M(bool urgent, bool hasPayload, int mode)
                {
                    var pkt = default(Pkt);
                    pkt.Id = 1;

                    if (urgent)
                    {
                        pkt.Seq = NextSeq();
                        if (hasPayload)
                        {
                            pkt.Payload = 3.14;
                            Send(pkt);
                        }
                        else
                        {
                            pkt.Payload = 0.0;
                            Log(pkt);
                        }
                        AfterInnerMerge(pkt);
                    }
                    else
                    {
                        switch (mode)
                        {
                            case 0:
                                pkt = new Pkt(10, 20, 30.0);
                                break;
                            case 1:
                                pkt = Create();
                                break;
                            default:
                                pkt.Seq = 99;
                                break;
                        }
                    }

                    Finalize(pkt);
                }
            }
        ", "M", "pkt");

        // Send(pkt) — inside if(urgent)/if(hasPayload): all 3 fields initialized
        var sendState = ctx.StateAtInvocation("Send");
        Assert.NotNull(sendState);
        Assert.Equal(InitState.Initialized, sendState!.Value.InitState);
        Assert.Equal(3, sendState.Value.FieldStates.Length);
        Assert.All(sendState.Value.FieldStates, f => Assert.Equal(InitState.Initialized, f));

        // AfterInnerMerge(pkt) — after if(hasPayload)/else merge:
        // both paths set Payload, so merge should be Initialized
        var afterMerge = ctx.StateAtInvocation("AfterInnerMerge");
        Assert.NotNull(afterMerge);
        Assert.Equal(InitState.Initialized, afterMerge!.Value.InitState);
        Assert.All(afterMerge.Value.FieldStates, f => Assert.Equal(InitState.Initialized, f));

        // Finalize(pkt) — merge of all paths:
        //   urgent+hasPayload:    Initialized
        //   urgent+!hasPayload:   Initialized
        //   !urgent+case0:        Initialized (ctor)
        //   !urgent+case1:        Initialized (Create())
        //   !urgent+default:      Id=Init, Seq=Init, Payload=Def → MaybeDefault
        // Merge → MaybeDefault (at least one path has Payload=Default)
        var finalState = ctx.StateAtInvocation("Finalize");
        Assert.NotNull(finalState);
        Assert.Equal(InitState.MaybeDefault, finalState!.Value.InitState);

        // Field-level: Id and Seq should be Initialized on all paths
        Assert.Equal(InitState.Initialized, finalState.Value.FieldStates[0]); // Id
        Assert.Equal(InitState.Initialized, finalState.Value.FieldStates[1]); // Seq
        // Payload: MaybeDefault (default switch branch didn't init it)
        Assert.Equal(InitState.MaybeDefault, finalState.Value.FieldStates[2]); // Payload
    }

    // ── Helper infrastructure ───────────────────────────────────────

    private static async Task<AnalysisContext> Analyze(string source, string methodName, string localName)
    {
        var refs = await ReferenceAssemblies.Net.Net80.ResolveAsync(
            LanguageNames.CSharp, CancellationToken.None);
        var coreRef = MetadataReference.CreateFromFile(typeof(EnforceInitializationAttribute).Assembly.Location);

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

        var enforceInitializationType = compilation.GetTypeByMetadataName("Houtamelo.Spire.EnforceInitializationAttribute")!;

        var initTypes = new List<INamedTypeSymbol>();
        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var sm = compilation.GetSemanticModel(syntaxTree);
            foreach (var typeDecl in syntaxTree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (sm.GetDeclaredSymbol(typeDecl) is INamedTypeSymbol ts
                    && EnforceInitializationChecks.HasEnforceInitializationAttribute(ts, enforceInitializationType))
                    initTypes.Add(ts);
            }
        }

        var fieldMap = TrackedSymbolSet.BuildFieldMap(initTypes);
        var symbols = new TrackedSymbolSet(enforceInitializationType, fieldMap);
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
