using System.ComponentModel;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModelContextProtocol.Server;

namespace DevTools;

[McpServerToolType]
public static class SyntaxTreeTool
{
    [McpServerTool(Name = "parse_syntax_tree")]
    [Description("Parse C# source code and return the Roslyn syntax tree. Use to discover SyntaxNode types, SyntaxKind values, and tree structure for analyzer development.")]
    public static string ParseSyntaxTree(
        [Description("C# source code to parse")] string code)
    {
        var tree = CSharpSyntaxTree.ParseText(code,
            new CSharpParseOptions(LanguageVersion.CSharp14));
        var root = tree.GetRoot();

        var sb = new StringBuilder();
        PrintNode(sb, root, indent: 0);
        return sb.ToString();
    }

    static void PrintNode(StringBuilder sb, SyntaxNodeOrToken nodeOrToken, int indent)
    {
        var prefix = new string(' ', indent * 2);

        if (nodeOrToken.IsNode)
        {
            var node = nodeOrToken.AsNode()!;
            sb.AppendLine($"{prefix}{node.GetType().Name} [{node.Kind()}] {node.Span}");
            foreach (var child in node.ChildNodesAndTokens())
            {
                PrintNode(sb, child, indent + 1);
            }
        }
        else
        {
            var token = nodeOrToken.AsToken();
            var valueStr = string.IsNullOrWhiteSpace(token.ValueText) ? "" : $" \"{token.ValueText}\"";
            sb.AppendLine($"{prefix}Token [{token.Kind()}]{valueStr} {token.Span}");

            foreach (var trivia in token.LeadingTrivia)
            {
                if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia) &&
                    !trivia.IsKind(SyntaxKind.EndOfLineTrivia))
                {
                    sb.AppendLine($"{prefix}  LeadingTrivia [{trivia.Kind()}]");
                }
            }
        }
    }
}
