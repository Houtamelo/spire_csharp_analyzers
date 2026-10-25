using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.Analyzers.SourceGenerators;

internal static class RoslynCompatibility
{
    // This enum value's named member was added after the 4.3 baseline.
    private const int RefReadOnlyParameterKind = 4;

    public static bool IsRefReadOnlyParameter(RefKind refKind) =>
        (int)refKind == RefReadOnlyParameterKind;
}
