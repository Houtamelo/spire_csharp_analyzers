using Houtamelo.Spire.PatternAnalysis.Algorithm;
using Houtamelo.Spire.PatternAnalysis.Resolution;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Houtamelo.Spire.PatternAnalysis;

/// Public entry point that wires DomainResolver, PatternMatrix, and DecisionTreeBuilder together.
internal static class ExhaustivenessChecker
{
    public static ExhaustivenessResult Check(
        Compilation compilation,
        ISwitchExpressionOperation switchExpr)
    {
        var resolver = new DomainResolver(compilation, new TypeHierarchyResolver());
        var matrix = PatternMatrix.Build(switchExpr, resolver);
        return DecisionTreeBuilder.Check(matrix);
    }

    public static ExhaustivenessResult Check(
        Compilation compilation,
        ISwitchOperation switchStmt)
    {
        var resolver = new DomainResolver(compilation, new TypeHierarchyResolver());
        var matrix = PatternMatrix.Build(switchStmt, resolver);
        return DecisionTreeBuilder.Check(matrix);
    }
}
