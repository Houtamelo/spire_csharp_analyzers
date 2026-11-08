using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using Houtamelo.Spire.Analyzers.SourceGenerators.Model;
using Houtamelo.Spire.PatternAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers.GuardFlow;

/// Performs receiver-aware discriminator analysis for one executable body.
internal static class GuardFlowAnalyzer
{
    public static void Analyze(
        OperationBlockAnalysisContext context,
        INamedTypeSymbol discriminatedUnionAttribute,
        ConcurrentDictionary<ReportKey, byte>? reported = null)
    {
        var accessCache = new Dictionary<INamedTypeSymbol, AccessModel>(
            SymbolEqualityComparer.Default);
        reported ??= new ConcurrentDictionary<ReportKey, byte>();
        foreach (var operationBlock in context.OperationBlocks)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (!ContainsRelevantAccessor(
                    operationBlock,
                    discriminatedUnionAttribute,
                    accessCache,
                    context.CancellationToken))
                continue;
            var cfg = context.GetControlFlowGraph(operationBlock);
            if (cfg is null)
                continue;

            new BodyAnalysis(
                context,
                discriminatedUnionAttribute,
                cfg,
                operationBlock,
                accessCache,
                reported).Run();
        }
    }

    private static bool ContainsRelevantAccessor(
        ControlFlowGraph cfg,
        INamedTypeSymbol discriminatedUnionAttribute,
        Dictionary<INamedTypeSymbol, AccessModel> accessCache,
        System.Threading.CancellationToken cancellationToken)
    {
        foreach (var block in cfg.Blocks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var operation in block.Operations)
            {
                if (ContainsRelevantAccessor(
                        operation,
                        discriminatedUnionAttribute,
                        accessCache,
                        cancellationToken))
                    return true;
            }
            if (block.BranchValue is not null &&
                ContainsRelevantAccessor(
                    block.BranchValue,
                    discriminatedUnionAttribute,
                    accessCache,
                    cancellationToken))
            {
                return true;
            }
        }
        return false;
    }

    private static bool ContainsRelevantAccessor(
        IOperation operation,
        INamedTypeSymbol discriminatedUnionAttribute,
        Dictionary<INamedTypeSymbol, AccessModel> accessCache,
        System.Threading.CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (TryGetAccessInfo(operation, discriminatedUnionAttribute, out _, accessCache))
            return true;
        foreach (var child in operation.ChildOperations)
        {
            if (ContainsRelevantAccessor(
                    child,
                    discriminatedUnionAttribute,
                    accessCache,
                    cancellationToken))
                return true;
        }
        return false;
    }

    private static bool TryGetAccessInfo(
        IOperation operation,
        INamedTypeSymbol discriminatedUnionAttribute,
        out AccessInfo access,
        Dictionary<INamedTypeSymbol, AccessModel>? accessCache = null)
    {
        access = default;
        ISymbol? member;
        IOperation? instance;
        switch (operation)
        {
            case IPropertyReferenceOperation property:
                member = property.Property;
                instance = property.Instance;
                break;
            case IFieldReferenceOperation field:
                member = field.Field;
                instance = field.Instance;
                break;
            default:
                return false;
        }

        var containingType = member.ContainingType;
        if (containingType is null || !containingType.IsValueType ||
            !HasEditorBrowsableNever(member))
        {
            return false;
        }

        AccessModel? model = null;
        if (accessCache is not null)
            accessCache.TryGetValue(containingType, out model);
        if (model is null)
        {
            var info = UnionTypeInfo.TryCreate(containingType, discriminatedUnionAttribute);
            var map = info is null ? null : VariantFieldMap.TryCreate(containingType, info);
            if (info is null || map is null)
                return false;
            model = new AccessModel(info, map);
            if (accessCache is not null)
                accessCache[containingType] = model;
        }

        if (model is null)
            return false;

        var owningVariants = model.Map.GetOwningVariants(member.Name);
        if (owningVariants.IsEmpty)
            return false;

        access = new AccessInfo(member, instance, owningVariants, model.Info, operation);
        return true;
    }

    private static bool HasEditorBrowsableNever(ISymbol symbol)
    {
        return symbol.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.Name == "EditorBrowsableAttribute" &&
            attribute.ConstructorArguments.Length > 0 &&
            attribute.ConstructorArguments[0].Value is int value &&
            value == 1);
    }

    private sealed class AccessModel
    {
        public UnionTypeInfo Info { get; }
        public VariantFieldMap Map { get; }

        public AccessModel(UnionTypeInfo info, VariantFieldMap map)
        {
            Info = info;
            Map = map;
        }
    }

    private readonly struct AccessInfo
    {
        public ISymbol Member { get; }
        public IOperation? Instance { get; }
        public ImmutableHashSet<string> OwningVariants { get; }
        public UnionTypeInfo Info { get; }
        public IOperation Operation { get; }

        public AccessInfo(
            ISymbol member,
            IOperation? instance,
            ImmutableHashSet<string> owningVariants,
            UnionTypeInfo info,
            IOperation operation)
        {
            Member = member;
            Instance = instance;
            OwningVariants = owningVariants;
            Info = info;
            Operation = operation;
        }
    }

    internal readonly struct ReportKey : IEquatable<ReportKey>
    {
        private readonly string _id;
        private readonly SyntaxTree? _tree;
        private readonly int _start;
        private readonly int _length;

        public ReportKey(string id, SyntaxNode syntax)
        {
            _id = id;
            _tree = syntax.SyntaxTree;
            _start = syntax.SpanStart;
            _length = syntax.Span.Length;
        }

        public bool Equals(ReportKey other)
            => string.Equals(_id, other._id, StringComparison.Ordinal) &&
               ReferenceEquals(_tree, other._tree) &&
               _start == other._start &&
               _length == other._length;

        public override bool Equals(object? obj)
            => obj is ReportKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = StringComparer.Ordinal.GetHashCode(_id);
                hash = (hash * 397) ^ (_tree is null ? 0 : RuntimeHelpers.GetHashCode(_tree));
                hash = (hash * 397) ^ _start;
                return (hash * 397) ^ _length;
            }
        }
    }

    private sealed class BodyAnalysis
    {
        private readonly OperationBlockAnalysisContext _context;
        private readonly INamedTypeSymbol _duAttribute;
        private readonly ControlFlowGraph _cfg;
        private readonly IOperation _operationBlock;
        private readonly Dictionary<INamedTypeSymbol, AccessModel> _accessCache;
        private readonly GuardFlowState?[] _inputs;
        private readonly GuardFlowState?[] _outputs;
        private readonly Dictionary<int, ImmutableArray<int>> _handlerSeeds;
        private readonly ConcurrentDictionary<ReportKey, byte> _reported;
        private readonly Dictionary<IMethodSymbol, EffectSummary> _localEffectSummaries =
            new(SymbolEqualityComparer.Default);
        private bool _localEffectsBuilt;

        private sealed class EffectAccumulator
        {
            public HashSet<ReceiverPath> ImmediateWrites { get; } = new();
            public HashSet<ReceiverPath> EscapedCallbackWrites { get; } = new();
            public HashSet<IMethodSymbol> Callees { get; } = new(SymbolEqualityComparer.Default);
            public Dictionary<ISymbol, EffectSummary> DelegateSummaries { get; } =
                new(SymbolEqualityComparer.Default);
            public Dictionary<ISymbol, AliasTargetSet> Aliases { get; } =
                new(SymbolEqualityComparer.Default);
            public bool MayInvokeEscapedCallbacks { get; set; }
            public bool MayWriteShared { get; set; }
            public bool MayWriteExposed { get; set; }

            public void Add(EffectSummary summary)
            {
                ImmediateWrites.UnionWith(summary.ImmediateWrites);
                EscapedCallbackWrites.UnionWith(summary.EscapedCallbackWrites);
                MayInvokeEscapedCallbacks |= summary.MayInvokeEscapedCallbacks;
                MayWriteShared |= summary.MayWriteShared;
                MayWriteExposed |= summary.MayWriteExposed;
            }

            public EffectSummary Build()
                => new(
                    ImmediateWrites,
                    EscapedCallbackWrites,
                    MayInvokeEscapedCallbacks,
                    MayWriteShared,
                    MayWriteExposed);
        }

        public BodyAnalysis(
            OperationBlockAnalysisContext context,
            INamedTypeSymbol duAttribute,
            ControlFlowGraph cfg,
            IOperation operationBlock,
            Dictionary<INamedTypeSymbol, AccessModel> accessCache,
            ConcurrentDictionary<ReportKey, byte> reported)
        {
            _context = context;
            _duAttribute = duAttribute;
            _cfg = cfg;
            _operationBlock = operationBlock;
            _accessCache = accessCache;
            _reported = reported;
            _inputs = new GuardFlowState?[cfg.Blocks.Length];
            _outputs = new GuardFlowState?[cfg.Blocks.Length];
            _handlerSeeds = BuildHandlerSeeds(cfg);
        }

        public void Run()
        {
            var entryOrdinal = _cfg.Blocks[0].Ordinal;
            _inputs[entryOrdinal] = GuardFlowState.Unknown();
            var work = new Queue<int>();
            var queued = new bool[_cfg.Blocks.Length];
            work.Enqueue(entryOrdinal);
            queued[entryOrdinal] = true;

            while (work.Count > 0)
            {
                _context.CancellationToken.ThrowIfCancellationRequested();
                var ordinal = work.Dequeue();
                queued[ordinal] = false;
                var input = _inputs[ordinal];
                if (input is null || !input.IsReachable)
                    continue;

                var state = input.Clone();
                EvaluateBlock(
                    _cfg.Blocks[ordinal],
                    state,
                    report: false,
                    exceptionalSeed: seed => PropagateExceptional(ordinal, seed, work, queued));
                if (!StatesEqual(_outputs[ordinal], state))
                    _outputs[ordinal] = state.Clone();
                Propagate(_cfg.Blocks[ordinal], state, work, queued);
                PropagateExceptional(ordinal, state, work, queued);
            }

            foreach (var block in _cfg.Blocks)
            {
                _context.CancellationToken.ThrowIfCancellationRequested();
                var input = _inputs[block.Ordinal];
                if (input is null || !input.IsReachable)
                    continue;
                EvaluateBlock(block, input.Clone(), report: true, exceptionalSeed: null);
            }


            AnalyzeDeclaredNestedFunctions();
        }

        private void AnalyzeDeclaredNestedFunctions()
        {
            var localFunctions = _operationBlock.DescendantsAndSelf()
                .OfType<ILocalFunctionOperation>()
                .Where(IsOwnedByCurrentBody)
                .ToArray();
            foreach (var local in localFunctions)
            {
                _context.CancellationToken.ThrowIfCancellationRequested();
                if (local.Body is null ||
                    !ContainsRelevantAccessor(
                        local.Body,
                        _duAttribute,
                        _accessCache,
                        _context.CancellationToken))
                    continue;

                var nested = _cfg.GetLocalFunctionControlFlowGraph(
                    local.Symbol,
                    _context.CancellationToken);
                new BodyAnalysis(
                    _context,
                    _duAttribute,
                    nested,
                    nested.OriginalOperation,
                    _accessCache,
                    _reported).Run();
            }

            var anonymousFunctions = _operationBlock.DescendantsAndSelf()
                .OfType<IAnonymousFunctionOperation>()
                .Where(IsOwnedByCurrentBody)
                .ToArray();
            foreach (var anonymous in anonymousFunctions)
            {
                _context.CancellationToken.ThrowIfCancellationRequested();
                var flowAnonymous = FindOwnedFlowAnonymousFunction(anonymous);
                if (flowAnonymous is null)
                    continue;
                var nested = _cfg.GetAnonymousFunctionControlFlowGraph(
                    flowAnonymous,
                    _context.CancellationToken);
                if (!ContainsRelevantAccessor(
                        nested,
                        _duAttribute,
                        _accessCache,
                        _context.CancellationToken))
                    continue;
                new BodyAnalysis(
                    _context,
                    _duAttribute,
                    nested,
                    nested.OriginalOperation,
                    _accessCache,
                    _reported).Run();
            }
        }

        private bool IsOwnedByCurrentBody(IOperation operation)
        {
            var current = operation.Parent;
            while (current is not null && !ReferenceEquals(current, _operationBlock))
            {
                if (current is ILocalFunctionOperation or
                    IAnonymousFunctionOperation or
                    IFlowAnonymousFunctionOperation)
                {
                    return false;
                }
                current = current.Parent;
            }
            return ReferenceEquals(current, _operationBlock);
        }

        private IFlowAnonymousFunctionOperation? FindOwnedFlowAnonymousFunction(
            IAnonymousFunctionOperation anonymous)
            => _cfg.Blocks
                .SelectMany(block => block.Operations.Concat(
                    block.BranchValue is null
                        ? Enumerable.Empty<IOperation>()
                        : new[] { block.BranchValue }))
                .SelectMany(operation => operation.DescendantsAndSelf())
                .OfType<IFlowAnonymousFunctionOperation>()
                .FirstOrDefault(candidate => candidate.Syntax.Span == anonymous.Syntax.Span);

        private void EvaluateBlock(
            BasicBlock block,
            GuardFlowState state,
            bool report,
            Action<GuardFlowState>? exceptionalSeed)
        {
            foreach (var operation in block.Operations)
            {
                _context.CancellationToken.ThrowIfCancellationRequested();
                exceptionalSeed?.Invoke(state.Clone());
                EvaluateOperation(operation, state, report, pattern: null);
                exceptionalSeed?.Invoke(state.Clone());
            }
            if (block.BranchValue is not null)
            {
                _context.CancellationToken.ThrowIfCancellationRequested();
                exceptionalSeed?.Invoke(state.Clone());
                EvaluateOperation(block.BranchValue, state, report, pattern: null);
                exceptionalSeed?.Invoke(state.Clone());
            }
        }

        private void Propagate(
            BasicBlock block,
            GuardFlowState state,
            Queue<int> work,
            bool[] queued)
        {
            if (block.BranchValue is not null)
            {
                var facts = TryGetConditionFacts(block.BranchValue, state);
                if (block.ConditionalSuccessor?.Destination is { } conditional)
                {
                    var conditionalState = ApplyBranch(
                        state,
                        facts,
                        block.ConditionKind == ControlFlowConditionKind.WhenTrue);
                    PropagateEdge(
                        block.ConditionalSuccessor,
                        conditionalState,
                        conditional,
                        work,
                        queued);
                }
                if (block.FallThroughSuccessor?.Destination is { } fallThrough)
                {
                    var fallThroughState = ApplyBranch(
                        state,
                        facts,
                        block.ConditionKind != ControlFlowConditionKind.WhenTrue);
                    PropagateEdge(
                        block.FallThroughSuccessor,
                        fallThroughState,
                        fallThrough,
                        work,
                        queued);
                }
                else if (block.EnclosingRegion.Kind == ControlFlowRegionKind.Filter)
                {
                    var failedFilterState = ApplyBranch(
                        state,
                        facts,
                        block.ConditionKind != ControlFlowConditionKind.WhenTrue);
                    PropagateFilterFailure(block, failedFilterState, work, queued);
                }
                return;
            }

            if (block.ConditionalSuccessor?.Destination is { } conditionalDestination)
                PropagateEdge(block.ConditionalSuccessor, state.Clone(), conditionalDestination, work, queued);
            if (block.FallThroughSuccessor?.Destination is { } fallThroughDestination)
                PropagateEdge(block.FallThroughSuccessor, state.Clone(), fallThroughDestination, work, queued);
        }

        private void PropagateEdge(
            ControlFlowBranch edge,
            GuardFlowState state,
            BasicBlock destination,
            Queue<int> work,
            bool[] queued)
        {
            if (!state.IsReachable)
                return;
            if (!edge.FinallyRegions.IsDefaultOrEmpty)
                ApplyFinallyEffects(state, edge.FinallyRegions);
            MergeInput(destination.Ordinal, state, work, queued);
        }

        private void PropagateExceptional(
            int sourceOrdinal,
            GuardFlowState state,
            Queue<int> work,
            bool[] queued)
        {
            if (!_handlerSeeds.TryGetValue(sourceOrdinal, out var handlers))
                return;
            foreach (var handler in handlers)
                MergeInput(handler, state, work, queued);

            var block = _cfg.Blocks[sourceOrdinal];
            if (block.EnclosingRegion.Kind == ControlFlowRegionKind.Filter)
                PropagateFilterFailure(block, state, work, queued);
        }

        private void PropagateFilterFailure(
            BasicBlock filter,
            GuardFlowState state,
            Queue<int> work,
            bool[] queued)
        {
            var trueDestination = filter.ConditionalSuccessor?.Destination?.Ordinal ??
                filter.Ordinal;
            var nextHandler = _cfg.Blocks
                .Where(block =>
                    block.EnclosingRegion.Kind == ControlFlowRegionKind.Catch &&
                    block.Ordinal > trueDestination)
                .OrderBy(block => block.Ordinal)
                .FirstOrDefault();
            if (nextHandler is not null)
                MergeInput(nextHandler.Ordinal, state, work, queued);
        }

        private void MergeInput(
            int ordinal,
            GuardFlowState incoming,
            Queue<int> work,
            bool[] queued)
        {
            if (!incoming.IsReachable)
                return;
            var current = _inputs[ordinal];
            var merged = current is null
                ? incoming.Clone()
                : GuardFlowState.Merge(current, incoming);
            if (StatesEqual(current, merged))
                return;
            _inputs[ordinal] = merged;
            if (!queued[ordinal])
            {
                work.Enqueue(ordinal);
                queued[ordinal] = true;
            }
        }

        private void ApplyFinallyEffects(
            GuardFlowState state,
            ImmutableArray<ControlFlowRegion> regions)
        {
            foreach (var region in regions)
            {
                var result = TransferFinallyRegion(region, state);
                state.ReplaceWith(result);
            }
        }

        private GuardFlowState TransferFinallyRegion(
            ControlFlowRegion region,
            GuardFlowState initial)
        {
            var states = new Dictionary<int, GuardFlowState>();
            var work = new Queue<int>();
            var exits = new List<GuardFlowState>();
            states[region.FirstBlockOrdinal] = initial.Clone();
            work.Enqueue(region.FirstBlockOrdinal);

            while (work.Count > 0)
            {
                _context.CancellationToken.ThrowIfCancellationRequested();
                var ordinal = work.Dequeue();
                var state = states[ordinal].Clone();
                var block = _cfg.Blocks[ordinal];
                EvaluateBlock(block, state, report: false, exceptionalSeed: null);

                if (block.BranchValue is not null &&
                    block.ConditionalSuccessor?.Destination is { } conditional &&
                    block.FallThroughSuccessor?.Destination is { } fallThrough)
                {
                    var facts = TryGetConditionFacts(block.BranchValue, state);
                    MergeFinallySuccessor(
                        region,
                        conditional.Ordinal,
                        ApplyBranch(state, facts, block.ConditionKind == ControlFlowConditionKind.WhenTrue),
                        states,
                        work,
                        exits);
                    MergeFinallySuccessor(
                        region,
                        fallThrough.Ordinal,
                        ApplyBranch(state, facts, block.ConditionKind != ControlFlowConditionKind.WhenTrue),
                        states,
                        work,
                        exits);
                }
                else
                {
                    var successors = new[] { block.ConditionalSuccessor, block.FallThroughSuccessor }
                        .Where(successor => successor?.Destination is not null)
                        .Select(successor => successor!.Destination!.Ordinal)
                        .ToArray();
                    if (successors.Length == 0)
                    {
                        exits.Add(state);
                    }
                    else
                    {
                        foreach (var successor in successors)
                        {
                            MergeFinallySuccessor(
                                region,
                                successor,
                                state,
                                states,
                                work,
                                exits);
                        }
                    }
                }
            }

            if (exits.Count == 0)
                return initial.Clone();
            var result = exits[0];
            for (var i = 1; i < exits.Count; i++)
                result = GuardFlowState.Merge(result, exits[i]);
            return result;
        }

        private static void MergeFinallySuccessor(
            ControlFlowRegion region,
            int ordinal,
            GuardFlowState incoming,
            Dictionary<int, GuardFlowState> states,
            Queue<int> work,
            List<GuardFlowState> exits)
        {
            if (ordinal < region.FirstBlockOrdinal || ordinal > region.LastBlockOrdinal)
            {
                exits.Add(incoming);
                return;
            }

            if (!states.TryGetValue(ordinal, out var existing))
            {
                states[ordinal] = incoming.Clone();
                work.Enqueue(ordinal);
                return;
            }

            var merged = GuardFlowState.Merge(existing, incoming);
            if (!StatesEqual(existing, merged))
            {
                states[ordinal] = merged;
                work.Enqueue(ordinal);
            }
        }

        private static Dictionary<int, ImmutableArray<int>> BuildHandlerSeeds(ControlFlowGraph cfg)
        {
            var regions = cfg.Blocks.Select(block => block.EnclosingRegion).Distinct().ToArray();
            var tries = regions.Where(region => region.Kind == ControlFlowRegionKind.Try).ToArray();
            var handlers = regions.Where(region =>
                region.Kind is ControlFlowRegionKind.Catch or ControlFlowRegionKind.Finally or
                ControlFlowRegionKind.Filter).ToArray();
            var result = new Dictionary<int, ImmutableArray<int>.Builder>();
            foreach (var handler in handlers)
            {
                var associated = tries
                    .Where(candidate => candidate.LastBlockOrdinal < handler.FirstBlockOrdinal)
                    .OrderByDescending(candidate => candidate.LastBlockOrdinal)
                    .FirstOrDefault();
                if (associated is null)
                    continue;

                if (handler.Kind == ControlFlowRegionKind.Catch &&
                    regions.Any(region =>
                        region.Kind == ControlFlowRegionKind.Filter &&
                        region.LastBlockOrdinal < handler.FirstBlockOrdinal &&
                        region.LastBlockOrdinal > associated.LastBlockOrdinal))
                {
                    continue;
                }
                for (var ordinal = associated.FirstBlockOrdinal;
                     ordinal <= associated.LastBlockOrdinal && ordinal < cfg.Blocks.Length;
                     ordinal++)
                {
                    if (!result.TryGetValue(ordinal, out var list))
                    {
                        list = ImmutableArray.CreateBuilder<int>();
                        result.Add(ordinal, list);
                    }
                    if (!list.Contains(handler.FirstBlockOrdinal))
                        list.Add(handler.FirstBlockOrdinal);
                }
            }
            return result.ToDictionary(pair => pair.Key, pair => pair.Value.ToImmutable());
        }

        private static bool StatesEqual(GuardFlowState? left, GuardFlowState? right)
        {
            if (left is null || right is null)
                return left is null && right is null;
            if (left.IsReachable != right.IsReachable ||
                left.Proofs.Count != right.Proofs.Count ||
                left.Aliases.Count != right.Aliases.Count ||
                left.Captures.Count != right.Captures.Count ||
                left.BooleanFacts.Count != right.BooleanFacts.Count ||
                left.OperationFacts.Count != right.OperationFacts.Count ||
                left.EvaluatedConditions.Count != right.EvaluatedConditions.Count ||
                left.DelegateWrites.Count != right.DelegateWrites.Count ||
                left.DelegateSummaries.Count != right.DelegateSummaries.Count ||
                left.UnknownDelegates.Count != right.UnknownDelegates.Count ||
                left.EscapedCallbackWrites.Count != right.EscapedCallbackWrites.Count ||
                left.WritableCapturePaths.Count != right.WritableCapturePaths.Count ||
                left.AddressExposed.Count != right.AddressExposed.Count)
            {
                return false;
            }
            foreach (var pair in left.Proofs)
            {
                if (!right.Proofs.TryGetValue(pair.Key, out var set) || !pair.Value.SetEquals(set))
                    return false;
            }
            foreach (var pair in left.Aliases)
            {
                if (!right.Aliases.TryGetValue(pair.Key, out var targets) ||
                    !pair.Value.Equals(targets))
                    return false;
            }
            foreach (var pair in left.Captures)
            {
                if (!right.Captures.TryGetValue(pair.Key, out var capture) ||
                    !ReferenceEquals(pair.Value.Value, capture.Value) ||
                    !(pair.Value.Path?.Equals(capture.Path) ?? capture.Path is null) ||
                    pair.Value.HasUnknownAlternative != capture.HasUnknownAlternative ||
                    !(pair.Value.AliasTargets?.Equals(capture.AliasTargets) ?? capture.AliasTargets is null) ||
                    pair.Value.Predicate is null != (capture.Predicate is null) ||
                    pair.Value.Predicate is not null &&
                    !SamePredicate(pair.Value.Predicate, capture.Predicate!))
                {
                    return false;
                }
            }
            foreach (var pair in left.BooleanFacts)
            {
                if (!right.BooleanFacts.TryGetValue(pair.Key, out var fact) ||
                    !SamePredicate(pair.Value, fact))
                {
                    return false;
                }
            }
            foreach (var pair in left.DelegateWrites)
            {
                if (!right.DelegateWrites.TryGetValue(pair.Key, out var writes) ||
                    !pair.Value.SetEquals(writes))
                {
                    return false;
                }
            }
            foreach (var pair in left.DelegateSummaries)
            {
                if (!right.DelegateSummaries.TryGetValue(pair.Key, out var summary) ||
                    !SameEffectSummary(pair.Value, summary))
                    return false;
            }
            if (!left.UnknownDelegates.SetEquals(right.UnknownDelegates) ||
                !left.EscapedCallbackWrites.SetEquals(right.EscapedCallbackWrites) ||
                !left.WritableCapturePaths.SetEquals(right.WritableCapturePaths))
            {
                return false;
            }
            if (!left.EvaluatedConditions.SetEquals(right.EvaluatedConditions))
                return false;
            foreach (var pair in left.OperationFacts)
            {
                if (!right.OperationFacts.TryGetValue(pair.Key, out var fact) ||
                    !SamePredicate(pair.Value, fact))
                    return false;
            }
            if (!left.AddressExposed.SetEquals(right.AddressExposed))
                return false;
            return true;
        }

        private static bool SamePredicate(PredicateFact left, PredicateFact right)
        {
            if (left.UnsafeForSource != right.UnsafeForSource ||
                left.Constraints.Count != right.Constraints.Count ||
                left.TrueReachable != right.TrueReachable ||
                left.FalseReachable != right.FalseReachable)
                return false;
            foreach (var pair in left.Constraints)
            {
                if (!right.Constraints.TryGetValue(pair.Key, out var other) ||
                    pair.Value.TrueKnown != other.TrueKnown ||
                    pair.Value.FalseKnown != other.FalseKnown ||
                    !pair.Value.TrueSet.SetEquals(other.TrueSet) ||
                    !pair.Value.FalseSet.SetEquals(other.FalseSet))
                    return false;
            }
            return true;
        }

        private static bool SameEffectSummary(EffectSummary left, EffectSummary right)
            => left.MayInvokeEscapedCallbacks == right.MayInvokeEscapedCallbacks &&
               left.MayWriteShared == right.MayWriteShared &&
               left.MayWriteExposed == right.MayWriteExposed &&
               left.ImmediateWrites.SetEquals(right.ImmediateWrites) &&
               left.EscapedCallbackWrites.SetEquals(right.EscapedCallbackWrites);

        private PredicateFact? GetEvaluatedConditionFacts(
            IOperation operation,
            GuardFlowState state)
        {
            if (state.TryGetOperationFact(operation, out var fact))
                return fact;
            if (state.WasConditionEvaluated(operation))
                return null;
            return TryGetConditionFacts(operation, state);
        }

        private void EvaluateOperation(
            IOperation operation,
            GuardFlowState state,
            bool report,
            PatternContext? pattern)
        {
            _context.CancellationToken.ThrowIfCancellationRequested();
            if (!state.IsReachable)
                return;

            if (operation is IAnonymousFunctionOperation or IFlowAnonymousFunctionOperation or ILocalFunctionOperation)
                return;

            if (operation is IDynamicInvocationOperation or
                IDynamicObjectCreationOperation or
                IDynamicMemberReferenceOperation or
                IDynamicIndexerAccessOperation)
            {
                foreach (var child in operation.ChildOperations)
                    EvaluateOperation(child, state, report, pattern);
                foreach (var argument in operation.DescendantsAndSelf().OfType<IArgumentOperation>())
                {
                    if (argument.Syntax is ArgumentSyntax syntax &&
                        syntax.RefKindKeyword.RawKind is
                            (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.RefKeyword or
                            (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.OutKeyword or
                            (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.InKeyword)
                    {
                        var targets = GetMayTargets(argument.Value, state);
                        foreach (var path in targets.Paths)
                            state.MarkAddressExposed(path);
                        InvalidateTargets(state, targets);
                    }
                }
                if (operation.Syntax is not null)
                {
                    var model = _context.Compilation.GetSemanticModel(operation.Syntax.SyntaxTree);
                    foreach (var syntax in operation.Syntax.DescendantNodesAndSelf()
                                 .OfType<ArgumentSyntax>())
                    {
                        if (syntax.RefKindKeyword.RawKind is not
                            ((int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.RefKeyword or
                             (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.OutKeyword or
                             (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.InKeyword))
                        {
                            continue;
                        }
                        var value = model.GetOperation(
                            syntax.Expression,
                            _context.CancellationToken);
                        var targets = GetMayTargets(value, state);
                        foreach (var path in targets.Paths)
                            state.MarkAddressExposed(path);
                        InvalidateTargets(state, targets);
                    }
                }
                // A dynamic dispatch may invoke callbacks registered in heap
                // storage even though Roslyn cannot resolve the target. Keep
                // this boundary consistent with the ordinary opaque-call
                // transfer before applying the exposed-storage fallback.
                state.InvalidateEscapedCallbacks();
                state.InvalidateUnknownDelegateEffects();
                state.InvalidateAll();
                return;
            }

            if (operation.Kind == OperationKind.YieldReturn)
            {
                foreach (var child in operation.ChildOperations)
                    EvaluateOperation(child, state, report, pattern);
                state.InvalidateSuspension();
                return;
            }

            if (operation.Kind == OperationKind.AddressOf)
            {
                foreach (var child in operation.ChildOperations)
                {
                    EvaluateOperation(child, state, report, pattern);
                    var path = GetPath(child, state);
                    if (path is not null)
                        state.MarkAddressExposed(path);
                }
                return;
            }

            switch (operation)
            {
                case IFlowCaptureOperation capture:
                    EvaluateOperation(capture.Value, state, report, pattern);
                    var capturePredicate = TryGetConditionFacts(capture.Value, state);
                    if (capturePredicate is null &&
                        TryGetBooleanConstant(capture.Value, out var constant))
                    {
                        capturePredicate = PredicateFact.FromConstraints(
                            Array.Empty<KeyValuePair<ReceiverPath, PredicateConstraint>>(),
                            trueReachable: constant,
                            falseReachable: !constant);
                    }
                    else if (capturePredicate is null)
                    {
                        capturePredicate = PredicateFact.FromProofs(state.Proofs);
                    }
                    else
                    {
                        capturePredicate = capturePredicate.WithCurrentProofs(state.Proofs);
                    }
                    state.SetCapture(
                        capture.Id,
                        new CaptureValue(
                            capture.Value,
                            GetPath(capture.Value, state) ?? capturePredicate?.Path,
                            capturePredicate,
                            aliasTargets: GetMayTargets(capture.Value, state)));
                    return;

                case IFlowCaptureReferenceOperation:
                    return;

                case IInvocationOperation invocation:
                    EvaluateInvocation(invocation, state, report, pattern);
                    return;

                case IFunctionPointerInvocationOperation functionPointer:
                    EvaluateFunctionPointerInvocation(functionPointer, state, report, pattern);
                    return;

                case IObjectCreationOperation creation:
                    EvaluateObjectCreation(creation, state, report, pattern);
                    return;

                case IEventAssignmentOperation eventAssignment:
                    EvaluateEventAssignment(eventAssignment, state, report, pattern);
                    return;

                case ISimpleAssignmentOperation assignment:
                    EvaluateAssignment(assignment, state, report, pattern);
                    return;

                case ICompoundAssignmentOperation compound:
                    EvaluateOperation(compound.Target, state, report, pattern);
                    EvaluateOperation(compound.Value, state, report, pattern);
                    InvalidateWriteTarget(compound.Target, state);
                    return;

                case IDeconstructionAssignmentOperation deconstruction:
                    EvaluateOperation(deconstruction.Target, state, report, pattern);
                    EvaluateOperation(deconstruction.Value, state, report, pattern);
                    InvalidateWriteTarget(deconstruction.Target, state);
                    if (TryGetDeconstructMethod(deconstruction) is { } deconstruct &&
                        !IsGeneratedPureDeconstruct(deconstruct, deconstruction.Value))
                    {
                        state.ApplyOpaqueEffects(new EffectSummary(
                            mayInvokeEscapedCallbacks: true,
                            mayWriteShared: true));
                    }
                    return;

                case IIncrementOrDecrementOperation increment:
                    EvaluateOperation(increment.Target, state, report, pattern);
                    InvalidateWriteTarget(increment.Target, state);
                    return;

                case IAwaitOperation awaitOperation:
                    EvaluateOperation(awaitOperation.Operation, state, report, pattern);
                    state.InvalidateSuspension();
                    return;

                case IPropertyReferenceOperation property:
                    EvaluatePropertyReference(property, state, report, pattern);
                    return;

                case IFieldReferenceOperation field:
                    EvaluateFieldReference(field, state, report, pattern);
                    return;

                case IIsPatternOperation isPattern:
                    EvaluatePatternOperation(isPattern, state, report);
                    return;

                case IConversionOperation conversion:
                    EvaluateOperation(conversion.Operand, state, report, pattern);
                    state.SetOperationFact(
                        conversion,
                        GetEvaluatedConditionFacts(conversion.Operand, state),
                        suppressFallback: false);
                    if (conversion.Conversion.IsUserDefined)
                    {
                        ApplyUserCodeBoundary(
                            state,
                            new[] { conversion.Operand },
                            conversion.Operand.Type is IPointerTypeSymbol);
                    }
                    return;

                case IBinaryOperation binary:
                    EvaluateOperation(binary.LeftOperand, state, report, pattern);
                    var leftFact = GetEvaluatedConditionFacts(binary.LeftOperand, state);
                    EvaluateOperation(binary.RightOperand, state, report, pattern);
                    var rightFact = GetEvaluatedConditionFacts(binary.RightOperand, state);
                    if (!state.TryGetOperationFact(binary.LeftOperand, out _))
                        leftFact = null;
                    var binaryFact = binary.OperatorKind switch
                    {
                        BinaryOperatorKind.ConditionalAnd => CombineAnd(leftFact, rightFact),
                        BinaryOperatorKind.ConditionalOr => CombineOr(leftFact, rightFact),
                        BinaryOperatorKind.And => CombineBitwiseAnd(leftFact, rightFact),
                        BinaryOperatorKind.Or => CombineBitwiseOr(leftFact, rightFact),
                        _ => null
                    };
                    state.SetOperationFact(
                        binary,
                        binaryFact,
                        binaryFact is not null ||
                        binary.OperatorKind is BinaryOperatorKind.ConditionalAnd or
                            BinaryOperatorKind.ConditionalOr or
                            BinaryOperatorKind.And or
                            BinaryOperatorKind.Or);
                    if (binary.OperatorMethod is not null)
                    {
                        ApplyUserCodeBoundary(
                            state,
                            new[] { binary.LeftOperand, binary.RightOperand },
                            binary.LeftOperand.Type is IPointerTypeSymbol ||
                            binary.RightOperand.Type is IPointerTypeSymbol);
                    }
                    return;

                case IUnaryOperation unary:
                    EvaluateOperation(unary.Operand, state, report, pattern);
                    var unaryFact = GetEvaluatedConditionFacts(unary.Operand, state)?.Swap();
                    state.SetOperationFact(unary, unaryFact, unaryFact is not null);
                    if (unary.OperatorMethod is not null)
                        ApplyUserCodeBoundary(
                            state,
                            new[] { unary.Operand },
                            unary.Operand.Type is IPointerTypeSymbol);
                    return;

                case IArgumentOperation argument:
                    EvaluateOperation(argument.Value, state, report, pattern);
                    return;

                default:
                    foreach (var child in operation.ChildOperations)
                        EvaluateOperation(child, state, report, pattern);
                    return;
            }
        }

        private void ApplyUserCodeBoundary(
            GuardFlowState state,
            IEnumerable<IOperation> operands,
            bool mayWriteExposed)
        {
            foreach (var operand in operands)
            {
                if (GetDelegateEffects(operand, state) is not { } operandSummary)
                    continue;
                state.ApplyOpaqueEffects(new EffectSummary(
                    operandSummary.ImmediateWrites,
                    operandSummary.EscapedCallbackWrites.Union(operandSummary.ImmediateWrites),
                    mayInvokeEscapedCallbacks: true,
                    mayWriteShared: operandSummary.MayWriteShared,
                    mayWriteExposed: operandSummary.MayWriteExposed));
            }
            state.ApplyOpaqueEffects(new EffectSummary(
                mayInvokeEscapedCallbacks: true,
                mayWriteShared: true,
                mayWriteExposed: mayWriteExposed));
        }

        private void EvaluateAssignment(
            ISimpleAssignmentOperation assignment,
            GuardFlowState state,
            bool report,
            PatternContext? pattern)
        {
            if (assignment.Target is IPropertyReferenceOperation propertyTarget &&
                propertyTarget.Instance is not null)
            {
                // A setter does not evaluate the getter. Evaluate its receiver
                // and index arguments first, then the RHS, and apply the
                // setter's mutation only after all values have been consumed.
                EvaluateOperation(propertyTarget.Instance, state, report, pattern);
                foreach (var argument in propertyTarget.Arguments)
                    EvaluateOperation(argument.Value, state, report, pattern);
            }
            else
            {
                EvaluateOperation(assignment.Target, state, report, pattern);
            }
            EvaluateOperation(assignment.Value, state, report, pattern);
            DetachReferenceRebindAliases(assignment.Target, state);

            if (IsRefStorageWriteTarget(assignment.Target))
            {
                // A by-ref field/indexer writes through the referenced cell,
                // while the receiver path itself remains a useful storage
                // identity for proofs obtained through that member. Invalidate
                // both views, and any previously address-exposed roots.
                InvalidateTargets(state, GetMayTargets(assignment.Target, state));
                state.Invalidate(GetPath(assignment.Target, state));
                state.InvalidateAll();
                return;
            }

            if (IsPointerIndirectionTarget(assignment.Target))
            {
                state.InvalidateAll();
                return;
            }

            if (assignment.IsRef && assignment.Target is ILocalReferenceOperation localTarget)
            {
                // Rebinding changes which storage the ref local observes; any
                // proof keyed by the old alias view must not survive it.
                state.Invalidate(new ReceiverPath(localTarget.Local));
                var aliasTargets = GetMayTargets(assignment.Value, state);
                state.SetAlias(localTarget.Local, aliasTargets);
                return;
            }

            if (assignment.Target is ILocalReferenceOperation pointerTarget &&
                pointerTarget.Local.Type is IPointerTypeSymbol)
            {
                state.Invalidate(new ReceiverPath(pointerTarget.Local));
                state.SetAlias(
                    pointerTarget.Local,
                    assignment.Value.Kind == OperationKind.AddressOf
                        ? GetMayTargets(assignment.Value, state)
                        : AliasTargetSet.Unknown());
                return;
            }

            if (assignment.Target is ILocalReferenceOperation local)
            {
                var hadAlias = state.Aliases.TryGetValue(local.Local, out var aliasTargets);
                if (hadAlias)
                    InvalidateTargets(state, aliasTargets!);
                else
                    state.Invalidate(new ReceiverPath(local.Local));
                state.ClearStorageFacts(local.Local);
                EffectSummary? assignedDelegateSummary = null;
                if (TryGetConditionFacts(assignment.Value, state) is { } predicate)
                    state.SetBooleanFact(local.Local, predicate);
                else if (TryGetAnonymousFunctionEffects(assignment.Value, state) is { } effects)
                {
                    assignedDelegateSummary = effects;
                    state.SetDelegateSummary(local.Local, effects);
                }
                else if (assignment.Value is IInvocationOperation functionFactory)
                {
                    var effectsFromFactory = GetLocalFunctionEffects(
                        functionFactory.TargetMethod,
                        state);
                    if (effectsFromFactory.ImmediateWrites.Count > 0 ||
                        effectsFromFactory.EscapedCallbackWrites.Count > 0 ||
                        effectsFromFactory.MayInvokeEscapedCallbacks)
                    {
                        assignedDelegateSummary = effectsFromFactory;
                        state.SetDelegateSummary(local.Local, effectsFromFactory);
                    }
                    else if (local.Local.Type?.TypeKind == TypeKind.Delegate)
                        state.SetUnknownDelegate(local.Local);
                }
                else if (local.Local.Type?.TypeKind == TypeKind.Delegate)
                    state.SetUnknownDelegate(local.Local);
                else
                    state.DelegateWrites.Remove(local.Local);
                if (assignedDelegateSummary is not null &&
                    hadAlias &&
                    aliasTargets!.Paths.Any(path => path.IsSharedStorage))
                {
                    RegisterEscapedCallback(state, assignedDelegateSummary);
                }
                if (state.AddressExposed.Contains(new ReceiverPath(local.Local)))
                    state.InvalidateAll();
                if (!hadAlias)
                    state.Aliases.Remove(local.Local);
                return;
            }

            state.Invalidate(GetPath(assignment.Target, state));
            if (assignment.Target is IParameterReferenceOperation parameterTarget &&
                state.AddressExposed.Contains(new ReceiverPath(parameterTarget.Parameter)))
            {
                state.InvalidateAll();
            }
            if (assignment.Target is IPropertyReferenceOperation propertyAssignment)
            {
                if (propertyAssignment.Instance is { } instance &&
                    instance.Type?.IsValueType == true)
                {
                    InvalidateTargets(state, GetMayTargets(instance, state));
                }
                if (GetDelegateEffects(assignment.Value, state) is { } setterSummary)
                {
                    state.ApplyOpaqueEffects(new EffectSummary(
                        setterSummary.ImmediateWrites,
                        setterSummary.EscapedCallbackWrites.Union(setterSummary.ImmediateWrites),
                        mayInvokeEscapedCallbacks: true,
                        mayWriteShared: true,
                        mayWriteExposed: setterSummary.MayWriteExposed));
                }
                else
                {
                    state.ApplyOpaqueEffects(new EffectSummary(
                        mayInvokeEscapedCallbacks: true,
                        mayWriteShared: true));
                }
            }
            else if (IsEscapingStorageTarget(assignment.Target, state) &&
                     GetDelegateEffects(assignment.Value, state) is { } fieldSummary)
            {
                RegisterEscapedCallback(state, fieldSummary);
            }
        }

        private static void RegisterEscapedCallback(
            GuardFlowState state,
            EffectSummary summary)
        {
            state.ApplyOpaqueEffects(new EffectSummary(
                escapedCallbackWrites: summary.ImmediateWrites.Union(summary.EscapedCallbackWrites),
                mayWriteExposed: summary.MayWriteExposed));
        }

        private static bool IsEscapingStorageTarget(
            IOperation target,
            GuardFlowState state)
        {
            if (target is IFieldReferenceOperation or IArrayElementReferenceOperation)
                return true;
            if (target is ILocalReferenceOperation local &&
                state.Aliases.TryGetValue(local.Local, out var alias))
            {
                return alias.IsUnknown || alias.Paths.Any(path => path.IsSharedStorage);
            }
            return false;
        }

        private static bool IsRefStorageWriteTarget(IOperation target)
            => target is IFieldReferenceOperation field &&
                   field.Field.RefKind != RefKind.None ||
               target is IPropertyReferenceOperation property &&
                   property.Property.GetMethod is { } getter &&
                   (getter.ReturnsByRef || getter.ReturnsByRefReadonly);

        private void DetachReferenceRebindAliases(
            IOperation target,
            GuardFlowState state)
        {
            if (target.Type?.IsReferenceType != true ||
                GetPath(target, state) is not { } path)
                return;
            state.DetachAliasesUnder(path);
        }

        private void InvalidateAssignmentTarget(
            IOperation target,
            GuardFlowState state)
        {
            if (target is ILocalReferenceOperation local)
            {
                DetachReferenceRebindAliases(target, state);
                InvalidateTargets(state, GetMayTargets(target, state));
                return;
            }

            if (target is IParameterReferenceOperation parameter)
            {
                DetachReferenceRebindAliases(target, state);
                InvalidateTargets(state, GetMayTargets(target, state));
                return;
            }

            // Writing a member changes that member's storage, not the
            // receiver object as a whole. The receiver may carry an
            // independent discriminator proof (for example holder.command
            // while holder.Count is updated).
            if (target is IFieldReferenceOperation or
                IPropertyReferenceOperation or
                IArrayElementReferenceOperation)
            {
                DetachReferenceRebindAliases(target, state);
                state.Invalidate(GetPath(target, state));
                return;
            }

            foreach (var child in target.ChildOperations)
                InvalidateAssignmentTarget(child, state);
        }

        private void InvalidateWriteTarget(
            IOperation target,
            GuardFlowState state)
        {
            if (IsRefStorageWriteTarget(target))
            {
                InvalidateTargets(state, GetMayTargets(target, state));
                state.Invalidate(GetPath(target, state));
                state.InvalidateAll();
                return;
            }

            if (IsPointerIndirectionTarget(target))
            {
                state.InvalidateAll();
                return;
            }

            InvalidateAssignmentTarget(target, state);
            state.Invalidate(GetPath(target, state));
            if (target is ILocalReferenceOperation local &&
                state.AddressExposed.Contains(new ReceiverPath(local.Local)) ||
                target is IParameterReferenceOperation parameter &&
                state.AddressExposed.Contains(new ReceiverPath(parameter.Parameter)))
            {
                state.InvalidateAll();
            }

            if (target is IPropertyReferenceOperation property &&
                property.Property.SetMethod is not null &&
                !IsGeneratedPureProperty(property.Property, property.Instance))
            {
                state.ApplyOpaqueEffects(new EffectSummary(
                    mayInvokeEscapedCallbacks: true,
                    mayWriteShared: true));
            }
        }

        private static void InvalidateTargets(
            GuardFlowState state,
            AliasTargetSet targets)
        {
            foreach (var path in targets.Paths)
                state.Invalidate(path);
            if (targets.IsUnknown)
                state.InvalidateAll();
        }

        private void EvaluateInvocation(
            IInvocationOperation invocation,
            GuardFlowState state,
            bool report,
            PatternContext? pattern)
        {
            if (invocation.Instance is not null)
                EvaluateOperation(invocation.Instance, state, report, pattern);

            var refTargets = new List<AliasTargetSet>();
            var delegateSummaries = new List<EffectSummary>();
            var unknownDelegateArgument = false;
            if (invocation.Instance is not null)
            {
                if (GetDelegateEffects(invocation.Instance, state) is { } instanceSummary)
                    delegateSummaries.Add(instanceSummary);
            }
            foreach (var argument in invocation.Arguments)
            {
                // Arguments are in source evaluation order. Ref/out mutation
                // starts only after all argument values have been evaluated.
                EvaluateOperation(argument.Value, state, report, pattern);
                if (argument.Parameter?.RefKind is RefKind.Ref or RefKind.Out)
                {
                    var targets = GetMayTargets(argument.Value, state);
                    foreach (var path in targets.Paths)
                        state.MarkAddressExposed(path);
                    refTargets.Add(targets);
                }
                if (GetDelegateEffects(argument.Value, state) is { } argumentSummary)
                    delegateSummaries.Add(argumentSummary);
                unknownDelegateArgument |= IsUnknownDelegate(argument.Value, state);
            }

            foreach (var targets in refTargets)
            {
                InvalidateTargets(state, targets);
            }
            if (invocation.Arguments.Any(argument =>
                    argument.Value.Type is IPointerTypeSymbol))
            {
                state.InvalidateAll();
            }
            foreach (var summary in delegateSummaries)
            {
                state.ApplyOpaqueEffects(new EffectSummary(
                    summary.ImmediateWrites,
                    summary.EscapedCallbackWrites.Union(summary.ImmediateWrites),
                    summary.MayInvokeEscapedCallbacks,
                    summary.MayWriteShared,
                    summary.MayWriteExposed));
            }

            var delegateInvocation = invocation.TargetMethod.MethodKind == MethodKind.DelegateInvoke ||
                invocation.TargetMethod.ContainingType?.TypeKind == TypeKind.Delegate;
            var unknownDelegateInvocation = delegateInvocation &&
                IsUnknownDelegate(invocation.Instance, state);
            if (unknownDelegateInvocation)
                state.InvalidateUnknownDelegateEffects();
            else if (delegateInvocation && delegateSummaries.Count == 0)
                state.ApplyOpaqueEffects(new EffectSummary(
                    mayInvokeEscapedCallbacks: true,
                    mayWriteShared: true));

            if (unknownDelegateArgument)
                state.InvalidateUnknownDelegateEffects();

            state.ApplyOpaqueEffects(GetLocalFunctionEffects(invocation.TargetMethod, state));

            if (!invocation.TargetMethod.IsStatic && invocation.Instance is not null &&
                invocation.Instance.Type?.IsValueType == true && !invocation.TargetMethod.IsReadOnly)
            {
                InvalidateTargets(state, GetMayTargets(invocation.Instance, state));
            }

            // Variant factories are generated pure projections. Other calls
            // may mutate shared/heap storage through a by-value reference.
            if (!HasVariantAttribute(invocation.TargetMethod))
            {
                state.ApplyOpaqueEffects(new EffectSummary(
                    mayInvokeEscapedCallbacks: true,
                    mayWriteShared: true,
                    mayWriteExposed: state.AddressExposed.Count > 0));
            }
        }

        private void EvaluateFunctionPointerInvocation(
            IFunctionPointerInvocationOperation invocation,
            GuardFlowState state,
            bool report,
            PatternContext? pattern)
        {
            EvaluateOperation(invocation.Target, state, report, pattern);
            var refTargets = new List<AliasTargetSet>();
            foreach (var argument in invocation.Arguments)
            {
                EvaluateOperation(argument.Value, state, report, pattern);
                if (argument.Parameter?.RefKind is RefKind.Ref or RefKind.Out)
                {
                    var targets = GetMayTargets(argument.Value, state);
                    foreach (var path in targets.Paths)
                        state.MarkAddressExposed(path);
                    refTargets.Add(targets);
                }
            }
            foreach (var targets in refTargets)
                InvalidateTargets(state, targets);
            if (invocation.Arguments.Any(argument =>
                    argument.Value.Type is IPointerTypeSymbol))
            {
                state.InvalidateAll();
            }
            state.ApplyOpaqueEffects(new EffectSummary(
                mayInvokeEscapedCallbacks: true,
                mayWriteShared: true,
                mayWriteExposed: invocation.Arguments.Any(argument =>
                    argument.Value.Type is IPointerTypeSymbol)));
        }

        private void EvaluateObjectCreation(
            IObjectCreationOperation creation,
            GuardFlowState state,
            bool report,
            PatternContext? pattern)
        {
            var refTargets = new List<AliasTargetSet>();
            var delegateSummaries = new List<EffectSummary>();
            foreach (var argument in creation.Arguments)
            {
                EvaluateOperation(argument.Value, state, report, pattern);
                if (argument.Parameter?.RefKind is RefKind.Ref or RefKind.Out)
                {
                    var targets = GetMayTargets(argument.Value, state);
                    foreach (var path in targets.Paths)
                        state.MarkAddressExposed(path);
                    refTargets.Add(targets);
                }
                if (GetDelegateEffects(argument.Value, state) is { } argumentSummary)
                    delegateSummaries.Add(argumentSummary);
            }

            foreach (var targets in refTargets)
                InvalidateTargets(state, targets);
            foreach (var summary in delegateSummaries)
            {
                state.ApplyOpaqueEffects(new EffectSummary(
                    summary.ImmediateWrites,
                    summary.EscapedCallbackWrites.Union(summary.ImmediateWrites),
                    summary.MayInvokeEscapedCallbacks,
                    summary.MayWriteShared,
                    summary.MayWriteExposed));
            }

            // Constructors and callback arguments are user code. Preserve
            // generated variant factories, but otherwise invalidate shared
            // storage after all argument expressions have been consumed.
            if (creation.Constructor is null || !HasVariantAttribute(creation.Constructor))
            {
                state.ApplyOpaqueEffects(new EffectSummary(
                    mayInvokeEscapedCallbacks: true,
                    mayWriteShared: true,
                    mayWriteExposed: creation.Arguments.Any(argument =>
                        argument.Value.Type is IPointerTypeSymbol)));
            }
        }

        private void EvaluateEventAssignment(
            IEventAssignmentOperation eventAssignment,
            GuardFlowState state,
            bool report,
            PatternContext? pattern)
        {
            EvaluateOperation(eventAssignment.EventReference, state, report, pattern);
            EvaluateOperation(eventAssignment.HandlerValue, state, report, pattern);

            if (GetDelegateEffects(eventAssignment.HandlerValue, state) is { } eventSummary)
            {
                state.ApplyOpaqueEffects(new EffectSummary(
                    eventSummary.ImmediateWrites,
                    eventSummary.EscapedCallbackWrites.Union(eventSummary.ImmediateWrites),
                    mayInvokeEscapedCallbacks: true,
                    mayWriteShared: true,
                    mayWriteExposed: eventSummary.MayWriteExposed));
            }
            else
            {
                state.ApplyOpaqueEffects(new EffectSummary(
                    mayInvokeEscapedCallbacks: true,
                    mayWriteShared: true));
            }

            var instance = eventAssignment.EventReference.ChildOperations.FirstOrDefault();
            if (instance is not null)
                InvalidateTargets(state, GetMayTargets(instance, state));
            state.ApplyOpaqueEffects(new EffectSummary(
                mayInvokeEscapedCallbacks: true,
                mayWriteShared: true));
        }

        private void EvaluatePropertyReference(
            IPropertyReferenceOperation property,
            GuardFlowState state,
            bool report,
            PatternContext? pattern)
        {
            if (property.Instance is not null)
                EvaluateOperation(property.Instance, state, report, pattern);

            if (TryGetAccessInfo(property, _duAttribute, out var access, _accessCache))
            {
                if (report && !IsInsideNameOf(property))
                    ReportAccess(property, access, state, pattern);
                return;
            }

            if (IsGeneratedPureProperty(property.Property, property.Instance))
            {
                if (TryGetPredicate(property, state, out var predicate))
                    state.SetOperationFact(property, predicate);
                return;
            }

            if (property.Instance is not null &&
                property.Instance.Type?.IsValueType == true &&
                property.Property.GetMethod?.IsReadOnly != true)
            {
                // A non-readonly struct getter can replace its receiver even
                // when the getter is syntactically a read.
                InvalidateTargets(state, GetMayTargets(property.Instance, state));
                var receiverPath = GetPath(property.Instance, state);
                if (pattern is not null &&
                    pattern.SourcePath is { } sourcePath &&
                    receiverPath is { } &&
                    (receiverPath.Equals(sourcePath) || receiverPath.StartsWith(sourcePath)))
                {
                    pattern.InvalidateSource(state);
                }
            }

            // A user getter is opaque for shared paths. It can replace a
            // shared union between two textual reads.
            state.ApplyOpaqueEffects(new EffectSummary(
                mayInvokeEscapedCallbacks: true,
                mayWriteShared: true));
        }

        private void EvaluateFieldReference(
            IFieldReferenceOperation field,
            GuardFlowState state,
            bool report,
            PatternContext? pattern)
        {
            if (field.Instance is not null)
                EvaluateOperation(field.Instance, state, report, pattern);
            if (report && TryGetAccessInfo(field, _duAttribute, out var access, _accessCache) &&
                !IsInsideNameOf(field))
            {
                ReportAccess(field, access, state, pattern);
            }
        }

        private void EvaluatePatternOperation(
            IIsPatternOperation isPattern,
            GuardFlowState state,
            bool report)
        {
            var facts = TryGetPatternFacts(isPattern, state);
            state.SetOperationFact(
                isPattern,
                facts?.ToPredicateFact());
            var sourcePath = GetPath(isPattern.Value, state);
            EvaluateOperation(isPattern.Value, state, report, pattern: null);

            var context = facts is null
                ? null
                : new PatternContext(facts.UnionType, facts.TrueSet, facts.UnsafeForSource, sourcePath);
            if (isPattern.Pattern is IRecursivePatternOperation recursive &&
                recursive.DeconstructSymbol is IMethodSymbol deconstruct &&
                !IsGeneratedPureDeconstruct(deconstruct, isPattern.Value))
            {
                if (isPattern.Value.Type?.IsValueType == true)
                    InvalidateTargets(state, GetMayTargets(isPattern.Value, state));
                context?.InvalidateSource(state);
                state.ApplyOpaqueEffects(new EffectSummary(
                    mayInvokeEscapedCallbacks: true,
                    mayWriteShared: true));
            }
            foreach (var child in isPattern.Pattern.ChildOperations)
                EvaluateOperation(child, state, report, context);

            if (facts?.UnsafeForSource == true)
                state.Invalidate(sourcePath);
        }

        private void ReportAccess(
            IOperation operation,
            AccessInfo access,
            GuardFlowState state,
            PatternContext? pattern)
        {
            var guarded = TryGetGuardedVariants(access, state, pattern);
            if (guarded is null)
            {
                if (TryMarkReported(operation, AnalyzerDescriptors.SPIRE014_UnguardedFieldAccess.Id))
                {
                    _context.ReportDiagnostic(Diagnostic.Create(
                        AnalyzerDescriptors.SPIRE014_UnguardedFieldAccess,
                        operation.Syntax!.GetLocation(),
                        access.Member.Name,
                        FirstOwningVariant(access)));
                }
                return;
            }

            // A join that can contain every declared variant carries no useful
            // discriminator proof. Keep it distinct from a strict non-empty
            // subset, which is what SPIRE013 describes.
            if (access.Info.VariantNames.Length > 1 &&
                guarded.Count == access.Info.VariantNames.Length)
                guarded = null;
            if (guarded is null)
            {
                if (TryMarkReported(operation, AnalyzerDescriptors.SPIRE014_UnguardedFieldAccess.Id))
                {
                    _context.ReportDiagnostic(Diagnostic.Create(
                        AnalyzerDescriptors.SPIRE014_UnguardedFieldAccess,
                        operation.Syntax!.GetLocation(),
                        access.Member.Name,
                        FirstOwningVariant(access)));
                }
                return;
            }

            if (guarded.All(access.OwningVariants.Contains))
                return;

            var ordered = access.Info.VariantNames.Where(guarded.Contains).ToList();
            var display = string.Join(" or ", ordered.Select(variant => $"'kind={variant}'"));
            var help = ordered.Count == 1
                ? $"Within this scope, only the field '{VariantNaming.AccessorName(ordered[0], access.Info.VariantNames)}' contains valid data."
                : "Check 'kind' again before accessing variant data; this scope can contain more than one variant.";
            if (TryMarkReported(operation, AnalyzerDescriptors.SPIRE013_WrongVariantFieldAccess.Id))
            {
                _context.ReportDiagnostic(Diagnostic.Create(
                    AnalyzerDescriptors.SPIRE013_WrongVariantFieldAccess,
                    operation.Syntax!.GetLocation(),
                    access.Member.Name,
                    FirstOwningVariant(access),
                    display,
                    help));
            }
        }

        private bool TryMarkReported(IOperation operation, string diagnosticId)
        {
            if (operation.Syntax is null)
                return true;
            return _reported.TryAdd(
                new ReportKey(diagnosticId, operation.Syntax),
                0);
        }

        private ImmutableHashSet<string>? TryGetGuardedVariants(
            AccessInfo access,
            GuardFlowState state,
            PatternContext? pattern)
        {
            if (TryGetFactoryVariant(access.Instance, access.Info, out var factoryVariant))
                return ImmutableHashSet.Create(StringComparer.Ordinal, factoryVariant);

            if (pattern is not null &&
                !pattern.UnsafeForSource &&
                SymbolEqualityComparer.Default.Equals(
                    pattern.UnionType,
                    access.Info.KindEnumType?.ContainingType) &&
                pattern.Variants.Count > 0)
            {
                return pattern.Variants;
            }

            var path = GetPath(access.Instance, state);
            return path is not null && TryGetProof(path, state, out var variants)
                ? variants
                : null;
        }

        private static bool TryGetProof(
            ReceiverPath path,
            GuardFlowState state,
            out ImmutableHashSet<string> variants)
        {
            if (state.Proofs.TryGetValue(path, out variants!))
                return true;
            if (path.IsThis || path.Segments.Length == 0 ||
                !state.Aliases.TryGetValue(path.Segments[0], out var aliases) ||
                aliases.IsUnknown)
            {
                variants = ImmutableHashSet<string>.Empty;
                return false;
            }

            var suffix = path.Segments.Skip(1);
            var found = false;
            var combined = ImmutableHashSet<string>.Empty;
            foreach (var target in aliases.Paths)
            {
                var candidate = target;
                foreach (var segment in suffix)
                    candidate = candidate.Append(segment);
                if (!state.Proofs.TryGetValue(candidate, out var targetVariants))
                {
                    variants = ImmutableHashSet<string>.Empty;
                    return false;
                }
                found = true;
                combined = combined.Union(targetVariants);
            }
            variants = combined;
            return found;
        }

        private static string FirstOwningVariant(AccessInfo access)
        {
            foreach (var variant in access.Info.VariantNames)
            {
                if (access.OwningVariants.Contains(variant))
                    return variant;
            }
            return access.OwningVariants.First();
        }

        private PredicateFact? TryGetConditionFacts(
            IOperation operation,
            GuardFlowState state)
        {
            if (operation is IFlowCaptureReferenceOperation captureReference &&
                state.Captures.TryGetValue(captureReference.Id, out var captureValue))
            {
                return captureValue.Predicate;

            }

            if (state.TryGetOperationFact(operation, out var operationFact))
                return operationFact;
            if (state.WasConditionEvaluated(operation))
                return null;

            operation = ExpandCapture(operation, state);

            if (operation is ILocalReferenceOperation local &&
                state.BooleanFacts.TryGetValue(local.Local, out var localFact))
            {
                return localFact;
            }

            if (operation is IUnaryOperation unary &&
                unary.OperatorKind == UnaryOperatorKind.Not)
            {
                return TryGetConditionFacts(unary.Operand, state)?.Swap();
            }

            if (operation is IBinaryOperation binary && binary.OperatorMethod is null)
            {
                if (binary.OperatorKind == BinaryOperatorKind.ConditionalAnd)
                {
                    var leftFacts = TryGetConditionFacts(binary.LeftOperand, state);
                    var rightFacts = TryGetConditionFacts(binary.RightOperand, state);
                    return CombineAnd(leftFacts, rightFacts);
                }
                if (binary.OperatorKind == BinaryOperatorKind.ConditionalOr)
                {
                    return CombineOr(
                        TryGetConditionFacts(binary.LeftOperand, state),
                        TryGetConditionFacts(binary.RightOperand, state));
                }
                if (binary.OperatorKind == BinaryOperatorKind.And)
                {
                    return CombineBitwiseAnd(
                        TryGetConditionFacts(binary.LeftOperand, state),
                        TryGetConditionFacts(binary.RightOperand, state));
                }
                if (binary.OperatorKind == BinaryOperatorKind.Or)
                {
                    return CombineBitwiseOr(
                        TryGetConditionFacts(binary.LeftOperand, state),
                        TryGetConditionFacts(binary.RightOperand, state));
                }

                if (binary.OperatorKind is BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals)
                {
                    if (TryGetKindAccess(binary.LeftOperand, state, out var leftPath, out var leftInfo) &&
                        TryGetKindVariant(binary.RightOperand, leftInfo, out var rightVariant))
                    {
                        return CreateEqualityFact(
                            leftPath,
                            leftInfo,
                            rightVariant,
                            binary.OperatorKind == BinaryOperatorKind.Equals);
                    }
                    if (TryGetKindAccess(binary.RightOperand, state, out var rightPath, out var rightInfo) &&
                        TryGetKindVariant(binary.LeftOperand, rightInfo, out var leftVariant))
                    {
                        return CreateEqualityFact(
                            rightPath,
                            rightInfo,
                            leftVariant,
                            binary.OperatorKind == BinaryOperatorKind.Equals);
                    }
                    var leftPredicate = TryGetConditionFacts(binary.LeftOperand, state);
                    if (leftPredicate is not null &&
                        TryGetBooleanConstant(binary.RightOperand, out var rightBoolean))
                    {
                        return ResolveBooleanComparison(leftPredicate, rightBoolean, binary.OperatorKind);
                    }
                    var rightPredicate = TryGetConditionFacts(binary.RightOperand, state);
                    if (rightPredicate is not null &&
                        TryGetBooleanConstant(binary.LeftOperand, out var leftBoolean))
                    {
                        return ResolveBooleanComparison(rightPredicate, leftBoolean, binary.OperatorKind);
                    }
                }
            }

            if (operation is IPropertyReferenceOperation property &&
                TryGetPredicate(property, state, out var directPredicate))
            {
                return directPredicate;
            }

            if (operation is IIsPatternOperation isPattern)
                return TryGetPatternFacts(isPattern, state)?.ToPredicateFact();

            if (TryGetBooleanConstant(operation, out var constant))
            {
                return PredicateFact.FromConstraints(
                    Array.Empty<KeyValuePair<ReceiverPath, PredicateConstraint>>(),
                    trueReachable: constant,
                    falseReachable: !constant);
            }

            return null;
        }

        private bool TryGetPredicate(
            IOperation operation,
            GuardFlowState state,
            out PredicateFact? predicate)
        {
            operation = ExpandCapture(operation, state);
            operation = StripIdentityConversions(operation);
            if (operation is IPropertyReferenceOperation propertyReference &&
                propertyReference.Instance is not null &&
                propertyReference.Property.ContainingType is { } containingType)
            {
                var info = UnionTypeInfo.TryCreate(containingType, _duAttribute);
                if (info is not null && info.IsStructUnion &&
                    info.KindEnumType?.ContainingType is { } unionType &&
                    GeneratedVariantPredicate.TryGetGeneratedProperty(
                        propertyReference,
                        unionType,
                        info.VariantNames,
                        out var variant))
                {
                    predicate = CreatePredicate(
                        GetPath(propertyReference.Instance, state),
                        info,
                        variant);
                    return true;
                }
            }
            predicate = null;
            return false;
        }

        private static bool TryGetCapturedPredicate(
            IOperation operation,
            GuardFlowState state,
            out PredicateFact? predicate)
        {
            if (operation is IFlowCaptureReferenceOperation capture &&
                state.Captures.TryGetValue(capture.Id, out var value))
            {
                predicate = value.Predicate;
                if (predicate is not null && value.HasUnknownAlternative)
                {
                    var retained = predicate.EnumerateConstraints()
                        .Where(pair => state.Proofs.ContainsKey(pair.Key))
                        .Select(pair => new KeyValuePair<ReceiverPath, PredicateConstraint>(
                            pair.Key,
                            new PredicateConstraint(
                                pair.Value.TrueSet,
                                pair.Value.TrueKnown,
                                null,
                                false)))
                        .ToArray();
                    if (retained.Length == 0)
                    {
                        predicate = null;
                        return false;
                    }
                    predicate = PredicateFact.FromConstraints(retained);
                }
                return predicate is not null;
            }

            predicate = null;
            return false;
        }

        private bool TryGetKindAccess(
            IOperation operation,
            GuardFlowState state,
            out ReceiverPath? path,
            out UnionTypeInfo info)
        {
            operation = ExpandCapture(operation, state);
            operation = StripIdentityConversions(operation);
            if (operation is IPropertyReferenceOperation property &&
                property.Property.Name == "kind" && property.Instance is not null &&
                property.Property.ContainingType is { } containingType &&
                UnionTypeInfo.TryCreate(containingType, _duAttribute) is { } created &&
                created.KindEnumType is not null)
            {
                path = GetPath(property.Instance, state);
                info = created;
                return true;
            }
            path = null;
            info = null!;
            return false;
        }

        private bool TryGetKindVariant(
            IOperation operation,
            UnionTypeInfo info,
            out string variant)
        {
            operation = StripIdentityConversions(operation);
            if (operation is IFieldReferenceOperation field &&
                field.Field.ContainingType is { } containing &&
                SymbolEqualityComparer.Default.Equals(containing, info.KindEnumType) &&
                field.Field.HasConstantValue)
            {
                var ordinal = Convert.ToInt32(field.Field.ConstantValue);
                if (ordinal >= 0 && ordinal < info.VariantNames.Length)
                {
                    variant = info.VariantNames[ordinal];
                    return true;
                }
            }
            variant = string.Empty;
            return false;
        }

        private PredicateFact CreateEqualityFact(
            ReceiverPath? path,
            UnionTypeInfo info,
            string variant,
            bool equality)
        {
            var one = ImmutableHashSet.Create(StringComparer.Ordinal, variant);
            var complement = info.VariantNames
                .ToImmutableHashSet(StringComparer.Ordinal)
                .Except(one);
            return equality
                ? new PredicateFact(path, one, true, complement, true)
                : new PredicateFact(path, complement, true, one, true);
        }

        private PredicateFact CreatePredicate(
            ReceiverPath? path,
            UnionTypeInfo info,
            string variant)
        {
            var one = ImmutableHashSet.Create(StringComparer.Ordinal, variant);
            var complement = info.VariantNames
                .ToImmutableHashSet(StringComparer.Ordinal)
                .Except(one);
            return new PredicateFact(path, one, true, complement, true);
        }

        private static PredicateFact ResolveBooleanComparison(
            PredicateFact predicate,
            bool constant,
            BinaryOperatorKind operatorKind)
        {
            var positive = operatorKind == BinaryOperatorKind.Equals ? constant : !constant;
            return positive ? predicate : predicate.Swap();
        }

        private static PredicateFact? CombineAnd(PredicateFact? left, PredicateFact? right)
        {
            if (left is null && right is null)
                return null;
            if (left is null)
                return RetainAndTrueFacts(right!);
            if (right is null)
                return RetainAndTrueFacts(left);

            var constraints = new Dictionary<ReceiverPath, PredicateConstraint>();
            foreach (var path in left.Constraints.Keys.Union(right.Constraints.Keys))
            {
                var hasLeft = left.Constraints.TryGetValue(path, out var l);
                var hasRight = right.Constraints.TryGetValue(path, out var r);
                var trueKnown = hasLeft && l!.TrueKnown || hasRight && r!.TrueKnown;
                var trueSet = hasLeft && l!.TrueKnown && hasRight && r!.TrueKnown
                    ? l.TrueSet.Intersect(r.TrueSet)
                    : hasLeft && l!.TrueKnown
                        ? l.TrueSet
                        : hasRight && r!.TrueKnown
                            ? r.TrueSet
                            : ImmutableHashSet<string>.Empty;
                var falseKnown = hasLeft && hasRight && l!.FalseKnown && r!.FalseKnown;
                var falseSet = falseKnown
                    ? l!.FalseSet.Union(r!.FalseSet)
                    : ImmutableHashSet<string>.Empty;
                if (trueKnown || falseKnown)
                    constraints[path] = new PredicateConstraint(
                        trueSet,
                        trueKnown,
                        falseSet,
                        falseKnown);
            }
            var trueReachable = left.TrueReachable && right.TrueReachable;
            var falseReachable = left.FalseReachable ||
                left.TrueReachable && right.FalseReachable;
            return constraints.Count == 0
                ? null
                : PredicateFact.FromConstraints(
                    constraints,
                    left.UnsafeForSource || right.UnsafeForSource,
                    trueReachable,
                    falseReachable);
        }

        private static PredicateFact? CombineOr(PredicateFact? left, PredicateFact? right)
        {
            if (left is null || right is null)
                return null;
            var constraints = new Dictionary<ReceiverPath, PredicateConstraint>();
            foreach (var path in left.Constraints.Keys.Union(right.Constraints.Keys))
            {
                var hasLeft = left.Constraints.TryGetValue(path, out var l);
                var hasRight = right.Constraints.TryGetValue(path, out var r);
                var falseKnown = hasLeft && l!.FalseKnown || hasRight && r!.FalseKnown;
                var falseSet = hasLeft && l!.FalseKnown && hasRight && r!.FalseKnown
                    ? l.FalseSet.Intersect(r.FalseSet)
                    : hasLeft && l!.FalseKnown
                        ? l.FalseSet
                        : hasRight && r!.FalseKnown
                            ? r.FalseSet
                            : ImmutableHashSet<string>.Empty;
                // A true result of A || B only narrows one receiver when both
                // predicates describe that same receiver.
                var trueKnown = hasLeft && hasRight && l!.TrueKnown && r!.TrueKnown;
                var trueSet = trueKnown
                    ? l!.TrueSet.Union(r!.TrueSet)
                    : ImmutableHashSet<string>.Empty;
                if (trueKnown || falseKnown)
                    constraints[path] = new PredicateConstraint(
                        trueSet,
                        trueKnown,
                        falseSet,
                        falseKnown);
            }
            if (!left.TrueReachable && left.FalseReachable && right.TrueReachable)
            {
                foreach (var pair in right.Constraints)
                {
                    if (pair.Value.TrueKnown)
                    {
                        constraints[pair.Key] = new PredicateConstraint(
                            pair.Value.TrueSet,
                            true,
                            pair.Value.FalseSet,
                            pair.Value.FalseKnown);
                    }
                }
            }
            else if (!right.TrueReachable && right.FalseReachable && left.TrueReachable)
            {
                foreach (var pair in left.Constraints)
                {
                    if (pair.Value.TrueKnown)
                    {
                        constraints[pair.Key] = new PredicateConstraint(
                            pair.Value.TrueSet,
                            true,
                            pair.Value.FalseSet,
                            pair.Value.FalseKnown);
                    }
                }
            }
            var trueReachable = left.TrueReachable ||
                left.FalseReachable && right.TrueReachable;
            var falseReachable = left.FalseReachable && right.FalseReachable;
            return constraints.Count == 0
                ? null
                : PredicateFact.FromConstraints(
                    constraints,
                    left.UnsafeForSource || right.UnsafeForSource,
                    trueReachable,
                    falseReachable);
        }

        private static PredicateFact? CombineBitwiseAnd(PredicateFact? left, PredicateFact? right)
        {
            if (left is null && right is null)
                return null;
            if (left is null)
                return RetainAndTrueFacts(right!);
            if (right is null)
                return RetainAndTrueFacts(left);

            var constraints = new Dictionary<ReceiverPath, PredicateConstraint>();
            foreach (var path in left.Constraints.Keys.Union(right.Constraints.Keys))
            {
                var hasLeft = left.Constraints.TryGetValue(path, out var l);
                var hasRight = right.Constraints.TryGetValue(path, out var r);
                var trueKnown = hasLeft && l!.TrueKnown || hasRight && r!.TrueKnown;
                var trueSet = hasLeft && l!.TrueKnown && hasRight && r!.TrueKnown
                    ? l.TrueSet.Intersect(r.TrueSet)
                    : hasLeft && l!.TrueKnown
                        ? l.TrueSet
                        : hasRight && r!.TrueKnown
                            ? r.TrueSet
                            : ImmutableHashSet<string>.Empty;
                if (trueKnown)
                    constraints[path] = new PredicateConstraint(trueSet, true, null, false);
            }
            var trueReachable = left.TrueReachable && right.TrueReachable;
            var falseReachable = left.FalseReachable ||
                left.TrueReachable && right.FalseReachable;
            return constraints.Count == 0
                ? null
                : PredicateFact.FromConstraints(
                    constraints,
                    left.UnsafeForSource || right.UnsafeForSource,
                    trueReachable,
                    falseReachable);
        }

        private static PredicateFact? CombineBitwiseOr(PredicateFact? left, PredicateFact? right)
        {
            if (left is null || right is null)
                return null;
            var constraints = new Dictionary<ReceiverPath, PredicateConstraint>();
            foreach (var path in left.Constraints.Keys.Union(right.Constraints.Keys))
            {
                var hasLeft = left.Constraints.TryGetValue(path, out var l);
                var hasRight = right.Constraints.TryGetValue(path, out var r);
                var falseKnown = hasLeft && l!.FalseKnown || hasRight && r!.FalseKnown;
                var falseSet = hasLeft && l!.FalseKnown && hasRight && r!.FalseKnown
                    ? l.FalseSet.Intersect(r.FalseSet)
                    : hasLeft && l!.FalseKnown
                        ? l.FalseSet
                        : hasRight && r!.FalseKnown
                            ? r.FalseSet
                            : ImmutableHashSet<string>.Empty;
                if (falseKnown)
                    constraints[path] = new PredicateConstraint(null, false, falseSet, true);
            }
            var trueReachable = left.TrueReachable ||
                left.FalseReachable && right.TrueReachable;
            var falseReachable = left.FalseReachable && right.FalseReachable;
            return constraints.Count == 0
                ? null
                : PredicateFact.FromConstraints(
                    constraints,
                    left.UnsafeForSource || right.UnsafeForSource,
                    trueReachable,
                    falseReachable);
        }

        private static PredicateFact RetainAndTrueFacts(PredicateFact source)
        {
            var constraints = source.EnumerateConstraints()
                .Where(pair => pair.Value.TrueKnown)
                .Select(pair => new KeyValuePair<ReceiverPath, PredicateConstraint>(
                    pair.Key,
                    new PredicateConstraint(pair.Value.TrueSet, true, null, false)))
                .ToArray();
            return PredicateFact.FromConstraints(
                constraints,
                source.UnsafeForSource,
                source.TrueReachable,
                true);
        }

        private GuardFlowState ApplyBranch(
            GuardFlowState state,
            PredicateFact? facts,
            bool conditionTrue)
        {
            var result = state.Clone();
            if (facts is null)
                return result;
            if (!(conditionTrue ? facts.TrueReachable : facts.FalseReachable))
            {
                result.MarkUnreachable();
                return result;
            }
            if (facts.UnsafeForSource)
                return result;
            foreach (var pair in facts.EnumerateConstraints())
            {
                var known = conditionTrue ? pair.Value.TrueKnown : pair.Value.FalseKnown;
                if (known)
                    result.Refine(
                        pair.Key,
                        conditionTrue ? pair.Value.TrueSet : pair.Value.FalseSet);
            }
            return result;
        }

        private PatternFactData? TryGetPatternFacts(
            IIsPatternOperation isPattern,
            GuardFlowState state)
        {
            var source = ExpandCapture(isPattern.Value, state);
            var sourcePath = GetPath(source, state);

            if (TryGetKindAccess(source, state, out var kindPath, out var kindInfo))
                return TryGetEnumPatternFacts(isPattern.Pattern, kindPath, kindInfo);

            if (source.Type is null)
                return null;
            var info = UnionTypeInfo.TryCreate(source.Type, _duAttribute);
            if (info is null || !info.IsStructUnion || info.KindEnumType?.ContainingType is not { } unionType)
                return null;

            var result = ExtractUnionPattern(isPattern.Pattern, info, unionType);
            if (result is not null)
                result.Path = sourcePath;
            return result;
        }

        private PatternFactData? TryGetEnumPatternFacts(
            IPatternOperation pattern,
            ReceiverPath? path,
            UnionTypeInfo info)
        {
            var trueSet = ExtractEnumPattern(pattern, info);
            if (trueSet is null)
                return null;
            var domain = info.VariantNames.ToImmutableHashSet(StringComparer.Ordinal);
            var unrestricted = IsUnrestrictedEnumPattern(pattern);
            return new PatternFactData(
                info.KindEnumType!.ContainingType!,
                path,
                trueSet,
                unrestricted ? domain.Except(trueSet) : null,
                unrestricted,
                unsafeForSource: false);
        }

        private PatternFactData? ExtractUnionPattern(
            IPatternOperation pattern,
            UnionTypeInfo info,
            INamedTypeSymbol unionType)
        {
            if (pattern is INegatedPatternOperation negated)
            {
                var inner = ExtractUnionPattern(negated.Pattern, info, unionType);
                if (inner is null)
                    return null;
                return new PatternFactData(
                    unionType,
                    inner.Path,
                    inner.FalseSet,
                    inner.TrueSet,
                    inner.FalseKnown,
                    inner.UnsafeForSource);
            }

            if (pattern is IBinaryPatternOperation orPattern &&
                orPattern.OperatorKind == BinaryOperatorKind.Or)
            {
                var left = ExtractUnionPattern(orPattern.LeftPattern, info, unionType);
                var right = ExtractUnionPattern(orPattern.RightPattern, info, unionType);
                return left is null || right is null
                    ? null
                    : PatternFactData.CombineOr(left, right, unionType);
            }

            if (pattern is IBinaryPatternOperation andPattern &&
                andPattern.OperatorKind == BinaryOperatorKind.And)
            {
                var left = ExtractUnionPattern(andPattern.LeftPattern, info, unionType);
                var right = ExtractUnionPattern(andPattern.RightPattern, info, unionType);
                return left is null || right is null
                    ? null
                    : PatternFactData.CombineAnd(left, right, unionType);
            }

            if (pattern is not IRecursivePatternOperation recursive)
                return null;

            var found = false;
            var variants = ImmutableHashSet<string>.Empty;
            var unrestricted = true;
            var unsafeSource = false;

            foreach (var property in recursive.PropertySubpatterns)
            {
                if (property.Member is not IPropertyReferenceOperation propertyReference)
                {
                    unrestricted = false;
                    continue;
                }

                if (propertyReference.Property.Name == "kind")
                {
                    var extracted = new List<string>();
                    ExhaustivenessChecker.CollectVariants(
                        property.Pattern,
                        info.KindEnumType,
                        info.VariantNames,
                        info.VariantTypes,
                        info.IsStructUnion,
                        extracted);
                    if (extracted.Count > 0)
                    {
                        found = true;
                        variants = variants.Union(extracted);
                    }
                    else
                    {
                        unrestricted = false;
                    }
                    continue;
                }

                if (GeneratedVariantPredicate.TryGetPropertyPatternVariants(
                        property,
                        unionType,
                        info.VariantNames,
                        out var generatedVariants))
                {
                    found = true;
                    variants = variants.Union(generatedVariants);
                    continue;
                }

                if (!IsUnrestrictedPayloadPattern(property.Pattern))
                    unrestricted = false;
                if (!IsPureGeneratedProjection(propertyReference))
                    unsafeSource = true;
            }

            if (!recursive.DeconstructionSubpatterns.IsEmpty)
            {
                var extracted = new List<string>();
                ExhaustivenessChecker.CollectVariants(
                    recursive.DeconstructionSubpatterns[0],
                    info.KindEnumType,
                    info.VariantNames,
                    info.VariantTypes,
                    info.IsStructUnion,
                    extracted);
                if (extracted.Count > 0)
                {
                    found = true;
                    variants = variants.Union(extracted);
                }
                for (var i = 1; i < recursive.DeconstructionSubpatterns.Length; i++)
                {
                    if (!IsUnrestrictedPayloadPattern(recursive.DeconstructionSubpatterns[i]))
                        unrestricted = false;
                }
            }

            if (!found)
                return null;

            var domain = info.VariantNames.ToImmutableHashSet(StringComparer.Ordinal);
            return new PatternFactData(
                unionType,
                path: null,
                variants,
                unrestricted ? domain.Except(variants) : null,
                unrestricted,
                unsafeSource);
        }

        private ImmutableHashSet<string>? ExtractEnumPattern(
            IPatternOperation pattern,
            UnionTypeInfo info)
        {
            if (pattern is IConstantPatternOperation constant &&
                TryGetKindVariant(constant.Value, info, out var variant))
            {
                return ImmutableHashSet.Create(StringComparer.Ordinal, variant);
            }
            if (pattern is IBinaryPatternOperation orPattern &&
                orPattern.OperatorKind == BinaryOperatorKind.Or)
            {
                var left = ExtractEnumPattern(orPattern.LeftPattern, info);
                var right = ExtractEnumPattern(orPattern.RightPattern, info);
                return left is null || right is null ? null : left.Union(right);
            }
            if (pattern is IBinaryPatternOperation andPattern &&
                andPattern.OperatorKind == BinaryOperatorKind.And)
            {
                var left = ExtractEnumPattern(andPattern.LeftPattern, info);
                var right = ExtractEnumPattern(andPattern.RightPattern, info);
                return left is null || right is null ? null : left.Intersect(right);
            }
            if (pattern is INegatedPatternOperation negated)
            {
                var inner = ExtractEnumPattern(negated.Pattern, info);
                return inner is null
                    ? null
                    : info.VariantNames.ToImmutableHashSet(StringComparer.Ordinal).Except(inner);
            }
            if (pattern is IDiscardPatternOperation)
                return info.VariantNames.ToImmutableHashSet(StringComparer.Ordinal);
            return null;
        }

        private ReceiverPath? GetPath(IOperation? operation, GuardFlowState state)
        {
            if (operation is null)
                return null;

            operation = ExpandCapture(operation, state);
            if (operation is IConversionOperation conversion)
            {
                if (!IsIdentityConversion(conversion) &&
                    (!conversion.Conversion.IsReference ||
                     conversion.Conversion.IsUserDefined))
                    return null;
                return GetPath(conversion.Operand, state);
            }

            switch (operation)
            {
                case ILocalReferenceOperation local:
                    if (state.Aliases.TryGetValue(local.Local, out var alias))
                        return new ReceiverPath(local.Local);
                    return new ReceiverPath(local.Local);
                case IParameterReferenceOperation parameter:
                    return new ReceiverPath(parameter.Parameter);
                case IInstanceReferenceOperation instance when
                    instance.ReferenceKind == InstanceReferenceKind.ContainingTypeInstance:
                    return ReceiverPath.This;
                case IFieldReferenceOperation field:
                    if (field.Field.IsVolatile)
                        return null;
                    if (field.Field.IsStatic)
                        return new ReceiverPath(field.Field);
                    return GetPath(field.Instance, state)?.Append(field.Field);
                default:
                    return null;
            }
        }

        private AliasTargetSet GetMayTargets(
            IOperation? operation,
            GuardFlowState state)
        {
            if (operation is null)
                return AliasTargetSet.Unknown();

            if (operation is IFlowCaptureReferenceOperation captureReference &&
                state.Captures.TryGetValue(captureReference.Id, out var captured) &&
                captured.AliasTargets is not null)
            {
                return captured.AliasTargets;
            }

            operation = ExpandCapture(operation, state);
            operation = StripIdentityConversions(operation);
            if (operation.Kind == OperationKind.AddressOf)
            {
                return GetMayTargets(
                    operation.ChildOperations.FirstOrDefault(),
                    state);
            }
            if (operation is IConditionalOperation conditional)
            {
                return GetMayTargets(conditional.WhenTrue, state)
                    .Union(GetMayTargets(conditional.WhenFalse, state));
            }
            if (operation is ILocalReferenceOperation local &&
                state.Aliases.TryGetValue(local.Local, out var alias))
            {
                return alias;
            }
            if (operation is ILocalReferenceOperation refLocal &&
                refLocal.Local.RefKind != RefKind.None)
            {
                return AliasTargetSet.Unknown();
            }

            var path = GetPath(operation, state);
            return path is null
                ? AliasTargetSet.Unknown()
                : AliasTargetSet.Known(path);
        }

        private IOperation ExpandCapture(IOperation operation, GuardFlowState state)
        {
            var seen = new HashSet<CaptureId>();
            while (operation is IFlowCaptureReferenceOperation capture &&
                   state.Captures.TryGetValue(capture.Id, out var value) &&
                   seen.Add(capture.Id))
            {
                operation = value.Value;
            }
            return operation;
        }

        private EffectSummary? GetDelegateEffects(
            IOperation operation,
            GuardFlowState state)
        {
            operation = StripIdentityConversions(operation);
            if (operation is ILocalReferenceOperation local &&
                state.DelegateSummaries.TryGetValue(local.Local, out var summary))
            {
                return summary;
            }
            if (operation is ILocalReferenceOperation localWrite &&
                state.DelegateWrites.TryGetValue(localWrite.Local, out var writes))
            {
                return new EffectSummary(immediateWrites: writes);
            }
            return GetFunctionEffects(operation, state);
        }

        private bool IsUnknownDelegate(IOperation? operation, GuardFlowState state)
        {
            if (operation is null)
                return false;
            operation = ExpandCapture(operation, state);
            return operation is ILocalReferenceOperation local &&
                state.UnknownDelegates.Contains(local.Local);
        }

        private EffectSummary? TryGetAnonymousFunctionEffects(
            IOperation operation,
            GuardFlowState state)
        {
            operation = StripIdentityConversions(operation);
            return GetFunctionEffects(operation, state);
        }

        private EffectSummary? GetFunctionEffects(
            IOperation operation,
            GuardFlowState state,
            ControlFlowGraph? scope = null)
        {
            IBlockOperation? body = null;
            ControlFlowGraph? flowGraph = null;
            foreach (var nested in operation.DescendantsAndSelf())
            {
                _context.CancellationToken.ThrowIfCancellationRequested();
                if (nested is IFlowAnonymousFunctionOperation flowAnonymous)
                {
                    flowGraph = (scope ?? _cfg).GetAnonymousFunctionControlFlowGraphInScope(
                        flowAnonymous,
                        _context.CancellationToken);
                    break;
                }
                if (nested is IAnonymousFunctionOperation anonymous)
                {
                    body = anonymous.Body;
                    break;
                }
            }

            if (body is null && flowGraph is null)
                return null;

            var effects = new EffectAccumulator();
            if (flowGraph is null)
            {
                CollectFunctionEffect(body!, state, effects, flowGraph);
            }
            else
            {
                foreach (var block in flowGraph.Blocks)
                {
                    _context.CancellationToken.ThrowIfCancellationRequested();
                    foreach (var nested in block.Operations)
                        CollectFunctionEffect(nested, state, effects, flowGraph);
                    if (block.BranchValue is not null)
                        CollectFunctionEffect(block.BranchValue, state, effects, flowGraph);
                }
            }
            return effects.Build();
        }

        private EffectSummary GetLocalFunctionEffects(
            IMethodSymbol method,
            GuardFlowState state)
        {
            if (method.MethodKind != MethodKind.LocalFunction)
                return new EffectSummary();
            EnsureLocalEffectSummaries(state);
            return _localEffectSummaries.TryGetValue(
                NormalizeLocalMethod(method),
                out var summary)
                ? summary
                : new EffectSummary();
        }

        private void EnsureLocalEffectSummaries(GuardFlowState state)
        {
            if (_localEffectsBuilt)
                return;

            _localEffectsBuilt = true;
            var declarations = new Dictionary<IMethodSymbol, ILocalFunctionOperation>(
                SymbolEqualityComparer.Default);
            foreach (var local in _operationBlock.DescendantsAndSelf()
                         .OfType<ILocalFunctionOperation>())
            {
                declarations[NormalizeLocalMethod(local.Symbol)] = local;
            }
            if (declarations.Count == 0)
                return;

            var direct = new Dictionary<IMethodSymbol, EffectSummary>(
                SymbolEqualityComparer.Default);
            var calls = new Dictionary<IMethodSymbol, ImmutableHashSet<IMethodSymbol>>(
                SymbolEqualityComparer.Default);
            foreach (var pair in declarations)
            {
                _context.CancellationToken.ThrowIfCancellationRequested();
                var effects = new EffectAccumulator();
                if (pair.Value.Body is not null)
                    CollectFunctionEffect(pair.Value.Body, state, effects);
                direct[pair.Key] = effects.Build();
                var callees = ImmutableHashSet.CreateBuilder<IMethodSymbol>(
                    SymbolEqualityComparer.Default);
                foreach (var callee in effects.Callees)
                {
                    var normalized = NormalizeLocalMethod(callee);
                    if (declarations.ContainsKey(normalized))
                        callees.Add(normalized);
                }
                calls[pair.Key] = callees.ToImmutable();
            }

            var summaries = new Dictionary<IMethodSymbol, EffectSummary>(
                direct,
                SymbolEqualityComparer.Default);
            var changed = true;
            while (changed)
            {
                _context.CancellationToken.ThrowIfCancellationRequested();
                changed = false;
                foreach (var pair in direct)
                {
                    var combined = pair.Value;
                    if (calls.TryGetValue(pair.Key, out var callees))
                    {
                        foreach (var callee in callees)
                        {
                            _context.CancellationToken.ThrowIfCancellationRequested();
                            if (summaries.TryGetValue(callee, out var calleeSummary))
                                combined = combined.Union(calleeSummary);
                        }
                    }
                    if (!summaries.TryGetValue(pair.Key, out var previous) ||
                        !SameEffectSummary(previous, combined))
                    {
                        summaries[pair.Key] = combined;
                        changed = true;
                    }
                }
            }

            foreach (var pair in summaries)
                _localEffectSummaries[pair.Key] = pair.Value;
        }

        private static IMethodSymbol NormalizeLocalMethod(IMethodSymbol method)
            => method.OriginalDefinition;

        private void CollectFunctionEffect(
            IOperation operation,
            GuardFlowState state,
            EffectAccumulator effects,
            ControlFlowGraph? scope = null)
        {
            _context.CancellationToken.ThrowIfCancellationRequested();
            if (operation is IAnonymousFunctionOperation or
                IFlowAnonymousFunctionOperation or
                ILocalFunctionOperation)
            {
                // A declaration is data until it is invoked or assigned to an
                // escaping callback slot. Its body is scanned by the relevant
                // summary, never as an unconditional child operation.
                return;
            }

            switch (operation)
            {
                case ISimpleAssignmentOperation assignment:
                    var pointerBinding = assignment.Target is ILocalReferenceOperation pointerLocal &&
                        pointerLocal.Local.Type is IPointerTypeSymbol &&
                        assignment.Value.Kind == OperationKind.AddressOf;
                    var pointerWrite = !pointerBinding &&
                        HasPointerOperand(assignment.Target);
                    if (!assignment.IsRef && !pointerBinding &&
                        !pointerWrite &&
                        !IsPointerIndirectionTarget(assignment.Target))
                        CollectAssignmentTargetEffect(assignment.Target, state, effects);
                    if (pointerWrite || IsPointerIndirectionTarget(assignment.Target))
                    {
                        effects.MayWriteExposed = true;
                        AddTargets(
                            effects,
                            GetEffectTargets(
                                FindPointerOperand(assignment.Target),
                                state,
                                effects.Aliases));
                    }
                    if ((assignment.IsRef || pointerBinding) &&
                        assignment.Target is ILocalReferenceOperation refTarget)
                    {
                        var targets = GetEffectTargets(
                            assignment.Value,
                            state,
                            effects.Aliases);
                        effects.Aliases[refTarget.Local] =
                            effects.Aliases.TryGetValue(refTarget.Local, out var existing)
                                ? existing.Union(targets)
                                : targets;
                    }
                    else if (assignment.Target is ILocalReferenceOperation localTarget &&
                             localTarget.Local.Type?.TypeKind == TypeKind.Delegate &&
                             GetFunctionEffects(assignment.Value, state, scope) is { } localDelegate)
                    {
                        effects.DelegateSummaries[localTarget.Local] = localDelegate;
                    }
                    else if (assignment.Target is not ILocalReferenceOperation &&
                             GetFunctionEffects(assignment.Value, state, scope) is { } escapingDelegate)
                    {
                        AddEscapedCallback(effects, escapingDelegate);
                    }
                    break;
                case IDeconstructionAssignmentOperation deconstruction:
                    CollectAssignmentTargetEffect(deconstruction.Target, state, effects);
                    if (TryGetDeconstructMethod(deconstruction) is { } deconstruct &&
                        !IsGeneratedPureDeconstruct(deconstruct, deconstruction.Value))
                    {
                        effects.MayInvokeEscapedCallbacks = true;
                        effects.MayWriteShared = true;
                    }
                    break;
                case IReturnOperation returnOperation:
                    if (returnOperation.ChildOperations.FirstOrDefault() is { } returnedValue &&
                        GetFunctionEffects(returnedValue, state, scope) is { } returnedDelegate)
                    {
                        AddImmediateCallback(effects, returnedDelegate);
                    }
                    break;
                case ICompoundAssignmentOperation compound:
                    CollectAssignmentTargetEffect(compound.Target, state, effects);
                    break;
                case IIncrementOrDecrementOperation increment:
                    CollectAssignmentTargetEffect(increment.Target, state, effects);
                    break;
                case IInvocationOperation invocation:
                    if (invocation.TargetMethod.MethodKind == MethodKind.LocalFunction)
                    {
                        effects.Callees.Add(NormalizeLocalMethod(invocation.TargetMethod));
                    }
                    else if (invocation.Instance is ILocalReferenceOperation delegateLocal &&
                             effects.DelegateSummaries.TryGetValue(delegateLocal.Local, out var delegateSummary))
                    {
                        AddImmediateCallback(effects, delegateSummary);
                    }
                    else if (!HasVariantAttribute(invocation.TargetMethod))
                    {
                        effects.MayInvokeEscapedCallbacks = true;
                        effects.MayWriteShared = true;
                    }
                    if (invocation.Instance is not null &&
                        invocation.Instance.Type?.IsValueType == true &&
                        !invocation.TargetMethod.IsReadOnly)
                    {
                        AddTargets(effects, GetEffectTargets(
                            invocation.Instance,
                            state,
                            effects.Aliases));
                    }
                    foreach (var argument in invocation.Arguments)
                    {
                        if (argument.Parameter?.RefKind is RefKind.Ref or RefKind.Out)
                        {
                            AddTargets(effects, GetEffectTargets(
                                argument.Value,
                                state,
                                effects.Aliases));
                        }
                        if (GetFunctionEffects(argument.Value, state, scope) is { } callback)
                            AddEscapedCallback(effects, callback);
                        if (argument.Value.Type is IPointerTypeSymbol)
                            effects.MayWriteExposed = true;
                    }
                    break;
                case IFunctionPointerInvocationOperation functionPointer:
                    effects.MayInvokeEscapedCallbacks = true;
                    effects.MayWriteShared = true;
                    foreach (var argument in functionPointer.Arguments)
                    {
                        if (argument.Parameter?.RefKind is RefKind.Ref or RefKind.Out)
                        {
                            AddTargets(effects, GetEffectTargets(
                                argument.Value,
                                state,
                                effects.Aliases));
                        }
                        if (argument.Value.Type is IPointerTypeSymbol)
                            effects.MayWriteExposed = true;
                    }
                    break;
                case IObjectCreationOperation creation:
                    if (creation.Constructor is null || !HasVariantAttribute(creation.Constructor))
                    {
                        effects.MayInvokeEscapedCallbacks = true;
                        effects.MayWriteShared = true;
                    }
                    foreach (var argument in creation.Arguments)
                    {
                        if (argument.Parameter?.RefKind is RefKind.Ref or RefKind.Out)
                        {
                            AddTargets(effects, GetEffectTargets(
                                argument.Value,
                                state,
                                effects.Aliases));
                        }
                        if (GetFunctionEffects(argument.Value, state, scope) is { } callback)
                            AddEscapedCallback(effects, callback);
                        if (argument.Value.Type is IPointerTypeSymbol)
                            effects.MayWriteExposed = true;
                    }
                    break;
                case IConversionOperation conversion when conversion.Conversion.IsUserDefined:
                    effects.MayInvokeEscapedCallbacks = true;
                    effects.MayWriteShared = true;
                    AddUserOperatorOperandEffects(
                        effects,
                        new[] { conversion.Operand },
                        state,
                        scope);
                    break;
                case IBinaryOperation binary when binary.OperatorMethod is not null:
                    effects.MayInvokeEscapedCallbacks = true;
                    effects.MayWriteShared = true;
                    AddUserOperatorOperandEffects(
                        effects,
                        new[] { binary.LeftOperand, binary.RightOperand },
                        state,
                        scope);
                    break;
                case IUnaryOperation unary when unary.OperatorMethod is not null:
                    effects.MayInvokeEscapedCallbacks = true;
                    effects.MayWriteShared = true;
                    AddUserOperatorOperandEffects(
                        effects,
                        new[] { unary.Operand },
                        state,
                        scope);
                    break;
                case IDynamicInvocationOperation dynamicInvocation:
                    effects.MayInvokeEscapedCallbacks = true;
                    effects.MayWriteShared = true;
                    effects.MayWriteExposed = true;
                    foreach (var argument in dynamicInvocation.DescendantsAndSelf()
                                 .OfType<IArgumentOperation>())
                    {
                        AddTargets(effects, GetEffectTargets(
                            argument.Value,
                            state,
                            effects.Aliases));
                    }
                    if (dynamicInvocation.Syntax is not null)
                    {
                        var model = _context.Compilation.GetSemanticModel(
                            dynamicInvocation.Syntax.SyntaxTree);
                        foreach (var syntax in dynamicInvocation.Syntax
                                     .DescendantNodesAndSelf()
                                     .OfType<ArgumentSyntax>())
                        {
                            _context.CancellationToken.ThrowIfCancellationRequested();
                            if (syntax.RefKindKeyword.RawKind is not
                                ((int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.RefKeyword or
                                 (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.OutKeyword or
                                 (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.InKeyword))
                                continue;
                            var value = model.GetOperation(
                                syntax.Expression,
                                _context.CancellationToken);
                            AddTargets(effects, GetEffectTargets(
                                value,
                                state,
                                effects.Aliases));
                        }
                    }
                    break;
                case IPropertyReferenceOperation property
                    when !IsGeneratedPureProperty(property.Property, property.Instance):
                    effects.MayInvokeEscapedCallbacks = true;
                    effects.MayWriteShared = true;
                    if (property.Instance is not null &&
                        property.Instance.Type?.IsValueType == true &&
                        property.Property.GetMethod?.IsReadOnly != true)
                    {
                        AddTargets(effects, GetEffectTargets(
                            property.Instance,
                            state,
                            effects.Aliases));
                    }
                    break;
                case IEventAssignmentOperation eventAssignment:
                    effects.MayInvokeEscapedCallbacks = true;
                    effects.MayWriteShared = true;
                    if (GetFunctionEffects(eventAssignment.HandlerValue, state, scope) is { } eventCallback)
                        AddEscapedCallback(effects, eventCallback);
                    var eventInstance = eventAssignment.EventReference.ChildOperations.FirstOrDefault();
                    if (eventInstance is not null)
                        AddTargets(effects, GetEffectTargets(
                            eventInstance,
                            state,
                            effects.Aliases));
                    break;
                case IIsPatternOperation patternOperation:
                    if (patternOperation.Pattern is IRecursivePatternOperation recursive &&
                        recursive.DeconstructSymbol is IMethodSymbol patternDeconstruct &&
                        !IsGeneratedPureDeconstruct(patternDeconstruct, patternOperation.Value))
                    {
                        if (patternOperation.Value.Type?.IsValueType == true)
                            AddTargets(effects, GetEffectTargets(
                                patternOperation.Value,
                                state,
                                effects.Aliases));
                        effects.MayInvokeEscapedCallbacks = true;
                        effects.MayWriteShared = true;
                    }
                    break;
            }

            foreach (var child in operation.ChildOperations)
                CollectFunctionEffect(child, state, effects, scope);
        }

        private void CollectAssignmentTargetEffect(
            IOperation target,
            GuardFlowState state,
            EffectAccumulator effects)
        {
            if (IsRefStorageWriteTarget(target))
            {
                effects.MayWriteExposed = true;
                return;
            }
            if (IsPointerIndirectionTarget(target))
            {
                effects.MayWriteExposed = true;
                var pointer = target.ChildOperations.FirstOrDefault(
                    child => child.Type is IPointerTypeSymbol);
                AddTargets(
                    effects,
                    pointer is null
                        ? AliasTargetSet.Unknown()
                        : GetEffectTargets(pointer, state, effects.Aliases));
                return;
            }
            if (target is IPropertyReferenceOperation property &&
                property.Instance is not null &&
                property.Instance.Type?.IsValueType == true)
            {
                AddTargets(effects, GetEffectTargets(
                    property.Instance,
                    state,
                    effects.Aliases));
                return;
            }
            if (target is ILocalReferenceOperation or
                IParameterReferenceOperation or
                IFieldReferenceOperation)
            {
                AddTargets(effects, GetEffectTargets(target, state, effects.Aliases));
                return;
            }
            foreach (var child in target.ChildOperations)
                CollectAssignmentTargetEffect(child, state, effects);
        }

        private AliasTargetSet GetEffectTargets(
            IOperation? operation,
            GuardFlowState state,
            Dictionary<ISymbol, AliasTargetSet> aliases)
        {
            if (operation is null)
                return AliasTargetSet.Unknown();
            operation = StripIdentityConversions(operation);
            if (operation.Kind == OperationKind.AddressOf)
            {
                return GetEffectTargets(
                    operation.ChildOperations.FirstOrDefault(),
                    state,
                    aliases);
            }
            if (operation is ILocalReferenceOperation local &&
                aliases.TryGetValue(local.Local, out var alias))
            {
                return alias;
            }
            if (operation is ILocalReferenceOperation capturedLocal &&
                state.Aliases.TryGetValue(capturedLocal.Local, out var capturedAlias))
            {
                return capturedAlias;
            }
            if (operation is ILocalReferenceOperation refLocal &&
                refLocal.Local.RefKind != RefKind.None)
            {
                return AliasTargetSet.Unknown();
            }
            if (operation is IConditionalOperation conditional)
            {
                return GetEffectTargets(conditional.WhenTrue, state, aliases)
                    .Union(GetEffectTargets(conditional.WhenFalse, state, aliases));
            }
            var path = GetPath(operation, state);
            return path is null
                ? AliasTargetSet.Unknown()
                : AliasTargetSet.Known(path);
        }

        private static bool IsPointerIndirectionTarget(IOperation target)
            => target.Syntax is PrefixUnaryExpressionSyntax prefix &&
                   prefix.RawKind == (int)Microsoft.CodeAnalysis.CSharp.SyntaxKind.PointerIndirectionExpression ||
               target.Syntax is ElementAccessExpressionSyntax &&
                   target.ChildOperations.Any(child => child.Type is IPointerTypeSymbol);

        private static bool HasPointerOperand(IOperation operation)
            => operation.Type is IPointerTypeSymbol ||
               operation.ChildOperations.Any(HasPointerOperand);

        private static IOperation? FindPointerOperand(IOperation operation)
        {
            if (operation.Type is IPointerTypeSymbol)
                return operation;
            foreach (var child in operation.ChildOperations)
            {
                if (FindPointerOperand(child) is { } pointer)
                    return pointer;
            }
            return null;
        }

        private static void AddTargets(
            EffectAccumulator effects,
            AliasTargetSet targets)
        {
            if (targets.IsUnknown)
            {
                effects.MayWriteExposed = true;
                return;
            }
            effects.ImmediateWrites.UnionWith(targets.Paths);
        }

        private static void AddEscapedCallback(
            EffectAccumulator effects,
            EffectSummary summary)
        {
            effects.EscapedCallbackWrites.UnionWith(summary.ImmediateWrites);
            effects.EscapedCallbackWrites.UnionWith(summary.EscapedCallbackWrites);
            effects.MayWriteExposed |= summary.MayWriteExposed;
            effects.MayInvokeEscapedCallbacks |= summary.MayInvokeEscapedCallbacks;
        }

        private static void AddImmediateCallback(
            EffectAccumulator effects,
            EffectSummary summary)
        {
            effects.ImmediateWrites.UnionWith(summary.ImmediateWrites);
            effects.ImmediateWrites.UnionWith(summary.EscapedCallbackWrites);
            effects.MayInvokeEscapedCallbacks |= summary.MayInvokeEscapedCallbacks;
            effects.MayWriteShared |= summary.MayWriteShared;
            effects.MayWriteExposed |= summary.MayWriteExposed;
        }

        private void AddUserOperatorOperandEffects(
            EffectAccumulator effects,
            IEnumerable<IOperation> operands,
            GuardFlowState state,
            ControlFlowGraph? scope)
        {
            foreach (var operand in operands)
            {
                _context.CancellationToken.ThrowIfCancellationRequested();
                var candidate = StripIdentityConversions(operand);
                if (candidate is ILocalReferenceOperation local &&
                    effects.DelegateSummaries.TryGetValue(local.Local, out var localSummary))
                {
                    AddEscapedCallback(effects, localSummary);
                }
                else if (GetFunctionEffects(operand, state, scope) is { } callback)
                {
                    AddEscapedCallback(effects, callback);
                }
                if (operand.Type is IPointerTypeSymbol)
                    effects.MayWriteExposed = true;
            }
        }

        private bool IsGeneratedPureProperty(IPropertySymbol property, IOperation? instance)
        {
            if (property.Name == "kind" && property.ContainingType is { } kindType &&
                UnionTypeInfo.TryCreate(kindType, _duAttribute) is { IsStructUnion: true })
            {
                return true;
            }
            if (property.ContainingType is { } containingType &&
                UnionTypeInfo.TryCreate(containingType, _duAttribute) is { } info &&
                info.KindEnumType?.ContainingType is { } unionType &&
                GeneratedVariantPredicate.TryGetGeneratedProperty(
                    property,
                    unionType,
                    info.VariantNames,
                    out _))
            {
                return true;
            }
            return instance is IPropertyReferenceOperation accessor &&
                TryGetAccessInfo(accessor, _duAttribute, out _, _accessCache);
        }

        private IMethodSymbol? TryGetDeconstructMethod(
            IDeconstructionAssignmentOperation deconstruction)
        {
            if (deconstruction.Syntax is AssignmentExpressionSyntax assignmentSyntax)
            {
                var semanticModel = _context.Compilation.GetSemanticModel(
                    assignmentSyntax.SyntaxTree);
                var info = Microsoft.CodeAnalysis.CSharp.CSharpExtensions.GetDeconstructionInfo(
                    semanticModel,
                    assignmentSyntax);
                if (info.Method is not null)
                    return info.Method;
            }

            if (deconstruction.Value.Type is INamedTypeSymbol namedType)
            {
                return namedType.GetMembers("Deconstruct")
                    .OfType<IMethodSymbol>()
                    .FirstOrDefault(method => method.Parameters.Length > 0 &&
                        method.Parameters.All(parameter =>
                            parameter.RefKind == RefKind.Out));
            }

            return null;
        }

        private bool IsGeneratedPureDeconstruct(
            IMethodSymbol method,
            IOperation? receiver)
        {
            // Generated payload deconstructors are projections over immutable
            // payload fields. The surrounding generated accessor is the stable
            // marker; user-defined Deconstruct methods on arbitrary structs are
            // opaque and may mutate shared state.
            return receiver is IPropertyReferenceOperation property &&
                IsPureGeneratedProjection(property);
        }

        private bool IsPureGeneratedProjection(IPropertyReferenceOperation property)
            => TryGetAccessInfo(property, _duAttribute, out _, _accessCache) ||
               IsGeneratedPureProperty(property.Property, property.Instance) ||
               property.Instance is IPropertyReferenceOperation accessor &&
               TryGetAccessInfo(accessor, _duAttribute, out _, _accessCache);

        private static bool IsIdentityConversion(IConversionOperation conversion)
            => conversion.Conversion.IsIdentity && !conversion.Conversion.IsUserDefined;

        private static IOperation StripIdentityConversions(IOperation operation)
        {
            while (operation is IConversionOperation conversion && IsIdentityConversion(conversion))
                operation = conversion.Operand;
            return operation;
        }

        private static bool TryGetBooleanConstant(IOperation operation, out bool value)
        {
            operation = StripIdentityConversions(operation);
            if (operation.ConstantValue is { HasValue: true, Value: bool constant })
            {
                value = constant;
                return true;
            }
            value = false;
            return false;
        }

        private static bool TryGetFactoryVariant(
            IOperation? instance,
            UnionTypeInfo info,
            out string variant)
        {
            instance = instance is null ? null : StripIdentityConversions(instance);
            if (instance is IInvocationOperation invocation &&
                invocation.TargetMethod.IsStatic &&
                HasVariantAttribute(invocation.TargetMethod) &&
                info.VariantNames.Contains(invocation.TargetMethod.Name))
            {
                variant = invocation.TargetMethod.Name;
                return true;
            }
            variant = string.Empty;
            return false;
        }

        private static bool HasVariantAttribute(IMethodSymbol method)
            => method.GetAttributes().Any(attribute =>
                attribute.AttributeClass?.Name == "VariantAttribute");

        private static bool IsInsideNameOf(IOperation operation)
        {
            for (var current = operation.Parent; current is not null; current = current.Parent)
            {
                if (current.Kind == OperationKind.NameOf)
                    return true;
                if (current is IMethodBodyOperation or IAnonymousFunctionOperation)
                    break;
            }
            return false;
        }

        private static bool IsUnrestrictedPattern(IPatternOperation pattern)
            => pattern is IDiscardPatternOperation ||
               pattern is IDeclarationPatternOperation { Syntax: VarPatternSyntax };

        private static bool IsUnrestrictedEnumPattern(IPatternOperation pattern)
        {
            if (pattern is IConstantPatternOperation or IDiscardPatternOperation)
                return true;
            if (pattern is IBinaryPatternOperation binary)
            {
                return IsUnrestrictedEnumPattern(binary.LeftPattern) &&
                    IsUnrestrictedEnumPattern(binary.RightPattern);
            }
            if (pattern is INegatedPatternOperation negated)
                return IsUnrestrictedEnumPattern(negated.Pattern);
            return false;
        }

        private static bool IsUnrestrictedPayloadPattern(IPatternOperation pattern)
        {
            if (IsUnrestrictedPattern(pattern))
                return true;
            if (pattern is not IRecursivePatternOperation recursive ||
                recursive.DeconstructionSubpatterns.IsEmpty ||
                recursive.InputType is not { IsValueType: true } inputType ||
                inputType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                return false;
            }
            foreach (var subpattern in recursive.DeconstructionSubpatterns)
            {
                if (subpattern is IDiscardPatternOperation)
                    continue;
                if (subpattern is IDeclarationPatternOperation declaration && declaration.MatchedType is null)
                    continue;
                return false;
            }
            return true;
        }

        private sealed class PatternContext
        {
            public INamedTypeSymbol UnionType { get; }
            public ImmutableHashSet<string> Variants { get; }
            public bool UnsafeForSource { get; private set; }
            public ReceiverPath? SourcePath { get; }

            public PatternContext(
                INamedTypeSymbol unionType,
                ImmutableHashSet<string> variants,
                bool unsafeForSource,
                ReceiverPath? sourcePath)
            {
                UnionType = unionType;
                Variants = variants;
                UnsafeForSource = unsafeForSource;
                SourcePath = sourcePath;
            }

            public void InvalidateSource(GuardFlowState state)
            {
                UnsafeForSource = true;
                state.Invalidate(SourcePath);
            }
        }

        private sealed class PatternFactData
        {
            public INamedTypeSymbol UnionType { get; }
            public ReceiverPath? Path { get; set; }
            public ImmutableHashSet<string> TrueSet { get; }
            public ImmutableHashSet<string> FalseSet { get; }
            public bool FalseKnown { get; }
            public bool UnsafeForSource { get; }

            public PatternFactData(
                INamedTypeSymbol unionType,
                ReceiverPath? path,
                IEnumerable<string> trueSet,
                IEnumerable<string>? falseSet,
                bool falseKnown,
                bool unsafeForSource)
            {
                UnionType = unionType;
                Path = path;
                TrueSet = trueSet.ToImmutableHashSet(StringComparer.Ordinal);
                FalseSet = falseSet is null
                    ? ImmutableHashSet<string>.Empty
                    : falseSet.ToImmutableHashSet(StringComparer.Ordinal);
                FalseKnown = falseKnown;
                UnsafeForSource = unsafeForSource;
            }

            public PredicateFact ToPredicateFact()
                => new(Path, TrueSet, TrueSet.Count > 0, FalseSet, FalseKnown, UnsafeForSource);

            public static PatternFactData CombineOr(
                PatternFactData left,
                PatternFactData right,
                INamedTypeSymbol unionType)
                => new(
                    unionType,
                    left.Path,
                    left.TrueSet.Union(right.TrueSet),
                    left.FalseKnown && right.FalseKnown
                        ? left.FalseSet.Intersect(right.FalseSet)
                        : null,
                    left.FalseKnown && right.FalseKnown,
                    left.UnsafeForSource || right.UnsafeForSource);

            public static PatternFactData CombineAnd(
                PatternFactData left,
                PatternFactData right,
                INamedTypeSymbol unionType)
                => new(
                    unionType,
                    left.Path,
                    left.TrueSet.Intersect(right.TrueSet),
                    null,
                    false,
                    left.UnsafeForSource || right.UnsafeForSource);
        }
    }
}
