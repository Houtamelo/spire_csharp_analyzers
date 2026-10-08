using System;
using Microsoft.CodeAnalysis.CSharp;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Emit;

internal static class IdentifierUtility
{
    public static string Escape(string name)
    {
        if (string.IsNullOrEmpty(name) || name[0] == '@')
            return name;

        return SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ||
            SyntaxFacts.GetContextualKeywordKind(name) != SyntaxKind.None
            ? "@" + name
            : name;
    }

    public static string SerializedName(string name)
        => name.StartsWith("@", StringComparison.Ordinal) ? name.Substring(1) : name;
}
