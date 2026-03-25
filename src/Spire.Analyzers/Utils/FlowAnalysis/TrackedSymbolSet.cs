using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Spire.Analyzers.Utils.FlowAnalysis;

/// Resolved type metadata for flow analysis. Built once per compilation.
public sealed class TrackedSymbolSet
{
    /// Types whose variables need init-state tracking (have [EnforceInitialization] + instance fields).
    /// Maps type → ordered list of instance fields (field ordinal = index in array).
    private readonly Dictionary<INamedTypeSymbol, ImmutableArray<IFieldSymbol>> _initTrackedTypes;

    /// The [EnforceInitialization] attribute type, resolved from compilation.
    public INamedTypeSymbol? EnforceInitializationType { get; }

    public TrackedSymbolSet(
        INamedTypeSymbol? enforceInitializationType,
        Dictionary<INamedTypeSymbol, ImmutableArray<IFieldSymbol>> initTrackedTypes)
    {
        EnforceInitializationType = enforceInitializationType;
        _initTrackedTypes = initTrackedTypes;
    }

    /// Returns field ordinal map if this type needs init tracking, or default if not.
    public bool TryGetFieldOrdinals(INamedTypeSymbol type, out ImmutableArray<IFieldSymbol> fields)
    {
        if (_initTrackedTypes.TryGetValue(type, out fields))
            return true;

        fields = default;
        return false;
    }

    /// Returns ordinal index of a field within its type's field list, or -1 if not tracked.
    public int GetFieldOrdinal(INamedTypeSymbol containingType, IFieldSymbol field)
    {
        if (!_initTrackedTypes.TryGetValue(containingType, out var fields))
            return -1;

        for (int i = 0; i < fields.Length; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(fields[i], field))
                return i;
        }

        return -1;
    }

    /// Creates initial VariableState for a variable of the given type.
    public VariableState CreateInitialState(ITypeSymbol type, InitState initState, NullState nullState)
    {
        if (type is INamedTypeSymbol named && _initTrackedTypes.TryGetValue(named, out var fields))
        {
            var builder = ImmutableArray.CreateBuilder<InitState>(fields.Length);
            for (int i = 0; i < fields.Length; i++)
                builder.Add(initState);
            return new VariableState(builder.MoveToImmutable(), KindState.Unknown, nullState);
        }

        return new VariableState(ImmutableArray<InitState>.Empty, KindState.Unknown, nullState);
    }

    /// Registers a type for init tracking. Call during CompilationStartAction.
    public static Dictionary<INamedTypeSymbol, ImmutableArray<IFieldSymbol>> BuildFieldMap(
        IEnumerable<INamedTypeSymbol> enforceInitializationTypes)
    {
        var map = new Dictionary<INamedTypeSymbol, ImmutableArray<IFieldSymbol>>(SymbolEqualityComparer.Default);

        foreach (var type in enforceInitializationTypes)
        {
            var fields = ImmutableArray.CreateBuilder<IFieldSymbol>();
            foreach (var member in type.GetMembers())
            {
                if (member is IFieldSymbol { IsStatic: false } field)
                    fields.Add(field);
            }

            if (fields.Count > 0)
                map[type] = fields.ToImmutable();
        }

        return map;
    }
}
