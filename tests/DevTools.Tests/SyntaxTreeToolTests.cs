using DevTools;
using Xunit;

namespace DevTools.Tests;

public sealed class SyntaxTreeToolTests
{
    [Fact]
    public void ParseSyntaxTree_ReportsCollectionExpressionStructure()
    {
        var output = SyntaxTreeTool.ParseSyntaxTree("int[] values = [1, 2, 3];");

        Assert.Contains("CompilationUnitSyntax [CompilationUnit]", output);
        Assert.Contains("CollectionExpressionSyntax [CollectionExpression]", output);
        Assert.Contains("OpenBracketToken", output);
        Assert.Contains("Token [IdentifierToken] \"values\"", output);
        Assert.Contains("Token [NumericLiteralToken] \"1\"", output);
        Assert.Contains("Token [NumericLiteralToken] \"2\"", output);
        Assert.Contains("Token [NumericLiteralToken] \"3\"", output);
    }

    [Fact]
    public void ParseSyntaxTree_ReportsRecoveryNodesForMalformedSource()
    {
        var output = SyntaxTreeTool.ParseSyntaxTree("class Broken {");

        Assert.Contains("CompilationUnitSyntax [CompilationUnit]", output);
        Assert.Contains("ClassDeclarationSyntax [ClassDeclaration]", output);
        Assert.Contains("Token [IdentifierToken] \"Broken\"", output);
        Assert.Contains("CloseBraceToken", output);
    }

    [Fact]
    public void ParseSyntaxTree_ReturnsCompilationUnitForEmptySource()
    {
        var output = SyntaxTreeTool.ParseSyntaxTree(string.Empty);

        Assert.Contains("CompilationUnitSyntax [CompilationUnit] [0..0)", output);
    }
}
