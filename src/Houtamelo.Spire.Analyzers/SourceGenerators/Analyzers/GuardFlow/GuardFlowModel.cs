using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers.GuardFlow;

/// A stable storage path used as the identity of a union receiver.
internal sealed class ReceiverPath : IEquatable<ReceiverPath>
{
    public static readonly ReceiverPath This = new(true, ImmutableArray<ISymbol>.Empty);

    public ImmutableArray<ISymbol> Segments { get; }
    public bool IsThis { get; }
    public bool IsExternalStorage =>
        Segments.Length > 0 &&
        Segments[0] is IParameterSymbol { RefKind: not RefKind.None };

    public bool IsSharedStorage =>
        IsThis || IsExternalStorage || Segments.Any(s => s.Kind == SymbolKind.Field);

    public ReceiverPath(ISymbol symbol)
        : this(false, ImmutableArray.Create(symbol))
    {
    }

    private ReceiverPath(bool isThis, ImmutableArray<ISymbol> segments)
    {
        IsThis = isThis;
        Segments = segments;
    }

    public ReceiverPath Append(ISymbol symbol)
    {
        var builder = ImmutableArray.CreateBuilder<ISymbol>(Segments.Length + 1);
        builder.AddRange(Segments);
        builder.Add(symbol);
        return new ReceiverPath(IsThis, builder.ToImmutable());
    }

    public bool StartsWith(ReceiverPath prefix)
    {
        if (IsThis != prefix.IsThis || prefix.Segments.Length > Segments.Length)
            return false;

        for (var i = 0; i < prefix.Segments.Length; i++)
        {
            if (!SymbolEqualityComparer.Default.Equals(Segments[i], prefix.Segments[i]))
                return false;
        }

        return true;
    }

    public bool EndsWith(ISymbol symbol)
        => Segments.Length > 0 &&
           SymbolEqualityComparer.Default.Equals(Segments[Segments.Length - 1], symbol);

    public bool Equals(ReceiverPath? other)
    {
        if (ReferenceEquals(this, other))
            return true;
        if (other is null || IsThis != other.IsThis || Segments.Length != other.Segments.Length)
            return false;

        for (var i = 0; i < Segments.Length; i++)
        {
            if (!SymbolEqualityComparer.Default.Equals(Segments[i], other.Segments[i]))
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as ReceiverPath);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = IsThis ? 17 : 31;
            foreach (var segment in Segments)
                hash = (hash * 397) ^ SymbolEqualityComparer.Default.GetHashCode(segment);
            return hash;
        }
    }

    public override string ToString()
    {
        if (IsThis)
            return "this";
        return string.Join(".", Segments.Select(s => s.Name));
    }
}

/// Possible storage locations for a ref local or externally addressable
/// parameter. Unknown means that a write may reach any exposed storage.
internal sealed class AliasTargetSet
{
    public ImmutableHashSet<ReceiverPath> Paths { get; }
    public bool IsUnknown { get; }

    private AliasTargetSet(ImmutableHashSet<ReceiverPath> paths, bool isUnknown)
    {
        Paths = paths;
        IsUnknown = isUnknown;
    }

    public static AliasTargetSet Known(ReceiverPath path)
        => new(ImmutableHashSet.Create(path), false);

    public static AliasTargetSet Known(IEnumerable<ReceiverPath> paths)
        => new(paths.ToImmutableHashSet(), false);

    public static AliasTargetSet Unknown()
        => new(ImmutableHashSet<ReceiverPath>.Empty, true);

    public static AliasTargetSet Unknown(IEnumerable<ReceiverPath> paths)
        => new(paths.ToImmutableHashSet(), true);

    public AliasTargetSet Union(AliasTargetSet other)
        => new(Paths.Union(other.Paths), IsUnknown || other.IsUnknown);

    public bool IsSingle(out ReceiverPath path)
    {
        if (!IsUnknown && Paths.Count == 1)
        {
            path = Paths.First();
            return true;
        }

        path = null!;
        return false;
    }

    public bool Equals(AliasTargetSet? other)
        => other is not null && IsUnknown == other.IsUnknown && Paths.SetEquals(other.Paths);
}

internal sealed class EffectSummary
{
    public ImmutableHashSet<ReceiverPath> ImmediateWrites { get; }
    public ImmutableHashSet<ReceiverPath> EscapedCallbackWrites { get; }
    public bool MayInvokeEscapedCallbacks { get; }
    public bool MayWriteShared { get; }
    public bool MayWriteExposed { get; }

    public EffectSummary(
        IEnumerable<ReceiverPath>? immediateWrites = null,
        IEnumerable<ReceiverPath>? escapedCallbackWrites = null,
        bool mayInvokeEscapedCallbacks = false,
        bool mayWriteShared = false,
        bool mayWriteExposed = false)
    {
        ImmediateWrites = (immediateWrites ?? Enumerable.Empty<ReceiverPath>()).ToImmutableHashSet();
        EscapedCallbackWrites = (escapedCallbackWrites ?? Enumerable.Empty<ReceiverPath>()).ToImmutableHashSet();
        MayInvokeEscapedCallbacks = mayInvokeEscapedCallbacks;
        MayWriteShared = mayWriteShared;
        MayWriteExposed = mayWriteExposed;
    }

    public EffectSummary Union(EffectSummary other)
        => new(
            ImmediateWrites.Union(other.ImmediateWrites),
            EscapedCallbackWrites.Union(other.EscapedCallbackWrites),
            MayInvokeEscapedCallbacks || other.MayInvokeEscapedCallbacks,
            MayWriteShared || other.MayWriteShared,
            MayWriteExposed || other.MayWriteExposed);
}

internal sealed class PredicateFact
{
    public ReceiverPath? Path { get; }
    public ImmutableHashSet<string> TrueSet { get; }
    public ImmutableHashSet<string> FalseSet { get; }
    public bool TrueKnown { get; }
    public bool FalseKnown { get; }
    public bool TrueReachable { get; }
    public bool FalseReachable { get; }
    public bool UnsafeForSource { get; }
    public ImmutableDictionary<ReceiverPath, PredicateConstraint> Constraints { get; }

    public PredicateFact(
        ReceiverPath? path,
        IEnumerable<string>? trueSet,
        bool trueKnown,
        IEnumerable<string>? falseSet,
        bool falseKnown,
        bool unsafeForSource = false,
        bool trueReachable = true,
        bool falseReachable = true)
    {
        Path = path;
        TrueSet = trueSet is null
            ? ImmutableHashSet<string>.Empty
            : trueSet.ToImmutableHashSet(StringComparer.Ordinal);
        FalseSet = falseSet is null
            ? ImmutableHashSet<string>.Empty
            : falseSet.ToImmutableHashSet(StringComparer.Ordinal);
        TrueKnown = trueKnown;
        FalseKnown = falseKnown;
        TrueReachable = trueReachable;
        FalseReachable = falseReachable;
        UnsafeForSource = unsafeForSource;
        Constraints = path is null
            ? ImmutableDictionary<ReceiverPath, PredicateConstraint>.Empty
            : ImmutableDictionary<ReceiverPath, PredicateConstraint>.Empty.Add(
                path,
                new PredicateConstraint(TrueSet, trueKnown, FalseSet, falseKnown));
    }

    private PredicateFact(
        IEnumerable<KeyValuePair<ReceiverPath, PredicateConstraint>> constraints,
        bool unsafeForSource,
        bool trueReachable,
        bool falseReachable)
    {
        Constraints = constraints.ToImmutableDictionary(
            pair => pair.Key,
            pair => pair.Value);
        UnsafeForSource = unsafeForSource;
        TrueReachable = trueReachable;
        FalseReachable = falseReachable;
        if (Constraints.Count == 1)
        {
            var pair = Constraints.First();
            Path = pair.Key;
            TrueSet = pair.Value.TrueSet;
            FalseSet = pair.Value.FalseSet;
            TrueKnown = pair.Value.TrueKnown;
            FalseKnown = pair.Value.FalseKnown;
        }
        else
        {
            Path = null;
            TrueSet = ImmutableHashSet<string>.Empty;
            FalseSet = ImmutableHashSet<string>.Empty;
            TrueKnown = false;
            FalseKnown = false;
        }
    }

    public static PredicateFact FromConstraints(
        IEnumerable<KeyValuePair<ReceiverPath, PredicateConstraint>> constraints,
        bool unsafeForSource = false,
        bool trueReachable = true,
        bool falseReachable = true)
        => new(constraints, unsafeForSource, trueReachable, falseReachable);

    public static PredicateFact FromProofs(
        IReadOnlyDictionary<ReceiverPath, ImmutableHashSet<string>> proofs)
        => FromConstraints(
            proofs.Select(pair => new KeyValuePair<ReceiverPath, PredicateConstraint>(
                pair.Key,
                new PredicateConstraint(
                    pair.Value,
                    true,
                    pair.Value,
                    true))));

    public IEnumerable<KeyValuePair<ReceiverPath, PredicateConstraint>> EnumerateConstraints()
        => Constraints;

    public bool Affects(ReceiverPath target)
        => Constraints.Keys.Any(path => PathsMayAffect(path, target));

    private static bool PathsMayAffect(ReceiverPath candidate, ReceiverPath target)
    {
        if (target.StartsWith(candidate) || candidate.StartsWith(target))
            return true;
        var terminal = target.Segments.Length == 0
            ? null
            : target.Segments[target.Segments.Length - 1];
        return terminal is not null && candidate.Segments.Any(segment =>
            SymbolEqualityComparer.Default.Equals(segment, terminal));
    }

    public PredicateFact Swap()
        => FromConstraints(
            Constraints.Select(pair => new KeyValuePair<ReceiverPath, PredicateConstraint>(
                pair.Key,
                pair.Value.Swap())),
            UnsafeForSource,
            FalseReachable,
            TrueReachable);

    public PredicateFact WithCurrentProofs(
        IReadOnlyDictionary<ReceiverPath, ImmutableHashSet<string>> proofs)
    {
        var constraints = new Dictionary<ReceiverPath, PredicateConstraint>();
        foreach (var pair in Constraints)
            constraints[pair.Key] = pair.Value;

        foreach (var proof in proofs)
        {
            if (constraints.TryGetValue(proof.Key, out var existing))
            {
                constraints[proof.Key] = new PredicateConstraint(
                    existing.TrueKnown
                        ? existing.TrueSet.Intersect(proof.Value)
                        : existing.TrueSet,
                    existing.TrueKnown,
                    existing.FalseKnown
                        ? existing.FalseSet.Intersect(proof.Value)
                        : existing.FalseSet,
                    existing.FalseKnown);
            }
            else
            {
                constraints[proof.Key] = new PredicateConstraint(
                    proof.Value,
                    true,
                    proof.Value,
                    true);
            }
        }

        var trueReachable = TrueReachable && !constraints.Any(pair =>
            pair.Value.TrueKnown && pair.Value.TrueSet.Count == 0);
        var falseReachable = FalseReachable && !constraints.Any(pair =>
            pair.Value.FalseKnown && pair.Value.FalseSet.Count == 0);
        return FromConstraints(
            constraints,
            UnsafeForSource,
            trueReachable,
            falseReachable);
    }

    public PredicateFact WithReachability(
        bool trueReachable,
        bool falseReachable)
        => FromConstraints(
            Constraints,
            UnsafeForSource,
            trueReachable,
            falseReachable);

    public static PredicateFact MergeAlternatives(
        PredicateFact left,
        PredicateFact right)
    {
        var constraints = new Dictionary<ReceiverPath, PredicateConstraint>();
        var trueReachable = left.TrueReachable || right.TrueReachable;
        var falseReachable = left.FalseReachable || right.FalseReachable;

        foreach (var path in left.Constraints.Keys.Union(right.Constraints.Keys))
        {
            var hasLeft = left.Constraints.TryGetValue(path, out var l);
            var hasRight = right.Constraints.TryGetValue(path, out var r);
            var trueKnown = false;
            var trueSet = ImmutableHashSet<string>.Empty;
            if (trueReachable)
            {
                if (!left.TrueReachable && hasRight && r!.TrueKnown)
                {
                    trueKnown = true;
                    trueSet = r.TrueSet;
                }
                else if (!right.TrueReachable && hasLeft && l!.TrueKnown)
                {
                    trueKnown = true;
                    trueSet = l.TrueSet;
                }
                else if (left.TrueReachable && right.TrueReachable &&
                         hasLeft && hasRight && l!.TrueKnown && r!.TrueKnown)
                {
                    trueKnown = true;
                    trueSet = l.TrueSet.Union(r.TrueSet);
                }
            }
            var falseKnown = false;
            var falseSet = ImmutableHashSet<string>.Empty;
            if (falseReachable)
            {
                if (!left.FalseReachable && hasRight && r!.FalseKnown)
                {
                    falseKnown = true;
                    falseSet = r.FalseSet;
                }
                else if (!right.FalseReachable && hasLeft && l!.FalseKnown)
                {
                    falseKnown = true;
                    falseSet = l.FalseSet;
                }
                else if (left.FalseReachable && right.FalseReachable &&
                         hasLeft && hasRight && l!.FalseKnown && r!.FalseKnown)
                {
                    falseKnown = true;
                    falseSet = l.FalseSet.Union(r.FalseSet);
                }
            }
            if (trueKnown || falseKnown)
            {
                constraints[path] = new PredicateConstraint(
                    trueSet,
                    trueKnown,
                    falseSet,
                    falseKnown);
            }
        }

        return FromConstraints(
            constraints,
            left.UnsafeForSource || right.UnsafeForSource,
            trueReachable,
            falseReachable);
    }
}

internal sealed class PredicateConstraint
{
    public ImmutableHashSet<string> TrueSet { get; }
    public ImmutableHashSet<string> FalseSet { get; }
    public bool TrueKnown { get; }
    public bool FalseKnown { get; }

    public PredicateConstraint(
        IEnumerable<string>? trueSet,
        bool trueKnown,
        IEnumerable<string>? falseSet,
        bool falseKnown)
    {
        TrueSet = trueSet is null
            ? ImmutableHashSet<string>.Empty
            : trueSet.ToImmutableHashSet(StringComparer.Ordinal);
        FalseSet = falseSet is null
            ? ImmutableHashSet<string>.Empty
            : falseSet.ToImmutableHashSet(StringComparer.Ordinal);
        TrueKnown = trueKnown;
        FalseKnown = falseKnown;
    }

    public PredicateConstraint Swap()
        => new(FalseSet, FalseKnown, TrueSet, TrueKnown);
}

internal sealed class CaptureValue
{
    public IOperation Value { get; }
    public ReceiverPath? Path { get; }
    public PredicateFact? Predicate { get; }
    public bool HasUnknownAlternative { get; }
    public AliasTargetSet? AliasTargets { get; }

    public CaptureValue(
        IOperation value,
        ReceiverPath? path,
        PredicateFact? predicate,
        bool hasUnknownAlternative = false,
        AliasTargetSet? aliasTargets = null)
    {
        Value = value;
        Path = path;
        Predicate = predicate;
        HasUnknownAlternative = hasUnknownAlternative;
        AliasTargets = aliasTargets;
    }
}

internal sealed class GuardFlowState
{
    public bool IsReachable { get; private set; }
    public Dictionary<ReceiverPath, ImmutableHashSet<string>> Proofs { get; }
    public Dictionary<ISymbol, AliasTargetSet> Aliases { get; }
    public Dictionary<CaptureId, CaptureValue> Captures { get; }
    public Dictionary<IOperation, PredicateFact> OperationFacts { get; }
    public HashSet<IOperation> EvaluatedConditions { get; }
    public Dictionary<ISymbol, PredicateFact> BooleanFacts { get; }
    public Dictionary<ISymbol, ImmutableHashSet<ReceiverPath>> DelegateWrites { get; }
    public Dictionary<ISymbol, EffectSummary> DelegateSummaries { get; }
    public HashSet<ISymbol> UnknownDelegates { get; }
    public HashSet<ReceiverPath> EscapedCallbackWrites { get; }
    public HashSet<ReceiverPath> WritableCapturePaths { get; }
    public HashSet<ReceiverPath> AddressExposed { get; }

    private GuardFlowState(bool isReachable)
    {
        IsReachable = isReachable;
        Proofs = new Dictionary<ReceiverPath, ImmutableHashSet<string>>();
        Aliases = new Dictionary<ISymbol, AliasTargetSet>(SymbolEqualityComparer.Default);
        Captures = new Dictionary<CaptureId, CaptureValue>();
        OperationFacts = new Dictionary<IOperation, PredicateFact>();
        EvaluatedConditions = new HashSet<IOperation>();
        BooleanFacts = new Dictionary<ISymbol, PredicateFact>(SymbolEqualityComparer.Default);
        DelegateWrites = new Dictionary<ISymbol, ImmutableHashSet<ReceiverPath>>(
            SymbolEqualityComparer.Default);
        DelegateSummaries = new Dictionary<ISymbol, EffectSummary>(SymbolEqualityComparer.Default);
        UnknownDelegates = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        EscapedCallbackWrites = new HashSet<ReceiverPath>();
        WritableCapturePaths = new HashSet<ReceiverPath>();
        AddressExposed = new HashSet<ReceiverPath>();
    }

    private GuardFlowState(GuardFlowState other)
    {
        IsReachable = other.IsReachable;
        Proofs = new Dictionary<ReceiverPath, ImmutableHashSet<string>>(other.Proofs);
        Aliases = new Dictionary<ISymbol, AliasTargetSet>(other.Aliases, SymbolEqualityComparer.Default);
        Captures = new Dictionary<CaptureId, CaptureValue>(other.Captures);
        OperationFacts = new Dictionary<IOperation, PredicateFact>(other.OperationFacts);
        EvaluatedConditions = new HashSet<IOperation>(other.EvaluatedConditions);
        BooleanFacts = new Dictionary<ISymbol, PredicateFact>(other.BooleanFacts, SymbolEqualityComparer.Default);
        DelegateWrites = new Dictionary<ISymbol, ImmutableHashSet<ReceiverPath>>(
            other.DelegateWrites, SymbolEqualityComparer.Default);
        DelegateSummaries = new Dictionary<ISymbol, EffectSummary>(
            other.DelegateSummaries,
            SymbolEqualityComparer.Default);
        UnknownDelegates = new HashSet<ISymbol>(other.UnknownDelegates, SymbolEqualityComparer.Default);
        EscapedCallbackWrites = new HashSet<ReceiverPath>(other.EscapedCallbackWrites);
        WritableCapturePaths = new HashSet<ReceiverPath>(other.WritableCapturePaths);
        AddressExposed = new HashSet<ReceiverPath>(other.AddressExposed);
    }

    public static GuardFlowState Unknown(bool reachable = true) => new(reachable);

    public GuardFlowState Clone() => new(this);

    public void ReplaceWith(GuardFlowState other)
    {
        IsReachable = other.IsReachable;
        Proofs.Clear();
        Aliases.Clear();
        Captures.Clear();
        OperationFacts.Clear();
        EvaluatedConditions.Clear();
        BooleanFacts.Clear();
        DelegateWrites.Clear();
        DelegateSummaries.Clear();
        UnknownDelegates.Clear();
        EscapedCallbackWrites.Clear();
        WritableCapturePaths.Clear();
        AddressExposed.Clear();
        foreach (var pair in other.Proofs)
            Proofs[pair.Key] = pair.Value;
        foreach (var pair in other.Aliases)
            Aliases[pair.Key] = pair.Value;
        foreach (var pair in other.Captures)
            Captures[pair.Key] = pair.Value;
        foreach (var pair in other.OperationFacts)
            OperationFacts[pair.Key] = pair.Value;
        foreach (var operation in other.EvaluatedConditions)
            EvaluatedConditions.Add(operation);
        foreach (var pair in other.BooleanFacts)
            BooleanFacts[pair.Key] = pair.Value;
        foreach (var pair in other.DelegateWrites)
            DelegateWrites[pair.Key] = pair.Value;
        foreach (var pair in other.DelegateSummaries)
            DelegateSummaries[pair.Key] = pair.Value;
        foreach (var symbol in other.UnknownDelegates)
            UnknownDelegates.Add(symbol);
        foreach (var path in other.EscapedCallbackWrites)
            EscapedCallbackWrites.Add(path);
        foreach (var path in other.WritableCapturePaths)
            WritableCapturePaths.Add(path);
        foreach (var path in other.AddressExposed)
            AddressExposed.Add(path);
    }

    public static GuardFlowState Merge(GuardFlowState left, GuardFlowState right)
    {
        if (!left.IsReachable)
            return right.Clone();
        if (!right.IsReachable)
            return left.Clone();

        var merged = Unknown();

        // A proof survives a join only when every incoming path has one. The
        // possible-kind sets then form their least upper bound.
        foreach (var pair in left.Proofs)
        {
            if (right.Proofs.TryGetValue(pair.Key, out var rightSet))
                merged.Proofs[pair.Key] = pair.Value.Union(rightSet);
        }

        foreach (var symbol in left.Aliases.Keys.Union(
                     right.Aliases.Keys,
                     SymbolEqualityComparer.Default))
        {
            var hasLeft = left.Aliases.TryGetValue(symbol, out var leftTargets);
            var hasRight = right.Aliases.TryGetValue(symbol, out var rightTargets);
            if (hasLeft && hasRight)
            {
                merged.Aliases[symbol] = leftTargets!.Union(rightTargets!);
            }
            else
            {
                merged.Aliases[symbol] = AliasTargetSet.Unknown();
            }
        }

        foreach (var pair in left.BooleanFacts)
        {
            if (right.BooleanFacts.TryGetValue(pair.Key, out var rightFact))
            {
                merged.BooleanFacts[pair.Key] = PredicateFact.MergeAlternatives(
                    pair.Value,
                    rightFact);
            }
        }

        foreach (var id in left.Captures.Keys.Union(right.Captures.Keys))
        {
            var hasLeft = left.Captures.TryGetValue(id, out var leftCapture);
            var hasRight = right.Captures.TryGetValue(id, out var rightCapture);
            if (hasLeft && hasRight)
            {
                var aliasTargets = leftCapture!.AliasTargets is not null &&
                                   rightCapture!.AliasTargets is not null
                    ? leftCapture.AliasTargets.Union(rightCapture.AliasTargets)
                    : leftCapture.AliasTargets ?? rightCapture.AliasTargets;
                if (leftCapture!.Predicate is not null && rightCapture!.Predicate is not null)
                {
                    merged.Captures[id] = new CaptureValue(
                        leftCapture.Value,
                        PredicateFact.MergeAlternatives(
                            leftCapture.Predicate,
                            rightCapture.Predicate).Path,
                        PredicateFact.MergeAlternatives(
                            leftCapture.Predicate,
                            rightCapture.Predicate),
                        aliasTargets: aliasTargets);
                }
                else if (leftCapture.Predicate is not null && rightCapture.Predicate is null)
                {
                    merged.Captures[id] = new CaptureValue(
                        leftCapture.Value,
                        null,
                        null,
                        aliasTargets: aliasTargets);
                }
                else if (rightCapture.Predicate is not null && leftCapture.Predicate is null)
                {
                    merged.Captures[id] = new CaptureValue(
                        rightCapture.Value,
                        null,
                        null,
                        aliasTargets: aliasTargets);
                }
            }
            else
            {
                var capture = hasLeft ? leftCapture! : rightCapture!;
                if (capture.AliasTargets is not null)
                {
                    merged.Captures[id] = new CaptureValue(
                        capture.Value,
                        null,
                        null,
                        aliasTargets: AliasTargetSet.Unknown());
                }
            }
        }

        foreach (var local in left.DelegateWrites.Keys.Union(
                     right.DelegateWrites.Keys,
                     SymbolEqualityComparer.Default))
        {
            var writes = ImmutableHashSet<ReceiverPath>.Empty;
            if (left.DelegateWrites.TryGetValue(local, out var leftWrites))
                writes = writes.Union(leftWrites);
            if (right.DelegateWrites.TryGetValue(local, out var rightWrites))
                writes = writes.Union(rightWrites);
            merged.DelegateWrites[local] = writes;
        }
        foreach (var local in left.DelegateSummaries.Keys.Union(
                     right.DelegateSummaries.Keys,
                     SymbolEqualityComparer.Default))
        {
            var hasLeft = left.DelegateSummaries.TryGetValue(local, out var leftSummary);
            var hasRight = right.DelegateSummaries.TryGetValue(local, out var rightSummary);
            if (hasLeft && hasRight)
                merged.DelegateSummaries[local] = leftSummary!.Union(rightSummary!);
            else if (hasLeft)
            {
                merged.DelegateSummaries[local] = leftSummary!;
                merged.UnknownDelegates.Add(local);
            }
            else if (hasRight)
            {
                merged.DelegateSummaries[local] = rightSummary!;
                merged.UnknownDelegates.Add(local);
            }
        }

        foreach (var symbol in left.UnknownDelegates)
            merged.UnknownDelegates.Add(symbol);
        foreach (var symbol in right.UnknownDelegates)
            merged.UnknownDelegates.Add(symbol);
        foreach (var path in left.EscapedCallbackWrites)
            merged.EscapedCallbackWrites.Add(path);
        foreach (var path in right.EscapedCallbackWrites)
            merged.EscapedCallbackWrites.Add(path);
        foreach (var path in left.WritableCapturePaths)
            merged.WritableCapturePaths.Add(path);
        foreach (var path in right.WritableCapturePaths)
            merged.WritableCapturePaths.Add(path);
        foreach (var operation in left.EvaluatedConditions)
            merged.EvaluatedConditions.Add(operation);
        foreach (var operation in right.EvaluatedConditions)
            merged.EvaluatedConditions.Add(operation);
        foreach (var pair in left.OperationFacts)
        {
            if (right.OperationFacts.TryGetValue(pair.Key, out var rightFact))
            {
                merged.OperationFacts[pair.Key] = PredicateFact.MergeAlternatives(
                    pair.Value,
                    rightFact);
            }
        }

        foreach (var path in left.AddressExposed)
            merged.AddressExposed.Add(path);
        foreach (var path in right.AddressExposed)
            merged.AddressExposed.Add(path);

        return merged;
    }

    public void Refine(ReceiverPath? path, IEnumerable<string> allowed)
    {
        if (!IsReachable || path is null)
            return;

        var allowedSet = allowed.ToImmutableHashSet(StringComparer.Ordinal);
        if (allowedSet.Count == 0)
        {
            IsReachable = false;
            Proofs.Clear();
            Aliases.Clear();
            Captures.Clear();
            BooleanFacts.Clear();
            return;
        }

        if (Proofs.TryGetValue(path, out var existing))
            allowedSet = existing.Intersect(allowedSet);

        if (allowedSet.Count == 0)
        {
            IsReachable = false;
            Proofs.Clear();
            return;
        }

        // A definite ref alias is another view of the same storage. Preserve
        // the proof under both the stable alias view and its concrete target,
        // so a guard through either spelling is usable by the other. Ambiguous
        // aliases deliberately remain symbolic: refining every possible target
        // would manufacture a proof for storage that was not actually tested.
        var proofPaths = new List<ReceiverPath> { path };
        if (path.Segments.Length > 0 &&
            Aliases.TryGetValue(path.Segments[0], out var alias) &&
            alias.IsSingle(out var target))
        {
            foreach (var segment in path.Segments.Skip(1))
                target = target.Append(segment);
            proofPaths.Add(target);
        }

        foreach (var proofPath in proofPaths)
        {
            var refined = allowedSet;
            if (Proofs.TryGetValue(proofPath, out var existingProof))
                refined = existingProof.Intersect(refined);
            if (refined.Count == 0)
            {
                IsReachable = false;
                Proofs.Clear();
                return;
            }
            Proofs[proofPath] = refined;
        }
    }

    public void MarkUnreachable()
    {
        IsReachable = false;
        Proofs.Clear();
        Captures.Clear();
        OperationFacts.Clear();
        EvaluatedConditions.Clear();
        BooleanFacts.Clear();
    }

    public void SetAlias(ISymbol alias, AliasTargetSet targets)
    {
        if (!IsReachable)
            return;
        Aliases[alias] = targets;
    }

    public void SetCapture(CaptureId id, CaptureValue value)
    {
        if (IsReachable)
        {
            Captures[id] = value;
        }
    }

    private static IEnumerable<ReceiverPath> CaptureDependencies(CaptureValue value)
    {
        if (value.Path is not null)
            yield return value.Path;
        if (value.Predicate is not null)
        {
            foreach (var path in value.Predicate.Constraints.Keys)
                yield return path;
        }
    }

    public void SetOperationFact(
        IOperation operation,
        PredicateFact? fact,
        bool suppressFallback = true)
    {
        if (!IsReachable)
            return;
        if (suppressFallback)
            EvaluatedConditions.Add(operation);
        else
            EvaluatedConditions.Remove(operation);
        if (fact is null)
            OperationFacts.Remove(operation);
        else
            OperationFacts[operation] = fact;
    }

    public bool TryGetOperationFact(IOperation operation, out PredicateFact fact)
        => OperationFacts.TryGetValue(operation, out fact!);

    public bool WasConditionEvaluated(IOperation operation)
        => EvaluatedConditions.Contains(operation);

    public void SetBooleanFact(ISymbol local, PredicateFact fact)
    {
        if (IsReachable)
        {
            BooleanFacts[local] = fact;
            DelegateWrites.Remove(local);
            UnknownDelegates.Remove(local);
        }
    }

    public void SetDelegateWrites(ISymbol local, ImmutableHashSet<ReceiverPath> writes)
    {
        if (IsReachable)
        {
            DelegateWrites[local] = writes;
            DelegateSummaries[local] = new EffectSummary(immediateWrites: writes);
            BooleanFacts.Remove(local);
            UnknownDelegates.Remove(local);
            foreach (var path in writes)
                WritableCapturePaths.Add(path);
        }
    }

    public void SetDelegateSummary(ISymbol local, EffectSummary summary)
    {
        if (!IsReachable)
            return;
        DelegateSummaries[local] = summary;
        DelegateWrites[local] = summary.ImmediateWrites;
        BooleanFacts.Remove(local);
        UnknownDelegates.Remove(local);
        foreach (var path in summary.ImmediateWrites)
            WritableCapturePaths.Add(path);
    }

    public void SetUnknownDelegate(ISymbol local)
    {
        if (!IsReachable)
            return;
        DelegateWrites.Remove(local);
        DelegateSummaries.Remove(local);
        BooleanFacts.Remove(local);
        UnknownDelegates.Add(local);
    }

    public void ClearStorageFacts(ISymbol local)
    {
        BooleanFacts.Remove(local);
        DelegateWrites.Remove(local);
        DelegateSummaries.Remove(local);
        UnknownDelegates.Remove(local);
    }

    public void MarkEscapedCallback(ReceiverPath path)
    {
        if (IsReachable)
            EscapedCallbackWrites.Add(path);
    }

    public void InvalidateEscapedCallbacks()
    {
        foreach (var path in EscapedCallbackWrites)
            Invalidate(path);
    }

    public void InvalidateUnknownDelegateEffects()
    {
        if (!IsReachable)
            return;
        InvalidateShared();
        foreach (var path in WritableCapturePaths.ToArray())
            Invalidate(path);
        foreach (var path in AddressExposed.ToArray())
            Invalidate(path);
    }

    public void ApplyOpaqueEffects(EffectSummary summary)
    {
        foreach (var path in summary.ImmediateWrites)
        {
            if (IsReferenceStoragePath(path))
                DetachAliasesUnder(path);
        }
        foreach (var path in summary.ImmediateWrites)
            Invalidate(path);
        foreach (var path in summary.EscapedCallbackWrites)
        {
            MarkEscapedCallback(path);
        }
        if (summary.MayInvokeEscapedCallbacks)
        {
            // An unresolved user-code boundary may replace an intermediate
            // reference object. Existing ref locals must not be retargeted to
            // the same textual path after that replacement.
            DetachAliasesAtOpaqueBoundary();
            InvalidateEscapedCallbacks();
        }
        // Once a value's address has escaped, every opaque user-code
        // boundary that may write shared state can also reach that value
        // through a stored pointer/ref, even when the boundary has no pointer
        // argument of its own. Generated pure projections do not set either
        // capability and therefore retain their precise snapshots.
        if (summary.MayWriteExposed ||
            summary.MayWriteShared && AddressExposed.Count > 0)
            InvalidateAll();
        else if (summary.MayWriteShared)
            InvalidateShared();
    }

    public void MarkAddressExposed(ReceiverPath path)
    {
        if (IsReachable)
            AddressExposed.Add(path);
    }

    public void DetachAliasesUnder(ReceiverPath target)
    {
        foreach (var pair in Aliases.ToArray())
        {
            var affected = pair.Value.Paths
                .Where(path => path.StartsWith(target))
                .ToImmutableHashSet();
            if (affected.Count == 0)
                continue;

            var aliasPath = new ReceiverPath(pair.Key);
            Invalidate(aliasPath, includeAliases: false);
            var remaining = pair.Value.Paths.Except(affected);
            Aliases[pair.Key] = AliasTargetSet.Unknown(remaining);
        }
    }

    private void DetachAliasesAtOpaqueBoundary()
    {
        foreach (var pair in Aliases.ToArray())
        {
            var affected = pair.Value.Paths
                .Where(HasMutableReferencePrefix)
                .ToImmutableHashSet();
            if (affected.Count == 0)
                continue;

            var aliasPath = new ReceiverPath(pair.Key);
            Invalidate(aliasPath, includeAliases: false);
            var remaining = pair.Value.Paths.Except(affected);
            Aliases[pair.Key] = AliasTargetSet.Unknown(remaining);
        }
    }

    private static bool HasMutableReferencePrefix(ReceiverPath path)
    {
        for (var index = 0; index + 1 < path.Segments.Length; index++)
        {
            // A by-value local/parameter receiver cannot itself be rebound by
            // an opaque call. Only reference-typed fields (or deeper members)
            // represent mutable object-location prefixes here.
            if (index == 0 &&
                path.Segments[index] is ILocalSymbol or IParameterSymbol)
                continue;
            if (IsReferenceTyped(path.Segments[index]))
                return true;
        }
        return false;
    }

    private static bool IsReferenceStoragePath(ReceiverPath path)
        => path.Segments.Length > 0 &&
           IsReferenceTyped(path.Segments[path.Segments.Length - 1]);

    private static bool IsReferenceTyped(ISymbol symbol)
        => symbol switch
        {
            IFieldSymbol field => field.Type.IsReferenceType,
            ILocalSymbol local => local.Type.IsReferenceType,
            IParameterSymbol parameter => parameter.Type.IsReferenceType,
            IPropertySymbol property => property.Type.IsReferenceType,
            _ => false
        };

    public void Invalidate(ReceiverPath? target)
    {
        if (!IsReachable || target is null)
            return;

        var terminal = target.Segments.Length == 0
            ? null
            : target.Segments[target.Segments.Length - 1] as IFieldSymbol;

        foreach (var path in Proofs.Keys.ToArray())
        {
            var prefixMatch = target.StartsWith(path) || path.StartsWith(target);
            var possibleHeapAlias = terminal is not null &&
                path.Segments.Any(segment =>
                    SymbolEqualityComparer.Default.Equals(segment, terminal));
            var possibleExternalAlias = target.IsExternalStorage && path.IsSharedStorage ||
                target.IsSharedStorage && path.IsExternalStorage;
            if (prefixMatch || possibleHeapAlias || possibleExternalAlias)
                Proofs.Remove(path);
        }

        foreach (var pair in Captures.Where(pair => CaptureMayAffect(pair.Value, target)).ToArray())
            Captures.Remove(pair.Key);
        foreach (var pair in BooleanFacts.Where(pair => pair.Value.Affects(target)).ToArray())
            BooleanFacts.Remove(pair.Key);
        foreach (var pair in BooleanFacts.Where(pair =>
                     MayAffect(new ReceiverPath(pair.Key), target)).ToArray())
            BooleanFacts.Remove(pair.Key);
        foreach (var pair in OperationFacts.Where(pair => pair.Value.Affects(target)).ToArray())
            OperationFacts.Remove(pair.Key);
        foreach (var pair in DelegateWrites.Where(pair =>
                     MayAffect(new ReceiverPath(pair.Key), target)).ToArray())
            DelegateWrites.Remove(pair.Key);
        foreach (var symbol in UnknownDelegates.Where(symbol =>
                     MayAffect(new ReceiverPath(symbol), target)).ToArray())
            UnknownDelegates.Remove(symbol);

        // A proof keyed by an ambiguous ref local describes the storage seen
        // through that local. A write through any of its possible targets
        // invalidates that view even though the target path itself is not the
        // synthetic local path.
        foreach (var pair in Aliases.ToArray())
        {
            if (pair.Value.IsUnknown || pair.Value.Paths.Any(path => MayAffect(path, target)))
            {
                Invalidate(new ReceiverPath(pair.Key), includeAliases: false);
            }
        }
    }

    public void InvalidateShared()
    {
        // A synthetic ref-local path is not itself marked shared. Its target
        // set is the storage identity, so shared invalidation must inspect the
        // current alias view before retaining facts under that local spelling.
        foreach (var path in Proofs.Keys.Where(PathMayBeShared).ToArray())
            Proofs.Remove(path);
        foreach (var pair in Captures.Where(pair => CaptureHasSharedPath(pair.Value)).ToArray())
            Captures.Remove(pair.Key);
        foreach (var pair in BooleanFacts.Where(pair => PredicateHasSharedPath(pair.Value)).ToArray())
            BooleanFacts.Remove(pair.Key);
        foreach (var pair in OperationFacts.Where(pair => PredicateHasSharedPath(pair.Value)).ToArray())
            OperationFacts.Remove(pair.Key);
        // Delegate maps describe executable effect capabilities, not facts
        // about the current discriminator values. A shared write invalidates
        // their dependent proofs, but must not erase a callback's known write
        // set; the callback may execute later against a fresh guard.
    }

    public void InvalidateSuspension()
    {
        InvalidateEscapedCallbacks();
        InvalidateShared();
        foreach (var writes in DelegateWrites.Values.ToArray())
        {
            foreach (var path in writes)
                Invalidate(path);
        }
    }

    public void InvalidateAll()
    {
        InvalidateShared();
        foreach (var path in AddressExposed.ToArray())
            Invalidate(path);
    }

    private void Invalidate(ReceiverPath? target, bool includeAliases)
    {
        if (!IsReachable || target is null)
            return;

        var terminal = target.Segments.Length == 0
            ? null
            : target.Segments[target.Segments.Length - 1] as IFieldSymbol;

        foreach (var path in Proofs.Keys.ToArray())
        {
            var prefixMatch = target.StartsWith(path) || path.StartsWith(target);
            var possibleHeapAlias = terminal is not null &&
                path.Segments.Any(segment =>
                    SymbolEqualityComparer.Default.Equals(segment, terminal));
            var possibleExternalAlias = target.IsExternalStorage && path.IsSharedStorage ||
                target.IsSharedStorage && path.IsExternalStorage;
            if (prefixMatch || possibleHeapAlias || possibleExternalAlias)
                Proofs.Remove(path);
        }

        foreach (var pair in Captures.Where(pair => CaptureMayAffect(pair.Value, target)).ToArray())
            Captures.Remove(pair.Key);
        foreach (var pair in BooleanFacts.Where(pair => pair.Value.Affects(target)).ToArray())
            BooleanFacts.Remove(pair.Key);
        foreach (var pair in OperationFacts.Where(pair => pair.Value.Affects(target)).ToArray())
            OperationFacts.Remove(pair.Key);

        if (!includeAliases)
            return;
        foreach (var pair in Aliases.ToArray())
        {
            if (pair.Value.IsUnknown || pair.Value.Paths.Any(path => MayAffect(path, target)))
                Invalidate(new ReceiverPath(pair.Key), includeAliases: false);
        }
    }

    private static bool MayAffect(ReceiverPath? candidate, ReceiverPath target)
    {
        if (candidate is null)
            return false;
        if (target.StartsWith(candidate) || candidate.StartsWith(target))
            return true;

        var terminal = target.Segments.Length == 0
            ? null
            : target.Segments[target.Segments.Length - 1];
        return terminal is not null && candidate.Segments.Any(segment =>
            SymbolEqualityComparer.Default.Equals(segment, terminal));
    }

    private static bool CaptureMayAffect(CaptureValue capture, ReceiverPath target)
        => MayAffect(capture.Path, target) ||
           capture.Predicate is not null && capture.Predicate.Affects(target);

    private bool PathMayBeShared(ReceiverPath path)
    {
        if (path.IsSharedStorage)
            return true;
        if (path.Segments.Length == 0 ||
            !Aliases.TryGetValue(path.Segments[0], out var aliases))
            return false;
        if (aliases.IsUnknown)
            return true;

        foreach (var target in aliases.Paths)
        {
            var candidate = target;
            foreach (var segment in path.Segments.Skip(1))
                candidate = candidate.Append(segment);
            if (candidate.IsSharedStorage)
                return true;
        }
        return false;
    }

    private bool PredicateHasSharedPath(PredicateFact predicate)
        => predicate.Constraints.Keys.Any(PathMayBeShared);

    private bool CaptureHasSharedPath(CaptureValue capture)
        => capture.Path is not null && PathMayBeShared(capture.Path) ||
           capture.Predicate is not null && PredicateHasSharedPath(capture.Predicate);

    private static bool SamePredicate(PredicateFact left, PredicateFact right)
    {
        if (left.UnsafeForSource != right.UnsafeForSource ||
            left.Constraints.Count != right.Constraints.Count)
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
}
