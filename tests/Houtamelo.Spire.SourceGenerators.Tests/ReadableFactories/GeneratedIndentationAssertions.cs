using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Houtamelo.Spire.SourceGenerators.Tests.ReadableFactories;

internal static class GeneratedIndentationAssertions
{
    internal static void AssertNamespaceMembersHaveNoBaseIndent(SyntaxTree tree)
    {
        var root = tree.GetRoot();
        var source = tree.GetText();

        foreach (var namespaceDeclaration in root.DescendantNodes().OfType<NamespaceDeclarationSyntax>())
        {
            foreach (var member in namespaceDeclaration.Members)
            {
                Assert.Equal(0, LeadingIndent(member, source));
                if (member is TypeDeclarationSyntax type)
                    AssertTypeMembers(type, 4, source);
            }
        }
    }

    internal static void AssertFirstNamespaceTypeIs(SyntaxTree tree, string expectedName)
    {
        var namespaceDeclaration = tree.GetRoot()
            .DescendantNodes()
            .OfType<NamespaceDeclarationSyntax>()
            .Single();
        var firstType = namespaceDeclaration.Members
            .OfType<TypeDeclarationSyntax>()
            .First();
        Assert.Equal(expectedName, firstType.Identifier.ValueText);
    }

    private static void AssertTypeMembers(TypeDeclarationSyntax type, int expectedIndent, SourceText source)
    {
        foreach (var member in type.Members)
        {
            Assert.Equal(expectedIndent, LeadingIndent(member, source));
            if (member is TypeDeclarationSyntax nestedType)
                AssertTypeMembers(nestedType, expectedIndent + 4, source);
        }
    }

    private static int LeadingIndent(SyntaxNode node, SourceText source)
    {
        var lineNumber = node.GetLocation().GetLineSpan().StartLinePosition.Line;
        var line = source.Lines[lineNumber].ToString();
        return line.TakeWhile(character => character == ' ').Count();
    }
}
