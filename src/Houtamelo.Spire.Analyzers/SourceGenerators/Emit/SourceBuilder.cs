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

    public void EnsureBlankLine()
    {
        if (_sb.Length == 0)
            return;

        while (_sb.Length > 0)
        {
            var character = _sb[^1];
            if (character is not ('\r' or '\n' or ' ' or '\t'))
                break;
            _sb.Length--;
        }

        _sb.AppendLine();
        _sb.AppendLine();
    }

    public void OpenNamespace(string namespaceName)
        => AppendLine($"namespace {namespaceName} {{");

    public void CloseNamespace()
        => AppendLine("}");

    public void OpenBrace()
    {
        if (_sb.Length > 0 && _sb[^1] == '\n')
        {
            _sb.Length--;
            if (_sb.Length > 0 && _sb[^1] == '\r')
                _sb.Length--;
            _sb.Append(" {");
            _sb.AppendLine();
        }
        else
        {
            AppendLine("{");
        }
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
            var accessibility = ct.AccessibilityKeyword == "private"
                ? string.Empty
                : ct.AccessibilityKeyword;
            var accessPrefix = string.IsNullOrEmpty(accessibility)
                ? string.Empty
                : accessibility + " ";
            var declaration = ct.Keyword == "static class"
                ? $"{accessPrefix}static partial class {IdentifierUtility.Escape(ct.Name)}{FormatTypeParams(ct.TypeParameters)}"
                : $"{accessPrefix}partial {ct.Keyword} {IdentifierUtility.Escape(ct.Name)}{FormatTypeParams(ct.TypeParameters)}";
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

    private static string FormatTypeParams(EquatableArray<string> typeParameters)
        => typeParameters.Length == 0
            ? string.Empty
            : "<" + string.Join(", ", typeParameters) + ">";
}
