using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Houtamelo.Spire.Analyzers.SourceGenerators;

internal static class RoslynCompatibility
{
    // These enum values are part of Roslyn's public API, but their named members
    // were added after the 4.3 compatibility baseline.
    private const int CSharp12LanguageVersion = 1200;
    private const int RefReadOnlyParameterKind = 4;

    public static bool IsAtLeastCSharp12(LanguageVersion languageVersion) =>
        (int)languageVersion >= CSharp12LanguageVersion;

    public static bool IsRefReadOnlyParameter(RefKind refKind) =>
        (int)refKind == RefReadOnlyParameterKind;
}
