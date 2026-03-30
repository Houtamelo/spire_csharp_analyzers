using System;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Houtamelo.Spire.Analyzers.Utils;

internal static class GlobalConfigHelper
{
    public static bool ReadEnforceExhaustivenessOnAllEnumTypes(AnalyzerOptions options)
    {
        if (options.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue(
                "build_property.Spire_EnforceExhaustivenessOnAllEnumTypes", out var value))
        {
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}
