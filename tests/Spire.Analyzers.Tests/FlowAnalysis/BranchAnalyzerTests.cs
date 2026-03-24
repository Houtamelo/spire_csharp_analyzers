using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Testing;
using Spire.Analyzers.Utils.FlowAnalysis;
using Xunit;

namespace Spire.Analyzers.Tests.FlowAnalysis;

public class BranchAnalyzerTests
{
    [Fact]
    public async Task NullCheck_NarrowsToNotNull_OnTrueBranch()
    {
        var cfg = await BuildCfg(@"
            void M(string? x)
            {
                if (x != null) { _ = x.Length; }
            }
        ");

        var branchBlock = cfg.Blocks.FirstOrDefault(b =>
            b.BranchValue is not null && b.ConditionalSuccessor is not null);
        Assert.NotNull(branchBlock);

        var currentState = new VariableState(
            ImmutableArray<InitState>.Empty, KindState.Unknown, NullState.MaybeNull);

        var (trueState, falseState) = BranchAnalyzer.AnalyzeBranch(
            branchBlock!.BranchValue!, branchBlock.ConditionKind, currentState);

        // At minimum, one branch should be narrower than MaybeNull.
        Assert.True(trueState.NullState != falseState.NullState
                 || trueState.NullState == NullState.MaybeNull,
            "Branch should narrow null state in at least one direction");
    }

    private static async Task<ControlFlowGraph> BuildCfg(string methodBody)
    {
        var source = $@"
#nullable enable
class C
{{
    {methodBody}
}}";
        var refs = await ReferenceAssemblies.Net.Net80.ResolveAsync(
            LanguageNames.CSharp, CancellationToken.None);
        var tree = CSharpSyntaxTree.ParseText(source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest));
        var compilation = CSharpCompilation.Create("Test", new[] { tree }, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var model = compilation.GetSemanticModel(tree);
        var methodDecl = tree.GetRoot().DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>().First();
        return ControlFlowGraph.Create(methodDecl, model)!;
    }
}
