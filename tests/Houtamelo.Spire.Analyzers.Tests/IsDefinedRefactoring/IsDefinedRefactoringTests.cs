using System.Collections.Immutable;
using Houtamelo.Spire.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;

namespace Houtamelo.Spire.Analyzers.Tests;

public class IsDefinedRefactoringTests : RefactoringTestBase
{
    protected override string Category => "IsDefinedRefactoring";

    protected override ImmutableArray<CodeRefactoringProvider> GetRefactoringProviders() =>
        ImmutableArray.Create<CodeRefactoringProvider>(new IsDefinedRefactoring());
}
