using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Spire.Analyzers.Utils;

namespace Spire.Analyzers.Rules;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SPIRE004NewOfMustBeInitStructWithoutCtorAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.SPIRE004_NewOfMustBeInitStructWithoutCtor);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var mustBeInitType = compilationContext.Compilation
                .GetTypeByMetadataName("Spire.MustBeInitAttribute");

            if (mustBeInitType is null)
                return;

            compilationContext.RegisterOperationAction(
                operationContext => AnalyzeObjectCreation(operationContext, mustBeInitType),
                OperationKind.ObjectCreation);
        });
    }

    private static void AnalyzeObjectCreation(
        OperationAnalysisContext context,
        INamedTypeSymbol mustBeInitType)
    {
        var operation = (IObjectCreationOperation)context.Operation;

        // Only flag parameterless construction (no arguments)
        // A parameterized ctor call is always user-defined, so skip it
        if (operation.Arguments.Length > 0)
            return;

        // Get the type being constructed — must be a named type (not a type parameter)
        var type = operation.Type as INamedTypeSymbol;
        if (type is null)
            return;

        // Must be a struct (covers struct, record struct, readonly struct, ref struct, etc.)
        if (type.TypeKind != TypeKind.Struct)
            return;

        // Must have [MustBeInit] attribute
        if (!MustBeInitChecks.HasMustBeInitAttribute(type, mustBeInitType))
            return;

        // Must have at least one instance field (auto-property backing fields count)
        if (!MustBeInitChecks.HasInstanceFields(type))
            return;

        // Skip if the struct has a user-defined parameterless constructor
        if (HasUserDefinedParameterlessCtor(type))
            return;

        // Skip if ALL instance fields and auto-property backing fields have initializers
        if (AllInstanceFieldsHaveInitializers(type))
            return;

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptors.SPIRE004_NewOfMustBeInitStructWithoutCtor,
                operation.Syntax.GetLocation(),
                type.Name));
    }

    private static bool HasUserDefinedParameterlessCtor(INamedTypeSymbol type)
    {
        foreach (var member in type.GetMembers())
        {
            if (member is IMethodSymbol method
                && method.MethodKind == MethodKind.Constructor
                && !method.IsImplicitlyDeclared
                && method.Parameters.Length == 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns true if every instance field (including auto-property backing fields) has an initializer.
    /// If the struct has no instance fields, this returns true (vacuously true — but callers check for fields first).
    /// </summary>
    private static bool AllInstanceFieldsHaveInitializers(INamedTypeSymbol type)
    {
        foreach (var member in type.GetMembers())
        {
            if (member is not IFieldSymbol { IsStatic: false } field)
                continue;

            if (!FieldHasInitializer(field))
                return false;
        }

        return true;
    }

    private static bool FieldHasInitializer(IFieldSymbol field)
    {
        if (field.IsImplicitlyDeclared)
        {
            // Auto-property backing field — check the property declaration for an initializer
            if (field.AssociatedSymbol is IPropertySymbol property)
            {
                foreach (var syntaxRef in property.DeclaringSyntaxReferences)
                {
                    var syntax = syntaxRef.GetSyntax();
                    if (syntax is PropertyDeclarationSyntax propDecl
                        && propDecl.Initializer is not null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        else
        {
            // Explicit field declaration — check the variable declarator for an initializer
            foreach (var syntaxRef in field.DeclaringSyntaxReferences)
            {
                var syntax = syntaxRef.GetSyntax();
                if (syntax is VariableDeclaratorSyntax varDecl
                    && varDecl.Initializer is not null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
