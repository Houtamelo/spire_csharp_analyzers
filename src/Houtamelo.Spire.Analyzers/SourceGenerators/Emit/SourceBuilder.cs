using System.Text;
using Houtamelo.Spire.Analyzers.SourceGenerators.Model;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Emit;

internal sealed class SourceBuilder
{
    private readonly StringBuilder _sb = new();
    private int _indent;

    public void Indent() => _indent++;
    public void Dedent() => _indent--;

    public void AppendLine(string line)
    {
        for (int i = 0; i < _indent; i++) _sb.Append("    ");
        _sb.AppendLine(line);
    }

    public void AppendLine() => _sb.AppendLine();

    public void OpenBrace()
    {
        AppendLine("{");
        Indent();
    }

    public void CloseBrace(string suffix = "")
    {
        Dedent();
        AppendLine("}" + suffix);
    }

    /// Emits partial type declarations for the containing type chain.
    public void OpenContainingTypes(EquatableArray<ContainingTypeInfo> containingTypes)
    {
        foreach (var ct in containingTypes)
        {
            // "static class" requires "static partial class" ordering;
            // for all others, "partial {keyword}" works.
            var declaration = ct.Keyword == "static class"
                ? $"{ct.AccessibilityKeyword} static partial class {ct.Name}"
                : $"{ct.AccessibilityKeyword} partial {ct.Keyword} {ct.Name}";
            AppendLine(declaration);
            OpenBrace();
        }
    }

    /// Closes the containing type wrappers.
    public void CloseContainingTypes(EquatableArray<ContainingTypeInfo> containingTypes)
    {
        for (int i = 0; i < containingTypes.Length; i++)
            CloseBrace();
    }

    public override string ToString() => _sb.ToString();
}
