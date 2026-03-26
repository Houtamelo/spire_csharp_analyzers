using System.Collections.Immutable;
using Houtamelo.Spire.PatternAnalysis.Domains;

namespace Houtamelo.Spire.PatternAnalysis;

/// Result of exhaustiveness analysis on a switch expression/statement.
internal readonly struct ExhaustivenessResult(ImmutableArray<MissingCase> missingCases)
{
    public ImmutableArray<MissingCase> MissingCases { get; } = missingCases;
}

/// A single combination of uncovered values across one or more slots.
internal readonly struct MissingCase(ImmutableArray<SlotConstraint> constraints)
{
    public ImmutableArray<SlotConstraint> Constraints { get; } = constraints;
}

/// An uncovered portion of a single slot's domain.
internal readonly struct SlotConstraint(SlotIdentifier slot, IValueDomain remaining)
{
    public SlotIdentifier Slot { get; } = slot;
    public IValueDomain Remaining { get; } = remaining;
}
