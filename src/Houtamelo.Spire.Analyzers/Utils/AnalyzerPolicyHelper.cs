using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Houtamelo.Spire.Analyzers.Utils;

internal static class AnalyzerPolicyHelper
{
    public static bool TryGetInitializationPolicy(
        Compilation compilation,
        AnalyzerOptions options,
        [NotNullWhen(true)] out INamedTypeSymbol? enforceInitializationType,
        out bool enforceOnAllEnums)
        => TryGetPolicy(
            compilation,
            options,
            "Houtamelo.Spire.EnforceInitializationAttribute",
            out enforceInitializationType,
            out enforceOnAllEnums);

    public static bool TryGetExhaustivenessPolicy(
        Compilation compilation,
        AnalyzerOptions options,
        [NotNullWhen(true)] out INamedTypeSymbol? enforceExhaustivenessType,
        out bool enforceOnAllEnums)
        => TryGetPolicy(
            compilation,
            options,
            "Houtamelo.Spire.EnforceExhaustivenessAttribute",
            out enforceExhaustivenessType,
            out enforceOnAllEnums);

    private static bool TryGetPolicy(
        Compilation compilation,
        AnalyzerOptions options,
        string attributeMetadataName,
        [NotNullWhen(true)] out INamedTypeSymbol? attributeType,
        out bool enforceOnAllEnums)
    {
        attributeType = compilation.GetTypeByMetadataName(attributeMetadataName);
        if (attributeType is null)
        {
            enforceOnAllEnums = false;
            return false;
        }

        enforceOnAllEnums = GlobalConfigHelper.ReadEnforceExhaustivenessOnAllEnumTypes(options);
        return true;
    }
}
