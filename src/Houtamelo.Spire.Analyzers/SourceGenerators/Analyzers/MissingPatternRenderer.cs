using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using Houtamelo.Spire.PatternAnalysis;
using Houtamelo.Spire.PatternAnalysis.Domains;
using Houtamelo.Spire.PatternAnalysis.Domains.Numeric;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;

/// Converts determinate pattern-analysis witnesses into source patterns that
/// can be consumed by the AddMissingArms code fix.  The renderer deliberately
/// returns no pattern when a witness cannot be represented without guessing.
internal static class MissingPatternRenderer
{
    internal static ImmutableArray<string> Render(
        ITypeSymbol subjectType,
        UnionTypeInfo? unionInfo,
        ExhaustivenessResult result,
        out bool canUseVariantTemplates)
    {
        canUseVariantTemplates = false;
        if (!result.IsDeterminate || result.MissingCases.IsEmpty)
            return ImmutableArray<string>.Empty;

        var rendered = new List<(string Pattern, int Order, int Index)>();
        var allWholeRoot = true;
        var index = 0;
        foreach (var missingCase in result.MissingCases)
        {
            if (!TryRenderCase(
                    subjectType,
                    unionInfo,
                    missingCase,
                    out var pattern,
                    out var isWholeRoot))
            {
                // Never expose a partial list of witnesses. Applying a
                // partial fix would leave the original diagnostic active.
                return ImmutableArray<string>.Empty;
            }

            allWholeRoot &= isWholeRoot;

            if (rendered.Any(entry => entry.Pattern.Equals(pattern, StringComparison.Ordinal)))
                continue;

            rendered.Add((pattern, GetOrderKey(missingCase), index++));
        }

        if (rendered.Count > 1)
        {
            rendered.Sort((left, right) =>
            {
                var leftIsNull = left.Pattern.Equals("null", StringComparison.Ordinal);
                var rightIsNull = right.Pattern.Equals("null", StringComparison.Ordinal);
                if (leftIsNull != rightIsNull)
                    return leftIsNull ? 1 : -1;

                var order = left.Order.CompareTo(right.Order);
                return order != 0 ? order : left.Index.CompareTo(right.Index);
            });
        }

        canUseVariantTemplates = unionInfo is not null &&
            allWholeRoot &&
            !rendered.Any(entry => entry.Pattern.Equals("null", StringComparison.Ordinal));

        return rendered.Select(entry => entry.Pattern).ToImmutableArray();
    }

    private static int GetOrderKey(MissingCase missingCase)
    {
        var propertyBest = int.MaxValue;
        var rootBest = int.MaxValue;
        foreach (var constraint in missingCase.Constraints)
        {
            var key = GetOrderKey(constraint.Remaining);
            if (constraint.Slot is SlotIdentifier.PropertySlot)
                propertyBest = Math.Min(propertyBest, key);
            else
                rootBest = Math.Min(rootBest, key);
        }

        return propertyBest != int.MaxValue ? propertyBest : rootBest;
    }

    private static int GetOrderKey(IValueDomain domain)
    {
        if (domain is NullableDomain nullable)
            return nullable.HasNull ? GetOrderKey(nullable.Inner) : GetOrderKey(nullable.Inner);

        if (domain is EnumDomain enumDomain)
        {
            foreach (var member in enumDomain.Members)
            {
                var location = member.Locations.FirstOrDefault();
                if (location is not null)
                    return location.SourceSpan.Start;
            }
        }
        else if (domain is EnforceExhaustiveDomain exhaustive)
        {
            foreach (var type in exhaustive.RemainingTypes)
            {
                var location = type.Locations.FirstOrDefault();
                if (location is not null)
                    return location.SourceSpan.Start;
            }
        }

        return int.MaxValue;
    }

    private static bool TryRenderCase(
        ITypeSymbol subjectType,
        UnionTypeInfo? unionInfo,
        MissingCase missingCase,
        out string pattern,
        out bool isWholeRoot)
    {
        pattern = string.Empty;
        isWholeRoot = false;
        var root = missingCase.Constraints
            .FirstOrDefault(constraint => constraint.Slot is SlotIdentifier.RootSlot);
        if (root.Slot is not SlotIdentifier.RootSlot)
            return false;

        // Null is a complete witness. Any property constraints accumulated on
        // the null branch are irrelevant and must not become unreachable
        // nested property patterns.
        if (TryRenderNullOnly(root.Remaining, out var isNull) && isNull)
        {
            pattern = "null";
            return true;
        }

        var rootParts = RenderRootDomain(
            root.Remaining,
            subjectType,
            unionInfo,
            out var rootIsStructKind);
        if (rootParts.Count == 0)
            return false;

        var propertyConstraints = missingCase.Constraints
            .Where(constraint => constraint.Slot is SlotIdentifier.PropertySlot)
            .Select(constraint =>
                ((SlotIdentifier.PropertySlot)constraint.Slot, constraint.Remaining))
            .ToImmutableArray();

        var meaningfulProperties = propertyConstraints
            .Where(entry => IsActiveProperty(entry.Item1, root.Remaining, subjectType, unionInfo))
            .Where(entry => IsMeaningful(entry.Item2))
            .ToImmutableArray();

        // A complete root variant is intentionally left to the existing DU
        // template code fix. It preserves captures and generated accessor
        // naming that cannot be inferred from a missing witness alone.
        if (meaningfulProperties.IsDefaultOrEmpty)
        {
            if (rootIsStructKind)
            {
                if (rootParts.Count != 1)
                    return false;
                pattern = $"{{ {BuildStructPredicate(rootParts[0])} }}";
            }
            else
            {
                pattern = rootParts.Count == 1 ? rootParts[0] : string.Join(" or ", rootParts);
            }

            isWholeRoot = true;
            return true;
        }

        var propertyRoot = new PropertyNode();
        foreach (var entry in meaningfulProperties)
            AddProperty(propertyRoot, entry.Item1.Path, entry.Item2);

        if (!TryRenderProperties(propertyRoot, out var propertyText))
            return false;
        if (propertyText.Length == 0)
        {
            if (rootIsStructKind)
            {
                if (rootParts.Count != 1)
                    return false;
                pattern = $"{{ {BuildStructPredicate(rootParts[0])} }}";
            }
            else
            {
                pattern = rootParts.Count == 1 ? rootParts[0] : string.Join(" or ", rootParts);
            }

            isWholeRoot = true;
            return true;
        }

        if (rootIsStructKind)
        {
            var arms = rootParts.Select(part =>
                $"{{ {BuildStructPredicate(part)}, {propertyText} }}");
            pattern = rootParts.Count == 1
                ? arms.First()
                : $"({string.Join(" or ", arms)})";
        }
        else
        {
            var arms = rootParts.Select(part => $"{part} {{ {propertyText} }}");
            pattern = rootParts.Count == 1
                ? arms.First()
                : $"({string.Join(" or ", arms)})";
        }

        return true;
    }

    private static string BuildStructPredicate(string kindPattern)
    {
        var separator = kindPattern.LastIndexOf('.');
        var variantName = separator >= 0
            ? kindPattern.Substring(separator + 1)
            : kindPattern;
        if (variantName.StartsWith("@", StringComparison.Ordinal))
            variantName = variantName.Substring(1);
        return $"{EscapeIdentifier("Is" + variantName)}: true";
    }

    private static List<string> RenderRootDomain(
        IValueDomain domain,
        ITypeSymbol subjectType,
        UnionTypeInfo? unionInfo,
        out bool isStructKind)
    {
        isStructKind = false;
        if (domain is NullableDomain nullable)
        {
            if (nullable.HasNull && nullable.Inner.IsEmpty)
                return new List<string> { "null" };
            domain = nullable.Inner;
        }

        if (domain is EnumDomain enumDomain)
        {
            var members = CanonicalEnumMembers(enumDomain.Members)
                .Select(member =>
                {
                    var enumType = member.ContainingType;
                    var typeName = enumType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    return $"{typeName}.{EscapeIdentifier(member.Name)}";
                })
                .ToList();

            // Struct DU root domains are the generated Kind enum. Their
            // patterns need the containing DU's property discriminator.
            if (unionInfo?.IsStructUnion == true &&
                unionInfo.KindEnumType is not null &&
                SymbolEqualityComparer.Default.Equals(
                    enumDomain.Type,
                    unionInfo.KindEnumType))
            {
                isStructKind = true;
            }

            return members;
        }

        if (domain is EnforceExhaustiveDomain exhaustive)
        {
            return exhaustive.RemainingTypes
                .OrderBy(type => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal)
                .Select(type => RenderType(type, subjectType, fullyQualified: true))
                .Where(type => type is not null)
                .Select(type => type!)
                .ToList();
        }

        if (domain is StructuralDomain structural && structural.IsUniverse)
        {
            // A structural root without a finite discriminator cannot be
            // represented as a safe missing arm.
            return new List<string>();
        }

        return new List<string>();
    }

    private static string? RenderType(
        INamedTypeSymbol type,
        ITypeSymbol subjectType,
        bool fullyQualified)
    {
        var rendered = type;
        if (type.Arity > 0 && subjectType is INamedTypeSymbol subject)
        {
            if (!TryConstructForSubject(type, subject, out rendered))
                return null;
        }

        var format = fullyQualified
            ? SymbolDisplayFormat.FullyQualifiedFormat
            : SymbolDisplayFormat.MinimallyQualifiedFormat;
        return rendered.ToDisplayString(format);
    }

    private static bool TryConstructForSubject(
        INamedTypeSymbol candidate,
        INamedTypeSymbol subject,
        out INamedTypeSymbol constructed)
    {
        constructed = candidate;
        var matchingBase = FindMatchingBase(candidate, subject.OriginalDefinition);
        if (matchingBase is null ||
            matchingBase.TypeArguments.Length != subject.TypeArguments.Length)
        {
            return false;
        }

        var substitutions = new Dictionary<ITypeParameterSymbol, ITypeSymbol>(
            SymbolEqualityComparer.Default);
        for (var index = 0; index < matchingBase.TypeArguments.Length; index++)
        {
            if (!UnifyType(
                    matchingBase.TypeArguments[index],
                    subject.TypeArguments[index],
                    substitutions))
            {
                return false;
            }
        }

        var arguments = new ITypeSymbol[candidate.TypeParameters.Length];
        for (var index = 0; index < candidate.TypeParameters.Length; index++)
        {
            if (!substitutions.TryGetValue(candidate.TypeParameters[index], out var argument))
                return false;
            arguments[index] = argument;
        }

        constructed = candidate.Construct(arguments);
        return true;
    }

    private static INamedTypeSymbol? FindMatchingBase(
        INamedTypeSymbol candidate,
        INamedTypeSymbol subjectDefinition)
    {
        for (var current = candidate.BaseType;
             current is not null;
             current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(
                    current.OriginalDefinition,
                    subjectDefinition))
            {
                return current;
            }
        }

        foreach (var iface in candidate.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(
                    iface.OriginalDefinition,
                    subjectDefinition))
            {
                return iface;
            }
        }

        return null;
    }

    private static bool UnifyType(
        ITypeSymbol pattern,
        ITypeSymbol actual,
        Dictionary<ITypeParameterSymbol, ITypeSymbol> substitutions)
    {
        if (pattern is ITypeParameterSymbol parameter)
        {
            if (substitutions.TryGetValue(parameter, out var previous))
                return SymbolEqualityComparer.Default.Equals(previous, actual);
            substitutions.Add(parameter, actual);
            return true;
        }

        if (pattern is INamedTypeSymbol patternNamed &&
            actual is INamedTypeSymbol actualNamed &&
            SymbolEqualityComparer.Default.Equals(
                patternNamed.OriginalDefinition,
                actualNamed.OriginalDefinition) &&
            patternNamed.TypeArguments.Length == actualNamed.TypeArguments.Length)
        {
            for (var index = 0; index < patternNamed.TypeArguments.Length; index++)
            {
                if (!UnifyType(
                        patternNamed.TypeArguments[index],
                        actualNamed.TypeArguments[index],
                        substitutions))
                {
                    return false;
                }
            }

            return true;
        }

        return SymbolEqualityComparer.Default.Equals(pattern, actual);
    }

    private static bool TryRenderNullOnly(IValueDomain domain, out bool isNull)
    {
        if (domain is NullableDomain nullable)
        {
            isNull = nullable.HasNull && nullable.Inner.IsEmpty;
            return true;
        }

        isNull = false;
        return false;
    }

    private static bool IsMeaningful(IValueDomain domain)
    {
        if (domain.IsEmpty)
            return false;
        if (domain.IsUniverse)
            return false;
        return true;
    }

    private static bool IsActiveProperty(
        SlotIdentifier.PropertySlot slot,
        IValueDomain rootDomain,
        ITypeSymbol subjectType,
        UnionTypeInfo? unionInfo)
    {
        if (slot.Path.IsDefaultOrEmpty || unionInfo is null)
            return true;

        var first = slot.Path[0];
        var activeNames = GetActiveVariantNames(rootDomain);
        if (activeNames.Count == 0)
            return true;

        if (unionInfo.IsStructUnion && subjectType is INamedTypeSymbol namedSubject)
        {
            var map = VariantFieldMap.TryCreate(
                UnwrapNullable(namedSubject),
                unionInfo);
            var owners = map?.GetOwningVariants(first.Name);
            if (owners is null || owners.Count == 0)
                return true;
            return owners.Any(activeNames.Contains);
        }

        if (first is not IPropertySymbol && first is not IFieldSymbol)
            return true;

        foreach (var activeType in GetActiveTypes(rootDomain))
        {
            if (TypeContainsMember(activeType, first))
            {
                return true;
            }
        }

        // If the property symbol does not belong to any known active type,
        // preserve it. This covers direct properties on an enforced base and
        // avoids dropping a witness solely because Roslyn used a substituted
        // symbol for a generic member.
        return !GetActiveTypes(rootDomain).Any();
    }

    private static bool TypeContainsMember(INamedTypeSymbol type, ISymbol member)
    {
        for (var current = type;
             current is not null;
             current = current.BaseType)
        {
            if (current.GetMembers().Any(candidate => SymbolsEqual(candidate, member)))
                return true;
        }

        foreach (var iface in type.AllInterfaces)
        {
            if (iface.GetMembers().Any(candidate => SymbolsEqual(candidate, member)))
                return true;
        }

        return false;
    }

    private static bool SymbolsEqual(ISymbol left, ISymbol right)
    {
        if (SymbolEqualityComparer.Default.Equals(left, right))
            return true;

        return left switch
        {
            IPropertySymbol leftProperty when right is IPropertySymbol rightProperty =>
                SymbolEqualityComparer.Default.Equals(
                    leftProperty.OriginalDefinition,
                    rightProperty.OriginalDefinition),
            IFieldSymbol leftField when right is IFieldSymbol rightField =>
                SymbolEqualityComparer.Default.Equals(
                    leftField.OriginalDefinition,
                    rightField.OriginalDefinition),
            _ => false,
        };
    }

    private static HashSet<string> GetActiveVariantNames(IValueDomain domain)
    {
        while (domain is NullableDomain nullable)
        {
            if (nullable.Inner.IsEmpty)
                return new HashSet<string>(StringComparer.Ordinal);
            domain = nullable.Inner;
        }

        if (domain is EnumDomain enumDomain)
            return new HashSet<string>(
                enumDomain.Members.Select(member => member.Name),
                StringComparer.Ordinal);

        return new HashSet<string>(StringComparer.Ordinal);
    }

    private static ImmutableArray<INamedTypeSymbol> GetActiveTypes(IValueDomain domain)
    {
        while (domain is NullableDomain nullable)
        {
            if (nullable.Inner.IsEmpty)
                return ImmutableArray<INamedTypeSymbol>.Empty;
            domain = nullable.Inner;
        }

        return domain is EnforceExhaustiveDomain exhaustive
            ? exhaustive.RemainingTypes.ToImmutableArray()
            : ImmutableArray<INamedTypeSymbol>.Empty;
    }

    private static INamedTypeSymbol UnwrapNullable(INamedTypeSymbol type)
    {
        return type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
               type.TypeArguments.Length == 1 &&
               type.TypeArguments[0] is INamedTypeSymbol inner
            ? inner
            : type;
    }

    private sealed class PropertyNode
    {
        public ISymbol? Symbol { get; set; }
        public IValueDomain? Domain { get; set; }
        public List<PropertyNode> Children { get; } = new();
    }

    private static void AddProperty(
        PropertyNode root,
        ImmutableArray<ISymbol> path,
        IValueDomain domain)
    {
        var current = root;
        foreach (var symbol in path)
        {
            var child = current.Children.FirstOrDefault(candidate =>
                SymbolEqualityComparer.Default.Equals(candidate.Symbol, symbol));
            if (child is null)
            {
                child = new PropertyNode { Symbol = symbol };
                current.Children.Add(child);
            }

            current = child;
        }

        current.Domain = domain;
    }

    private static bool TryRenderProperties(PropertyNode root, out string text)
    {
        var values = new List<string>();
        foreach (var child in root.Children)
        {
            if (child.Symbol is null)
            {
                text = string.Empty;
                return false;
            }

            var name = EscapeIdentifier(child.Symbol.Name);
            if (child.Domain is not null &&
                TryRenderNullOnly(child.Domain, out var childIsNull) && childIsNull)
            {
                // A null parent cannot expose any of its child paths. The
                // null witness is the complete pattern for this node.
                values.Add($"{name}: null");
                continue;
            }

            if (child.Children.Count > 0)
            {
                if (TryRenderProperties(child, out var nested) && nested.Length > 0)
                {
                    if (child.Domain is not null &&
                        TryRenderTypeDomain(child.Domain, out var typePattern))
                    {
                        values.Add($"{name}: {typePattern} {{ {nested} }}");
                    }
                    else
                    {
                        values.Add($"{name}: {{ {nested} }}");
                    }
                }
                else if (child.Domain is not null &&
                         TryRenderDomain(child.Domain, out var direct))
                    values.Add($"{name}: {direct}");
                else
                {
                    text = string.Empty;
                    return false;
                }
            }
            else if (child.Domain is not null && TryRenderDomain(child.Domain, out var value))
            {
                values.Add($"{name}: {value}");
            }
            else
            {
                text = string.Empty;
                return false;
            }
        }

        text = string.Join(", ", values);
        return true;
    }

    private static bool TryRenderDomain(IValueDomain domain, out string text)
    {
        text = string.Empty;
        if (domain is NullableDomain nullable)
        {
            if (nullable.HasNull && nullable.Inner.IsEmpty)
            {
                text = "null";
                return true;
            }

            if (!nullable.HasNull && nullable.Inner.IsUniverse)
            {
                text = "{ }";
                return true;
            }

            if (!nullable.Inner.IsEmpty && TryRenderDomain(nullable.Inner, out var inner))
            {
                text = nullable.HasNull ? $"null or {inner}" : inner;
                return true;
            }

            return false;
        }

        if (domain is BoolDomain boolDomain)
        {
            var truePart = new BoolDomain(boolDomain.Type, hasTrue: true, hasFalse: false);
            var falsePart = new BoolDomain(boolDomain.Type, hasTrue: false, hasFalse: true);
            var hasTrue = !boolDomain.Intersect(truePart).IsEmpty;
            var hasFalse = !boolDomain.Intersect(falsePart).IsEmpty;
            if (hasTrue && hasFalse)
                return false;
            if (hasTrue)
            {
                text = "true";
                return true;
            }

            if (hasFalse)
            {
                text = "false";
                return true;
            }

            return false;
        }

        if (domain is EnumDomain enumDomain)
        {
            var members = CanonicalEnumMembers(enumDomain.Members)
                .Select(member =>
                    $"{member.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.{EscapeIdentifier(member.Name)}")
                .ToArray();
            if (members.Length == 0)
                return false;
            text = members.Length == 1 ? members[0] : string.Join(" or ", members);
            return true;
        }

        if (domain is EnforceExhaustiveDomain)
            return TryRenderTypeDomain(domain, out text);

        if (domain is NumericDomain numeric)
            return TryRenderNumeric(numeric, out text);

        return false;
    }

    private static bool TryRenderTypeDomain(IValueDomain domain, out string text)
    {
        text = string.Empty;
        while (domain is NullableDomain nullable)
        {
            if (nullable.HasNull || nullable.Inner.IsEmpty)
                return false;
            domain = nullable.Inner;
        }

        if (domain is not EnforceExhaustiveDomain exhaustive ||
            exhaustive.RemainingTypes.Count != 1)
        {
            return false;
        }

        var type = exhaustive.RemainingTypes.First();
        if (type.Arity > 0 &&
            domain.Type is INamedTypeSymbol subject &&
            !TryConstructForSubject(type, subject, out type))
        {
            return false;
        }

        text = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return true;
    }

    private static IEnumerable<IFieldSymbol> CanonicalEnumMembers(
        IEnumerable<IFieldSymbol> members)
    {
        var seenValues = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in members.OrderBy(
                     value => value.Locations.FirstOrDefault()?.SourceSpan.Start ?? int.MaxValue))
        {
            var value = Convert.ToString(
                member.ConstantValue,
                System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
            if (seenValues.Add(value))
                yield return member;
        }
    }

    private static bool TryRenderNumeric(NumericDomain domain, out string text)
    {
        text = string.Empty;
        if (!domain.IsIntegral)
            return TryRenderFloating(domain, out text);

        var intervals = domain.IntegralIntervals.Intervals;
        if (intervals.IsDefaultOrEmpty)
            return false;

        var values = new List<string>();
        foreach (var interval in intervals)
        {
            if (interval.Lo == interval.Hi && interval.LoInclusive && interval.HiInclusive)
            {
                values.Add(FormatIntegral(interval.Lo, domain.Type.SpecialType));
                continue;
            }

            // Relational patterns are safe for exact integral intervals. The
            // code fix currently exercises singleton gaps; retain a compact
            // representation for wider intervals as well.
            var lower = interval.LoInclusive
                ? $">= {FormatIntegral(interval.Lo, domain.Type.SpecialType)}"
                : $"> {FormatIntegral(interval.Lo, domain.Type.SpecialType)}";
            var upper = interval.HiInclusive
                ? $"<= {FormatIntegral(interval.Hi, domain.Type.SpecialType)}"
                : $"< {FormatIntegral(interval.Hi, domain.Type.SpecialType)}";
            values.Add($"{lower} and {upper}");
        }

        if (values.Count == 0)
            return false;
        text = values.Count == 1 ? values[0] : string.Join(" or ", values);
        return true;
    }

    private static string FormatIntegral(BigInteger value, SpecialType type)
    {
        var text = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return type switch
        {
            SpecialType.System_UInt32 => text + "U",
            SpecialType.System_UInt64 => text + "UL",
            SpecialType.System_Int64 => text + "L",
            _ => text,
        };
    }

    private static bool TryRenderFloating(NumericDomain domain, out string text)
    {
        text = string.Empty;
        var intervals = domain.Intervals.Intervals;
        if (intervals.IsDefaultOrEmpty)
            return false;

        var values = new List<string>();
        foreach (var interval in intervals)
        {
            if (!TryFormatFloating(
                    interval.Lo,
                    domain.Type.SpecialType,
                    out var lowerValue) ||
                !TryFormatFloating(
                    interval.Hi,
                    domain.Type.SpecialType,
                    out var upperValue))
            {
                return false;
            }

            var lower = interval.LoInclusive
                ? $">= {lowerValue}"
                : $"> {lowerValue}";
            var upper = interval.HiInclusive
                ? $"<= {upperValue}"
                : $"< {upperValue}";

            if (interval.Lo.Equals(interval.Hi) && interval.LoInclusive && interval.HiInclusive)
                values.Add(lowerValue);
            else
                values.Add($"{lower} and {upper}");
        }

        if (values.Count == 0)
            return false;
        text = values.Count == 1 ? values[0] : string.Join(" or ", values);
        return true;
    }

    private static bool TryFormatFloating(
        double value,
        SpecialType type,
        out string text)
    {
        text = string.Empty;
        if (type == SpecialType.System_Decimal)
            return false;

        text = value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        if (text == "-0")
            text = "0";

        // C# accepts an integer-looking floating literal with the suffix. This
        // keeps generated gaps compact (`0f` rather than `0.0f`) and preserves
        // the source type of float/decimal patterns.
        text = type switch
        {
            SpecialType.System_Single => text + "f",
            _ => text + "d",
        };
        return true;
    }

    private static string EscapeIdentifier(string name) =>
        SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;
}
