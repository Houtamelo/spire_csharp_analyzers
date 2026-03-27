using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.PatternAnalysis;

/// Identifies a position within a pattern that a constraint applies to.
internal abstract class SlotIdentifier
{
    private SlotIdentifier() { }

    /// The top-level switch subject (column 0 in a non-structural switch).
    internal sealed class RootSlot(ITypeSymbol type) : SlotIdentifier
    {
        public ITypeSymbol Type { get; } = type;
    }

    internal sealed class PropertySlot(IPropertySymbol property) : SlotIdentifier
    {
        public IPropertySymbol Property { get; } = property;
    }

    internal sealed class TupleSlot(int index, ITypeSymbol elementType) : SlotIdentifier
    {
        public int Index { get; } = index;
        public ITypeSymbol ElementType { get; } = elementType;
    }

    internal sealed class DeconstructSlot(int index, IMethodSymbol deconstructor) : SlotIdentifier
    {
        public int Index { get; } = index;
        public IMethodSymbol Deconstructor { get; } = deconstructor;
    }
}
