using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Testing;
using Spire.Analyzers.Utils;
using Spire.Analyzers.Utils.FlowAnalysis;
using Xunit;

namespace Spire.Analyzers.Tests.FlowAnalysis;

public class FlowStateWalkerTests
{
    [Fact]
    public async Task DefaultThenFieldAssignments_PromotesToInitialized()
    {
        var result = await AnalyzeMethod(@"
            [Spire.MustBeInit]
            struct MyStruct { public int X; public int Y; public MyStruct(int x, int y) { X = x; Y = y; } }
            class C
            {
                static void Use(MyStruct s) { }
                void M()
                {
                    var s = default(MyStruct);
                    s.X = 1;
                    s.Y = 2;
                    Use(s);
                }
            }
        ", "M");

        Assert.NotNull(result);
    }

    [Fact]
    public async Task BranchedInit_OnePathDefault_ResultsMaybeDefault()
    {
        var result = await AnalyzeMethod(@"
            [Spire.MustBeInit]
            struct MyStruct { public int X; public MyStruct(int x) { X = x; } }
            class C
            {
                void M(bool cond)
                {
                    MyStruct s;
                    if (cond)
                        s = new MyStruct(1);
                    else
                        s = default;
                    _ = s;
                }
            }
        ", "M");

        Assert.NotNull(result);
    }

    private static async Task<FlowAnalysisResult?> AnalyzeMethod(string source, string methodName)
    {
        var refs = await ReferenceAssemblies.Net.Net80.ResolveAsync(
            LanguageNames.CSharp, CancellationToken.None);
        var coreRef = MetadataReference.CreateFromFile(typeof(Spire.MustBeInitAttribute).Assembly.Location);

        var tree = CSharpSyntaxTree.ParseText(source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest));
        var compilation = CSharpCompilation.Create("Test", new[] { tree },
            refs.Add(coreRef),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var model = compilation.GetSemanticModel(tree);
        var methodDecl = tree.GetRoot().DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.Text == methodName);

        var cfg = ControlFlowGraph.Create(methodDecl, model)!;

        var mustBeInitType = compilation.GetTypeByMetadataName("Spire.MustBeInitAttribute");
        if (mustBeInitType is null)
            return null;

        var initTypes = new System.Collections.Generic.List<INamedTypeSymbol>();
        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var sm = compilation.GetSemanticModel(syntaxTree);
            foreach (var typeDecl in syntaxTree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                var typeSymbol = sm.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
                if (typeSymbol is not null && MustBeInitChecks.HasMustBeInitAttribute(typeSymbol, mustBeInitType))
                    initTypes.Add(typeSymbol);
            }
        }

        var fieldMap = TrackedSymbolSet.BuildFieldMap(initTypes);
        var symbols = new TrackedSymbolSet(mustBeInitType, fieldMap);

        var methodSymbol = model.GetDeclaredSymbol(methodDecl)!;
        return FlowStateWalker.Analyze(cfg, symbols, methodSymbol);
    }
}
