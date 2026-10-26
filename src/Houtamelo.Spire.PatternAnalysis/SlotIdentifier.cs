using System.Collections.Immutable;
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

    internal sealed class PropertySlot : SlotIdentifier
    {
        public ImmutableArray<ISymbol> Path { get; }

        // Existing DU helpers model direct properties. Nested paths expose
        // their final property here for compatibility with those helpers.
        public IPropertySymbol? Property => Path.Length > 0 ? Path[Path.Length - 1] as IPropertySymbol : null;

        public PropertySlot(IPropertySymbol property)
            : this(ImmutableArray.Create<ISymbol>(property))
        {
        }

        public PropertySlot(ImmutableArray<ISymbol> path)
        {
            Path = path;
        }
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
