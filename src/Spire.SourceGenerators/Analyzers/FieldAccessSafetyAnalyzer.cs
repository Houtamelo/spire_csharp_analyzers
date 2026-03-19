using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Spire.SourceGenerators.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FieldAccessSafetyAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            AnalyzerDescriptors.SPIRE013_WrongVariantFieldAccess,
            AnalyzerDescriptors.SPIRE014_UnguardedFieldAccess);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(compilationCtx =>
        {
            var duAttr = compilationCtx.Compilation
                .GetTypeByMetadataName("Spire.DiscriminatedUnionAttribute");
            if (duAttr is null) return;

            compilationCtx.RegisterOperationAction(
                ctx => AnalyzeFieldReference(ctx, duAttr),
                OperationKind.FieldReference);
        });
    }

    private static void AnalyzeFieldReference(OperationAnalysisContext ctx, INamedTypeSymbol duAttr)
    {
        var fieldRefOp = (IFieldReferenceOperation)ctx.Operation;
        var field = fieldRefOp.Field;
        var containingType = field.ContainingType;

        // Must be a value type with [DiscriminatedUnion]
        if (containingType is null || !containingType.IsValueType)
            return;

        var info = UnionTypeInfo.TryCreate(containingType, duAttr);
        if (info is null)
            return;

        // Must have [EditorBrowsable(EditorBrowsableState.Never)] — variant fields have this,
        // tag and other public fields do not.
        if (!HasEditorBrowsableNever(field))
            return;

        // Build variant field map and find which variant owns this field
        var fieldMap = VariantFieldMap.TryCreate(containingType, info);
        if (fieldMap is null)
            return;

        var owningVariant = fieldMap.GetOwningVariant(field.Name);
        if (owningVariant is null)
            return;

        // If the field reference is inside a property subpattern, it's inherently guarded
        if (IsInsidePropertySubpattern(fieldRefOp))
            return;

        // Walk parent chain to determine if there's a tag guard
        if (HasTagGuard(fieldRefOp))
            return;

        // No guard found — report SPIRE014
        ctx.ReportDiagnostic(Diagnostic.Create(
            AnalyzerDescriptors.SPIRE014_UnguardedFieldAccess,
            fieldRefOp.Syntax.GetLocation(),
            field.Name));
    }

    private static bool HasEditorBrowsableNever(IFieldSymbol field)
    {
        return field.GetAttributes().Any(a =>
            a.AttributeClass?.Name == "EditorBrowsableAttribute" &&
            a.ConstructorArguments.Length > 0 &&
            a.ConstructorArguments[0].Value is int val &&
            val == 1); // EditorBrowsableState.Never = 1
    }

    /// Checks whether the field reference is within an IPropertySubpatternOperation.
    /// Property patterns like { circle_radius: var r } are inherently guarded.
    private static bool IsInsidePropertySubpattern(IOperation operation)
    {
        var current = operation.Parent;
        while (current is not null)
        {
            if (current is IPropertySubpatternOperation)
                return true;
            // Stop at meaningful scope boundaries
            if (current is IMethodBodyOperation or IBlockOperation or IAnonymousFunctionOperation)
                break;
            current = current.Parent;
        }
        return false;
    }

    /// Walks the parent chain to find a tag guard (switch arm, switch case, or if-condition).
    private static bool HasTagGuard(IFieldReferenceOperation fieldRefOp)
    {
        var current = fieldRefOp.Parent;
        while (current is not null)
        {
            switch (current)
            {
                case ISwitchExpressionArmOperation:
                    // Inside a switch expression arm — guarded by the arm's pattern
                    return true;

                case ISwitchCaseOperation:
                    // Inside a switch statement case — guarded by the case clause
                    return true;

                case IConditionalOperation conditional:
                    // Inside an if-statement — check if the condition guards the field access
                    // and the field access is in the WhenTrue branch
                    if (IsInWhenTrueBranch(fieldRefOp, conditional) &&
                        ConditionReferencesUnionVariable(conditional.Condition, fieldRefOp))
                        return true;
                    break;

                // Stop at method/lambda scope boundaries
                case IMethodBodyOperation:
                case IAnonymousFunctionOperation:
                    return false;
            }

            current = current.Parent;
        }

        return false;
    }

    /// Checks whether the field reference operation is within the WhenTrue branch of a conditional.
    private static bool IsInWhenTrueBranch(IOperation fieldRef, IConditionalOperation conditional)
    {
        if (conditional.WhenTrue is null)
            return false;

        // Walk up from fieldRef to see if we pass through WhenTrue before hitting conditional
        var current = fieldRef;
        while (current is not null && current != conditional)
        {
            if (current == conditional.WhenTrue)
                return true;
            current = current.Parent;
        }
        return false;
    }

    /// Checks whether the condition references the same union variable as the field access.
    /// Handles: is-pattern (s is { tag: Kind.Circle }), tag comparison (s.tag == Kind.Circle).
    private static bool ConditionReferencesUnionVariable(IOperation condition, IFieldReferenceOperation fieldRef)
    {
        var targetInstance = fieldRef.Instance;
        if (targetInstance is null)
            return false;

        switch (condition)
        {
            case IIsPatternOperation isPattern:
                return ReferenceSameVariable(isPattern.Value, targetInstance);

            case IBinaryOperation binary when
                binary.OperatorKind == BinaryOperatorKind.Equals ||
                binary.OperatorKind == BinaryOperatorKind.NotEquals:
                // Check if either side is a .tag access on the same variable
                return IsTagAccessOnSameVariable(binary.LeftOperand, targetInstance) ||
                       IsTagAccessOnSameVariable(binary.RightOperand, targetInstance);

            default:
                return false;
        }
    }

    /// Checks whether an operation is a .tag field/property access on the same variable.
    private static bool IsTagAccessOnSameVariable(IOperation operation, IOperation targetInstance)
    {
        if (operation is IFieldReferenceOperation tagFieldRef &&
            tagFieldRef.Field.Name == "tag" &&
            tagFieldRef.Instance is not null)
        {
            return ReferenceSameVariable(tagFieldRef.Instance, targetInstance);
        }

        if (operation is IPropertyReferenceOperation tagPropRef &&
            tagPropRef.Property.Name == "tag" &&
            tagPropRef.Instance is not null)
        {
            return ReferenceSameVariable(tagPropRef.Instance, targetInstance);
        }

        return false;
    }

    /// Compares two operations to determine if they reference the same variable.
    private static bool ReferenceSameVariable(IOperation a, IOperation b)
    {
        // Both are local references to the same local
        if (a is ILocalReferenceOperation localA &&
            b is ILocalReferenceOperation localB)
        {
            return SymbolEqualityComparer.Default.Equals(localA.Local, localB.Local);
        }

        // Both are parameter references to the same parameter
        if (a is IParameterReferenceOperation paramA &&
            b is IParameterReferenceOperation paramB)
        {
            return SymbolEqualityComparer.Default.Equals(paramA.Parameter, paramB.Parameter);
        }

        // Both are field references to the same field (e.g., this.field)
        if (a is IFieldReferenceOperation fieldA &&
            b is IFieldReferenceOperation fieldB)
        {
            return SymbolEqualityComparer.Default.Equals(fieldA.Field, fieldB.Field);
        }

        return false;
    }
}
