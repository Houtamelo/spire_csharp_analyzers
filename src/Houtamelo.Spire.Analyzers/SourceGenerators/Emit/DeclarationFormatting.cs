using System.Linq;
using Houtamelo.Spire.Analyzers.SourceGenerators.Model;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Emit;

internal static class DeclarationFormatting
{
    internal static string FormatEscapedTypeParameters(EquatableArray<string> parameters)
        => parameters.Length == 0
            ? string.Empty
            : "<" + string.Join(", ", parameters.Select(IdentifierUtility.Escape)) + ">";

    internal static string FormatRawTypeParameters(EquatableArray<string> parameters)
        => parameters.Length == 0
            ? string.Empty
            : "<" + string.Join(", ", parameters) + ">";

    /// Inliner headers preserve the declared accessibility and raw identifiers.
    internal static string FormatRawPartialTypeDeclaration(
        string accessibility,
        string keyword,
        string name,
        EquatableArray<string> parameters)
    {
        var generics = FormatRawTypeParameters(parameters);
        var prefix = string.IsNullOrEmpty(accessibility) ? "" : accessibility + " ";
        return keyword == "static class"
            ? $"{prefix}static partial class {name}{generics}"
            : $"{prefix}partial {keyword} {name}{generics}";
    }

    internal static string FormatConstraints(EquatableArray<string> constraints)
        => constraints.Length == 0 ? string.Empty : " " + string.Join(" ", constraints);

    /// Produces open generic syntax: "Foo<>" for arity 1, "Foo<,>" for arity 2, etc.
    internal static string FormatOpenGenericType(string typeName, int arity)
        => arity == 0 ? typeName : typeName + "<" + new string(',', arity - 1) + ">";

    internal static string FormatAccessibility(string accessibility)
        => string.IsNullOrEmpty(accessibility) || accessibility == "private"
            ? string.Empty
            : accessibility + " ";
}
