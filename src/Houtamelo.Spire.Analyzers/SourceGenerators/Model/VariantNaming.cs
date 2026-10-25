using System;
using System.Collections.Generic;
using System.Linq;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Model;

internal static class VariantNaming
{
    public static string AccessorName(string variantName, IEnumerable<string> factoryNames)
    {
        var name = FieldNameResolver.CamelCase(variantName);
        return factoryNames.Contains(name, StringComparer.Ordinal) ? name + "_value" : name;
    }
}
