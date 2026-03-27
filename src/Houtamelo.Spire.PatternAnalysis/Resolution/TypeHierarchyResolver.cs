using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.PatternAnalysis.Resolution;

/// Walks a Roslyn compilation to find all concrete types that implement/extend a base type.
/// Uses visibility-based scoping to limit the search to reachable assemblies.
internal sealed class TypeHierarchyResolver
{
    private readonly ConcurrentDictionary<INamedTypeSymbol, ImmutableArray<INamedTypeSymbol>> _cache
        = new(SymbolEqualityComparer.Default);

    public ImmutableArray<INamedTypeSymbol> Resolve(INamedTypeSymbol baseType, Compilation compilation)
    {
        return _cache.GetOrAdd(baseType, bt => ResolveCore(bt, compilation));
    }

    private ImmutableArray<INamedTypeSymbol> ResolveCore(INamedTypeSymbol baseType, Compilation compilation)
    {
        // Rule 1: sealed — no derived types possible
        if (baseType.IsSealed)
            return ImmutableArray<INamedTypeSymbol>.Empty;

        var scope = DetermineScope(baseType, compilation);
        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

        switch (scope)
        {
            case SearchScope.NestedOnly nestedOnly:
                // Only walk nested types of the constraining type
                WalkNestedTypes(nestedOnly.ContainingType, baseType, builder);
                break;

            case SearchScope.Assemblies assemblies:
                foreach (var assembly in assemblies.AssemblyList)
                {
                    WalkNamespace(assembly.GlobalNamespace, baseType, builder);
                }
                break;
        }

        return builder.ToImmutable();
    }

    /// Represents the resolved search scope for a type hierarchy walk.
    private abstract class SearchScope
    {
        private SearchScope() { }

        /// Only search nested types of a specific containing type.
        internal sealed class NestedOnly(INamedTypeSymbol containingType) : SearchScope
        {
            public INamedTypeSymbol ContainingType { get; } = containingType;
        }

        /// Search all types across the given list of assemblies.
        internal sealed class Assemblies(ImmutableArray<IAssemblySymbol> assemblyList) : SearchScope
        {
            public ImmutableArray<IAssemblySymbol> AssemblyList { get; } = assemblyList;
        }
    }

    /// Applies the visibility-based scoping rules to determine where to search.
    private static SearchScope DetermineScope(INamedTypeSymbol baseType, Compilation compilation)
    {
        // Rule 2: nested + private/protected — parent type's nested types only
        if (baseType.ContainingType != null)
        {
            var accessibility = baseType.DeclaredAccessibility;
            if (accessibility == Accessibility.Private || accessibility == Accessibility.Protected)
            {
                return new SearchScope.NestedOnly(baseType.ContainingType);
            }

            // Rule 3: nested + parent is internal/protected/private — declaring assembly only
            var parentAccessibility = GetEffectiveAccessibility(baseType.ContainingType);
            if (parentAccessibility != Accessibility.Public)
            {
                return new SearchScope.Assemblies(ImmutableArray.Create(baseType.ContainingAssembly));
            }
        }

        // Rule 4: base type itself is internal/protected/private
        if (baseType.DeclaredAccessibility != Accessibility.Public)
        {
            return new SearchScope.Assemblies(ImmutableArray.Create(baseType.ContainingAssembly));
        }

        // Rules 5/6: constructor visibility (skip for interfaces — they have no ctors)
        if (baseType.TypeKind != TypeKind.Interface)
        {
            var mostExposedCtor = GetMostExposedCtorAccessibility(baseType);
            if (mostExposedCtor != null)
            {
                // Rule 5: private ctor — only base type's nested types
                if (mostExposedCtor.Value == Accessibility.Private)
                {
                    return new SearchScope.NestedOnly(baseType);
                }

                // Rule 6: internal ctor — declaring assembly only
                if (mostExposedCtor.Value == Accessibility.Internal ||
                    mostExposedCtor.Value == Accessibility.ProtectedAndInternal)
                {
                    return new SearchScope.Assemblies(ImmutableArray.Create(baseType.ContainingAssembly));
                }

                // Protected ctor: external classes can still inherit if they extend the type
                if (mostExposedCtor.Value == Accessibility.Protected)
                {
                    return new SearchScope.Assemblies(
                        GetDeclaringAndDependentAssemblies(baseType, compilation));
                }
            }
        }

        // Rule 7: public type + public ctor (or interface) — declaring + dependent assemblies
        return new SearchScope.Assemblies(GetDeclaringAndDependentAssemblies(baseType, compilation));
    }

    /// Returns the most accessible instance constructor, or null if no instance ctors exist.
    private static Accessibility? GetMostExposedCtorAccessibility(INamedTypeSymbol type)
    {
        Accessibility? best = null;
        foreach (var ctor in type.InstanceConstructors)
        {
            var access = ctor.DeclaredAccessibility;
            if (best == null || AccessibilityRank(access) > AccessibilityRank(best.Value))
                best = access;
        }

        return best;
    }

    private static int AccessibilityRank(Accessibility accessibility)
    {
        switch (accessibility)
        {
            case Accessibility.Public: return 5;
            case Accessibility.ProtectedOrInternal: return 4;
            case Accessibility.Internal: return 3;
            case Accessibility.Protected: return 2;
            case Accessibility.ProtectedAndInternal: return 1;
            case Accessibility.Private: return 0;
            default: return -1;
        }
    }

    /// Walks up through containing types to find the effective (most restrictive) accessibility.
    private static Accessibility GetEffectiveAccessibility(INamedTypeSymbol type)
    {
        var current = type;
        var effective = current.DeclaredAccessibility;

        while (current.ContainingType != null)
        {
            current = current.ContainingType;
            if (AccessibilityRank(current.DeclaredAccessibility) < AccessibilityRank(effective))
                effective = current.DeclaredAccessibility;
        }

        return effective;
    }

    /// Returns the declaring assembly plus all assemblies that reference it.
    private static ImmutableArray<IAssemblySymbol> GetDeclaringAndDependentAssemblies(
        INamedTypeSymbol baseType, Compilation compilation)
    {
        var declaringAssembly = baseType.ContainingAssembly;
        var result = new List<IAssemblySymbol> { declaringAssembly };

        // The compilation's own assembly might reference the declaring assembly
        if (!SymbolEqualityComparer.Default.Equals(compilation.Assembly, declaringAssembly))
        {
            if (ReferencesAssembly(compilation.Assembly, declaringAssembly))
                result.Add(compilation.Assembly);
        }

        // Check each referenced assembly
        foreach (var reference in compilation.References)
        {
            var symbol = compilation.GetAssemblyOrModuleSymbol(reference);
            if (symbol is IAssemblySymbol assemblySymbol &&
                !SymbolEqualityComparer.Default.Equals(assemblySymbol, declaringAssembly))
            {
                if (ReferencesAssembly(assemblySymbol, declaringAssembly))
                    result.Add(assemblySymbol);
            }
        }

        return result.ToImmutableArray();
    }

    /// Checks if `assembly` has a reference to `target`.
    private static bool ReferencesAssembly(IAssemblySymbol assembly, IAssemblySymbol target)
    {
        foreach (var module in assembly.Modules)
        {
            foreach (var referenced in module.ReferencedAssemblySymbols)
            {
                if (SymbolEqualityComparer.Default.Equals(referenced, target))
                    return true;
            }
        }

        return false;
    }

    /// Walk only the nested types of a given containing type.
    private static void WalkNestedTypes(
        INamedTypeSymbol containingType,
        INamedTypeSymbol baseType,
        ImmutableArray<INamedTypeSymbol>.Builder builder)
    {
        foreach (var nested in containingType.GetTypeMembers())
        {
            CheckType(nested, baseType, builder);
        }
    }

    /// Recursively walks namespaces and types to find concrete subtypes.
    private static void WalkNamespace(
        INamespaceSymbol ns,
        INamedTypeSymbol baseType,
        ImmutableArray<INamedTypeSymbol>.Builder builder)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            CheckType(type, baseType, builder);
        }

        foreach (var child in ns.GetNamespaceMembers())
        {
            WalkNamespace(child, baseType, builder);
        }
    }

    /// Checks a single type and recurses into its nested types.
    private static void CheckType(
        INamedTypeSymbol type,
        INamedTypeSymbol baseType,
        ImmutableArray<INamedTypeSymbol>.Builder builder)
    {
        // Only include concrete types (not abstract, not interface, not the base type itself)
        if (!type.IsAbstract && type.TypeKind != TypeKind.Interface &&
            !SymbolEqualityComparer.Default.Equals(type, baseType))
        {
            if (DerivesFrom(type, baseType))
                builder.Add(type);
        }

        // Recurse into nested types
        foreach (var nested in type.GetTypeMembers())
        {
            CheckType(nested, baseType, builder);
        }
    }

    /// Determines if `type` derives from or implements `baseType`.
    /// Uses OriginalDefinition comparison to handle generic base types.
    private static bool DerivesFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        if (baseType.TypeKind == TypeKind.Interface)
            return ImplementsInterface(type, baseType);

        return ExtendsClass(type, baseType);
    }

    private static bool ImplementsInterface(INamedTypeSymbol type, INamedTypeSymbol interfaceType)
    {
        foreach (var iface in type.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(iface, interfaceType) ||
                SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, interfaceType.OriginalDefinition))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ExtendsClass(INamedTypeSymbol type, INamedTypeSymbol baseClass)
    {
        var current = type.BaseType;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseClass) ||
                SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, baseClass.OriginalDefinition))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }
}
