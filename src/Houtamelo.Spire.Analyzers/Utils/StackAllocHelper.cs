using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Houtamelo.Spire.Analyzers.Utils;

/// <summary>
/// Utilities for extracting information from stackalloc expressions.
/// Stackalloc has no dedicated IOperation/OperationKind in Roslyn (verified up to 5.0.0),
/// so analyzers must use RegisterSyntaxNodeAction with SyntaxKind.StackAllocArrayCreationExpression.
/// </summary>
public static class StackAllocHelper
{
    /// <summary>
    /// Gets the element type symbol from a stackalloc expression.
    /// Returns null if the type cannot be resolved.
    /// </summary>
    public static ITypeSymbol? GetElementType(
        StackAllocArrayCreationExpressionSyntax node,
        SemanticModel model)
    {
        if (node.Type is not ArrayTypeSyntax arrayType)
            return null;

        return model.GetTypeInfo(arrayType.ElementType).Type;
    }

    /// <summary>
    /// Gets the size expression from a stackalloc expression.
    /// Returns null if the size is omitted (e.g., stackalloc T[] { ... }).
    /// </summary>
    public static ExpressionSyntax? GetSizeExpression(
        StackAllocArrayCreationExpressionSyntax node)
    {
        if (node.Type is not ArrayTypeSyntax arrayType)
            return null;

        var ranks = arrayType.RankSpecifiers;
        if (ranks.Count == 0)
            return null;

        var sizes = ranks[0].Sizes;
        if (sizes.Count == 0)
            return null;

        var size = sizes[0];
        if (size is OmittedArraySizeExpressionSyntax)
            return null;

        return size;
    }

    /// <summary>
    /// Returns true if the stackalloc expression has an initializer.
    /// When an initializer is present, all elements are explicitly provided.
    /// </summary>
    public static bool HasInitializer(StackAllocArrayCreationExpressionSyntax node)
        => node.Initializer != null;
}
