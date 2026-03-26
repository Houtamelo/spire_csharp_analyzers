using System.Collections.Immutable;
using Houtamelo.Spire.Analyzers.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Houtamelo.Spire.Analyzers.Rules;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SPIRE004NewOfEnforceInitializationStructWithoutCtorAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.SPIRE004_NewOfEnforceInitializationStructWithoutCtor);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var enforceInitializationType = compilationContext.Compilation
                .GetTypeByMetadataName("Houtamelo.Spire.EnforceInitializationAttribute");

            if (enforceInitializationType is null)
                return;

            compilationContext.RegisterOperationAction(
                operationContext => AnalyzeObjectCreation(operationContext, enforceInitializationType),
                OperationKind.ObjectCreation);
        });
    }

    private static void AnalyzeObjectCreation(
        OperationAnalysisContext context,
        INamedTypeSymbol enforceInitializationType)
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

        if (type.TypeKind != TypeKind.Struct && type.TypeKind != TypeKind.Enum)
            return;

        if (!EnforceInitializationChecks.IsDefaultValueInvalid(type, enforceInitializationType))
            return;

        // Struct-specific: skip if user defined a parameterless ctor or all fields have initializers
        if (type.TypeKind == TypeKind.Struct)
        {
            if (HasUserDefinedParameterlessCtor(type))
                return;

            if (AllInstanceFieldsHaveInitializers(type))
                return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Descriptors.SPIRE004_NewOfEnforceInitializationStructWithoutCtor,
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
