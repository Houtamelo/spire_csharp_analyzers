// Workaround: Spire.SourceGenerators.Tests.Diagnostic namespace (from DiagnosticTests.cs)
// shadows Microsoft.CodeAnalysis.Diagnostic in the parent namespace scope.
// This global alias restores the unambiguous name for all files in the project.
global using RoslynDiagnostic = Microsoft.CodeAnalysis.Diagnostic;
