// src/Houtamelo.Spire.Analyzers.Utils/FlowAnalysis/FlowAnalysisCache.cs
using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;

namespace Houtamelo.Spire.Analyzers.Utils.FlowAnalysis;

/// Thread-safe, per-compilation cache for flow analysis results.
/// Created once in CompilationStartAction, shared across all rules.
public sealed class FlowAnalysisCache
{
    private readonly ConcurrentDictionary<ISymbol, FlowAnalysisResult> _cache =
        new(SymbolEqualityComparer.Default);

    public TrackedSymbolSet Symbols { get; }

    public FlowAnalysisCache(TrackedSymbolSet symbols)
    {
        Symbols = symbols;
    }

    /// Returns cached result or computes and caches a new one.
    public FlowAnalysisResult GetOrCompute(ISymbol containingMember, ControlFlowGraph cfg)
    {
        return _cache.GetOrAdd(containingMember, _ => FlowStateWalker.Analyze(cfg, Symbols, containingMember));
    }
}
