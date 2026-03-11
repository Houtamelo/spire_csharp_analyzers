using System;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: SyntaxTreeViewer <file.cs | --stdin>");
    return 1;
}

string source;
if (args[0] == "--stdin")
{
    source = Console.In.ReadToEnd();
}
else
{
    source = File.ReadAllText(args[0]);
}

var tree = CSharpSyntaxTree.ParseText(source,
    new CSharpParseOptions(LanguageVersion.CSharp14));
var root = tree.GetRoot();

PrintNode(root, indent: 0);
return 0;

static void PrintNode(SyntaxNodeOrToken nodeOrToken, int indent)
{
    var prefix = new string(' ', indent * 2);

    if (nodeOrToken.IsNode)
    {
        var node = nodeOrToken.AsNode()!;
        Console.WriteLine($"{prefix}{node.GetType().Name} [{node.Kind()}] {node.Span}");
        foreach (var child in node.ChildNodesAndTokens())
        {
            PrintNode(child, indent + 1);
        }
    }
    else
    {
        var token = nodeOrToken.AsToken();
        var valueStr = string.IsNullOrWhiteSpace(token.ValueText) ? "" : $" \"{token.ValueText}\"";
        Console.WriteLine($"{prefix}Token [{token.Kind()}]{valueStr} {token.Span}");

        foreach (var trivia in token.LeadingTrivia)
        {
            if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia) &&
                !trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                Console.WriteLine($"{prefix}  LeadingTrivia [{trivia.Kind()}]");
            }
        }
    }
}
