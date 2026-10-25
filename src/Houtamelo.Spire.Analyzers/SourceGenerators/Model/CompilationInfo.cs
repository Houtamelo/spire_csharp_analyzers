using System;
using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Model;

internal sealed record CompilationInfo(
    bool HasSystemTextJson,
    bool HasNewtonsoftJson,
    bool HasInitProperties,
    bool HasUnitySerialization = false,
    bool HasUnityEditor = false
) : IEquatable<CompilationInfo>
{
    internal static bool HasAccessibleType(Compilation compilation, string metadataName)
    {
        var symbol = compilation.GetTypeByMetadataName(metadataName);
        return symbol is not null
            && compilation.IsSymbolAccessibleWithin(symbol, compilation.Assembly);
    }
}
