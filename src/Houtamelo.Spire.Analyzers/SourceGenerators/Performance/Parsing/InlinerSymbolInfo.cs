using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Houtamelo.Spire.Analyzers.SourceGenerators.Model;
using Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Model;
using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Parsing;

internal static class InlinerSymbolInfo
{
    internal static InlinerDiagnostic CreateDiagnostic(string id, string message, Location location)
    {
        var lineSpan = location.GetLineSpan();
        var sourceSpan = location.SourceSpan;
        var filePath = location.SourceTree?.FilePath ?? "";

        return new InlinerDiagnostic(
            Id: id,
            Message: message,
            FilePath: filePath,
            StartOffset: sourceSpan.Start,
            Length: sourceSpan.Length,
            StartLine: lineSpan.StartLinePosition.Line,
            StartColumn: lineSpan.StartLinePosition.Character,
            EndLine: lineSpan.EndLinePosition.Line,
            EndColumn: lineSpan.EndLinePosition.Character);
    }

    internal static Location GetSymbolLocation(ISymbol symbol)
    {
        var locations = symbol.Locations;
        if (!locations.IsDefaultOrEmpty)
        {
            foreach (var loc in locations)
            {
                if (loc.IsInSource)
                    return loc;
            }
        }
        return Location.None;
    }

    internal static ImmutableArray<InlinerContainingType> BuildContainingTypes(INamedTypeSymbol declaringType)
    {
        var chain = new List<InlinerContainingType>();
        var current = declaringType.ContainingType;
        while (current is not null)
        {
            chain.Add(new InlinerContainingType(
                AccessibilityKeyword: AccessibilityToKeyword(current.DeclaredAccessibility),
                Keyword: GetTypeKeyword(current),
                Name: current.Name,
                TypeParameters: new EquatableArray<string>(
                    current.TypeParameters.Select(tp => tp.Name).ToImmutableArray())));
            current = current.ContainingType;
        }
        chain.Reverse(); // outermost first
        return chain.ToImmutableArray();
    }

    internal static string GetTypeKeyword(INamedTypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Interface)
            return "interface";

        if (type.IsRecord && type.IsValueType)
            return "record struct";

        if (type.IsRecord)
            return "record";

        if (type.IsValueType)
        {
            // ref struct / readonly ref struct / readonly struct
            if (type.IsReadOnly)
                return "readonly struct";
            return "struct";
        }

        // Reference type (class)
        if (type.IsStatic)
            return "static class";

        return "class";
    }

    internal static string AccessibilityToKeyword(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Private => "private",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            _ => "internal",
        };
    }
}
