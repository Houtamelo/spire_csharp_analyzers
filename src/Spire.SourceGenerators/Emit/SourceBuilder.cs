using System.Text;

namespace Spire.SourceGenerators.Emit;

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

    public override string ToString() => _sb.ToString();
}
