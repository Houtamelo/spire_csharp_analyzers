using System.Text;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Emit;

/// Escapes arbitrary text for a regular C# string literal.
internal static class CSharpStringLiteral
{
    public static string Quote(string value)
        => "\"" + EscapeContent(value) + "\"";

    public static string EscapeContent(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            switch (character)
            {
                case '\\': builder.Append("\\\\"); break;
                case '"': builder.Append("\\\""); break;
                case '\0': builder.Append("\\0"); break;
                case '\a': builder.Append("\\a"); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                case '\v': builder.Append("\\v"); break;
                // These Unicode line separators are accepted as line breaks by
                // the C# lexer and therefore cannot appear literally in source.
                case '\u0085': builder.Append("\\u0085"); break;
                case '\u2028': builder.Append("\\u2028"); break;
                case '\u2029': builder.Append("\\u2029"); break;
                default:
                    if (character < ' ' || character == '\u007f')
                        builder.Append("\\u").Append(((int)character).ToString("X4"));
                    else
                        builder.Append(character);
                    break;
            }
        }

        return builder.ToString();
    }
}
