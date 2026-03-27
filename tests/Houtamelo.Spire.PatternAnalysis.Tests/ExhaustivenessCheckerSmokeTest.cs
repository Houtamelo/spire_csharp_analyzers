using Houtamelo.Spire.PatternAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System.Linq;
using Xunit;

namespace Houtamelo.Spire.PatternAnalysis.Tests;

public class ExhaustivenessCheckerSmokeTest
{
    static readonly MetadataReference[] CoreReferences =
    [
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
    ];

    static (CSharpCompilation compilation, ISwitchExpressionOperation switchOp) CompileSwitchExpression(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create("Test", [tree], CoreReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var model = compilation.GetSemanticModel(tree);
        var switchSyntax = tree.GetRoot().DescendantNodes()
            .OfType<SwitchExpressionSyntax>().Single();
        var switchOp = (ISwitchExpressionOperation)model.GetOperation(switchSyntax)!;

        return (compilation, switchOp);
    }

    [Fact]
    public void BoolSwitch_AllCasesCovered_IsExhaustive()
    {
        var source = @"
public class C
{
    public int M(bool b) => b switch
    {
        true => 1,
        false => 2,
    };
}";
        var (compilation, switchOp) = CompileSwitchExpression(source);

        var result = ExhaustivenessChecker.Check(compilation, switchOp);

        Assert.True(result.MissingCases.IsEmpty);
    }

    [Fact]
    public void BoolSwitch_MissingFalse_IsNotExhaustive()
    {
        var source = @"
public class C
{
    public int M(bool b) => b switch
    {
        true => 1,
    };
}";
        var (compilation, switchOp) = CompileSwitchExpression(source);

        var result = ExhaustivenessChecker.Check(compilation, switchOp);

        Assert.False(result.MissingCases.IsEmpty);
    }

    [Fact]
    public void BoolSwitch_DiscardCoversAll_IsExhaustive()
    {
        var source = @"
public class C
{
    public int M(bool b) => b switch
    {
        true => 1,
        _ => 2,
    };
}";
        var (compilation, switchOp) = CompileSwitchExpression(source);

        var result = ExhaustivenessChecker.Check(compilation, switchOp);

        Assert.True(result.MissingCases.IsEmpty);
    }

    [Fact]
    public void BoolSwitch_OnlyDiscard_IsExhaustive()
    {
        var source = @"
public class C
{
    public int M(bool b) => b switch
    {
        _ => 1,
    };
}";
        var (compilation, switchOp) = CompileSwitchExpression(source);

        var result = ExhaustivenessChecker.Check(compilation, switchOp);

        Assert.True(result.MissingCases.IsEmpty);
    }

    [Fact]
    public void BoolSwitch_GuardedArmExcluded_IsNotExhaustive()
    {
        // The `when` guard means the `true` arm is excluded from exhaustiveness analysis.
        // Only `false` is unconditionally covered.
        var source = @"
public class C
{
    public int M(bool b) => b switch
    {
        true when 1 == 1 => 1,
        false => 2,
    };
}";
        var (compilation, switchOp) = CompileSwitchExpression(source);

        var result = ExhaustivenessChecker.Check(compilation, switchOp);

        Assert.False(result.MissingCases.IsEmpty);
    }
}
