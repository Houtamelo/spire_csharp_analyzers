using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Model;

internal sealed record CompilationInfo(
    bool HasSystemTextJson,
    bool HasNewtonsoftJson,
    bool HasInitProperties,
    bool HasUnitySerialization = false,
    bool HasUnityEditor = false
) : IEquatable<CompilationInfo>
{
    internal static bool IsUnityCompilation(Compilation compilation)
        => HasPreprocessorSymbol(compilation, "UNITY_5_3_OR_NEWER");

    internal static bool HasPreprocessorSymbol(Compilation compilation, string symbol)
        => compilation.SyntaxTrees.Any(tree =>
            tree.Options is CSharpParseOptions options &&
            options.PreprocessorSymbolNames.Contains(symbol, StringComparer.Ordinal));

    internal static bool HasAccessibleType(Compilation compilation, string metadataName)
    {
        var symbol = compilation.GetTypeByMetadataName(metadataName);
        return symbol is not null
            && compilation.IsSymbolAccessibleWithin(symbol, compilation.Assembly);
    }
}
