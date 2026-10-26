using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Parsing;

internal static class InlinerHintNaming
{
    private const int ReadablePrefixLimit = 80;
    private const string Hex = "0123456789abcdef";

    internal static string ForInlinerStruct(IMethodSymbol method)
        => Create(method, "InlinerStruct");

    internal static string ForInlinableTwin(IMethodSymbol method)
        => Create(method, "InlinableTwin");

    private static string Create(IMethodSymbol method, string emitterSuffix)
    {
        var canonicalId = method.GetDocumentationCommentId();
        if (string.IsNullOrEmpty(canonicalId))
            canonicalId = BuildFallbackIdentity(method);

        var nonNullCanonicalId = canonicalId!;
        var readable = ToReadablePrefix(nonNullCanonicalId);
        var digest = Sha256(nonNullCanonicalId);
        return $"{readable}.{digest}.{emitterSuffix}.g.cs";
    }

    private static string BuildFallbackIdentity(IMethodSymbol method)
    {
        var parts = new List<string>();
        if (!method.ContainingNamespace.IsGlobalNamespace)
            parts.Add(method.ContainingNamespace.ToDisplayString());

        var containingTypes = new Stack<string>();
        for (var containing = method.ContainingType;
             containing is not null;
             containing = containing.ContainingType)
        {
            containingTypes.Push(containing.MetadataName);
        }

        parts.AddRange(containingTypes);
        parts.Add(method.MetadataName);

        var parameters = method.Parameters.Select(parameter => parameter.Type.ToDisplayString(
            SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
                SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions |
                SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier)));
        return $"M:{string.Join(".", parts)}({string.Join(",", parameters)})";
    }

    private static string ToReadablePrefix(string canonicalId)
    {
        var builder = new StringBuilder(Math.Min(canonicalId.Length, ReadablePrefixLimit));
        foreach (var character in canonicalId)
        {
            if (builder.Length == ReadablePrefixLimit)
                break;

            builder.Append(IsAsciiIdentifierCharacter(character) ? character : '_');
        }

        return builder.Length == 0 ? "Inliner" : builder.ToString();
    }

    private static bool IsAsciiIdentifierCharacter(char character)
        => character is >= 'A' and <= 'Z'
            or >= 'a' and <= 'z'
            or >= '0' and <= '9'
            or '_';

    private static string Sha256(string value)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
        var builder = new StringBuilder(bytes.Length * 2);
        foreach (var valueByte in bytes)
        {
            builder.Append(Hex[valueByte >> 4]);
            builder.Append(Hex[valueByte & 0x0f]);
        }

        return builder.ToString();
    }
}
