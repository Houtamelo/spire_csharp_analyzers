using System.Linq;
using Houtamelo.Spire.Analyzers.SourceGenerators.Emit;
using Houtamelo.Spire.Analyzers.SourceGenerators.Model;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Emit;

internal static class InlinerEmitterFormatting
{
    internal static string FormatPartialTypeDeclaration(
        string accessibility,
        string keyword,
        string name,
        EquatableArray<string> parameters)
    {
        var generics = DeclarationFormatting.FormatEscapedTypeParameters(parameters);
        var prefix = string.IsNullOrEmpty(accessibility) ? "" : accessibility + " ";
        var escapedName = IdentifierUtility.Escape(name);
        return keyword == "static class"
            ? $"{prefix}static partial class {escapedName}{generics}"
            : $"{prefix}partial {keyword} {escapedName}{generics}";
    }

    internal static void AppendConstraints(SourceBuilder sb, EquatableArray<string> constraints)
    {
        if (constraints.Length == 0)
            return;

        sb.Indent();
        foreach (var constraint in constraints)
            sb.AppendLine(constraint);
        sb.Dedent();
    }

    internal static string EscapeQualifiedName(string name)
        => string.Join(".", name.Split('.').Select(IdentifierUtility.Escape));
}
