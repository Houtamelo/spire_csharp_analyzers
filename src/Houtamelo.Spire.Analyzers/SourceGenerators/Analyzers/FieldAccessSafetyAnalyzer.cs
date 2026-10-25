using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Houtamelo.Spire.PatternAnalysis;
using Houtamelo.Spire.Analyzers.SourceGenerators.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Analyzers;

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
                .GetTypeByMetadataName("Houtamelo.Spire.DiscriminatedUnionAttribute");
            if (duAttr is null) return;

            compilationCtx.RegisterOperationAction(
                ctx => AnalyzeFieldReference(ctx, duAttr),
                OperationKind.FieldReference);

            compilationCtx.RegisterOperationAction(
                ctx => AnalyzePropertyReference(ctx, duAttr),
                OperationKind.PropertyReference);
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
        // kind and other public fields do not.
        if (!HasEditorBrowsableNever(field))
            return;

        // Build variant field map and find which variant owns this field
        var fieldMap = VariantFieldMap.TryCreate(containingType, info);
        if (fieldMap is null)
            return;

        var owningVariants = fieldMap.GetOwningVariants(field.Name);
        if (owningVariants.IsEmpty)
            return;

        // Payload fields inside a property pattern are ordinary fields and need
        // no discriminator proof. A variant accessor itself is still a union
        // read, even when it appears as the selected property in a pattern.
        if (IsInsidePropertySubpattern(fieldRefOp))
            return;

        // Walk parent chain to find a guard and extract guarded variants
        var guardResult = FindGuard(fieldRefOp, info);

        if (!guardResult.HasGuard)
        {
            // No guard found — report SPIRE014
            ctx.ReportDiagnostic(Diagnostic.Create(
                AnalyzerDescriptors.SPIRE014_UnguardedFieldAccess,
                fieldRefOp.Syntax.GetLocation(),
                field.Name));
            return;
        }

        // Every possible guarded variant must expose this member. An
        // intersection is insufficient when a guard covers multiple kinds.
        if (guardResult.GuardedVariants.All(owningVariants.Contains))
            return; // Correct variant — no diagnostic

        // Wrong variant — report SPIRE013
        var guardedDisplay = string.Join(", ", guardResult.GuardedVariants);
        ctx.ReportDiagnostic(Diagnostic.Create(
            AnalyzerDescriptors.SPIRE013_WrongVariantFieldAccess,
            fieldRefOp.Syntax.GetLocation(),
            field.Name,
            string.Join(" or ", owningVariants),
            guardedDisplay));
    }

    private static void AnalyzePropertyReference(OperationAnalysisContext ctx, INamedTypeSymbol duAttr)
    {
        var propRefOp = (IPropertyReferenceOperation)ctx.Operation;
        var property = propRefOp.Property;
        var containingType = property.ContainingType;

        if (containingType is null || !containingType.IsValueType)
            return;

        var info = UnionTypeInfo.TryCreate(containingType, duAttr);
        if (info is null)
            return;

        if (!HasEditorBrowsableNever(property))
            return;

        var fieldMap = VariantFieldMap.TryCreate(containingType, info);
        if (fieldMap is null)
            return;

        var owningVariants = fieldMap.GetOwningVariants(property.Name);
        if (owningVariants.IsEmpty)
            return;

        if (IsInsidePropertySubpattern(propRefOp) &&
            !IsVariantAccessor(property.Name, info))
            return;

        var guardResult = FindGuard(propRefOp, info);

        if (!guardResult.HasGuard)
        {
            ctx.ReportDiagnostic(Diagnostic.Create(
                AnalyzerDescriptors.SPIRE014_UnguardedFieldAccess,
                propRefOp.Syntax.GetLocation(),
                property.Name));
            return;
        }

        if (guardResult.GuardedVariants.All(owningVariants.Contains))
            return;

        var guardedDisplay = string.Join(", ", guardResult.GuardedVariants);
        ctx.ReportDiagnostic(Diagnostic.Create(
            AnalyzerDescriptors.SPIRE013_WrongVariantFieldAccess,
            propRefOp.Syntax.GetLocation(),
            property.Name,
            string.Join(" or ", owningVariants),
            guardedDisplay));
    }

    private static bool HasEditorBrowsableNever(ISymbol symbol)
    {
        return symbol.GetAttributes().Any(a =>
            a.AttributeClass?.Name == "EditorBrowsableAttribute" &&
            a.ConstructorArguments.Length > 0 &&
            a.ConstructorArguments[0].Value is int val &&
            val == 1); // EditorBrowsableState.Never = 1
    }

    private static bool IsVariantAccessor(string propertyName, UnionTypeInfo info)
    {
        foreach (var variantName in info.VariantNames)
        {
            if (string.Equals(
                    propertyName,
                    VariantNaming.AccessorName(variantName, info.VariantNames),
                    System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
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

    /// Result of finding a guard in the parent chain.
    private readonly struct GuardResult
    {
        public static readonly GuardResult NoGuard = new(ImmutableHashSet<string>.Empty, false);

        public ImmutableHashSet<string> GuardedVariants { get; }
        public bool HasGuard { get; }

        private GuardResult(ImmutableHashSet<string> guardedVariants, bool hasGuard)
        {
            GuardedVariants = guardedVariants;
            HasGuard = hasGuard;
        }

        public static GuardResult WithVariants(IEnumerable<string> variants)
        {
            var set = variants.ToImmutableHashSet();
            return set.Count == 0 ? NoGuard : new(set, true);
        }
    }

    /// Walks the parent chain to find a kind guard and extract guarded variants.
    private static GuardResult FindGuard(IOperation memberRefOp, UnionTypeInfo info)
    {
        if (memberRefOp is IPropertyReferenceOperation propertyReference &&
            TryGetFactoryVariant(propertyReference.Instance, info, out var factoryVariant))
        {
            return GuardResult.WithVariants(new[] { factoryVariant });
        }

        var current = memberRefOp.Parent;
        while (current is not null)
        {
            switch (current)
            {
                case IRecursivePatternOperation recursive:
                    {
                        var variants = new List<string>();
                        ExhaustivenessChecker.CollectVariants(
                            recursive,
                            info.KindEnumType,
                            info.VariantNames,
                            info.VariantTypes,
                            info.IsStructUnion,
                            variants);
                        var guard = GuardResult.WithVariants(variants);
                        if (guard.HasGuard)
                            return guard;
                        break;
                    }

                case ISwitchExpressionArmOperation arm:
                    if (!SwitchGuardReferencesSameInstance(
                            arm.Parent as ISwitchExpressionOperation,
                            memberRefOp) ||
                        HasMutationBefore(memberRefOp, arm, memberRefOp))
                    {
                        break;
                    }

                    {
                        var variants = new List<string>();
                        ExhaustivenessChecker.CollectVariants(
                            arm.Pattern, info.KindEnumType, info.VariantNames,
                            info.VariantTypes, info.IsStructUnion, variants);
                        var guard = GuardResult.WithVariants(variants);
                        if (guard.HasGuard)
                            return guard;
                        break;
                    }

                case ISwitchCaseOperation switchCase:
                    if (!SwitchGuardReferencesSameInstance(
                            FindEnclosingSwitch(switchCase),
                            memberRefOp) ||
                        HasMutationBefore(memberRefOp, switchCase, memberRefOp))
                    {
                        break;
                    }

                    {
                        var variants = new List<string>();
                        foreach (var clause in switchCase.Clauses)
                        {
                            if (clause is IPatternCaseClauseOperation patternClause)
                                ExhaustivenessChecker.CollectVariants(
                                    patternClause.Pattern, info.KindEnumType, info.VariantNames,
                                    info.VariantTypes, info.IsStructUnion, variants);
                            else if (clause is ISingleValueCaseClauseOperation singleValueClause)
                                TryExtractKindVariant(singleValueClause.Value, info, variants);
                        }
                        var guard = GuardResult.WithVariants(variants);
                        if (guard.HasGuard)
                            return guard;
                        break;
                    }

                case IConditionalOperation conditional:
                    if (IsInWhenTrueBranch(memberRefOp, conditional) &&
                        ConditionReferencesUnionVariable(conditional.Condition, memberRefOp) &&
                        !HasMutationBefore(memberRefOp, conditional.WhenTrue, memberRefOp))
                    {
                        var variants = ExtractVariantsFromCondition(conditional.Condition, info);
                        var guard = GuardResult.WithVariants(variants);
                        if (guard.HasGuard)
                            return guard;
                        break;
                    }
                    break;

                case IBlockOperation block:
                    {
                        var priorGuard = FindPriorExitGuard(memberRefOp, block, info);
                        if (priorGuard.HasGuard)
                            return priorGuard;
                        break;
                    }

                // Stop at method/lambda scope boundaries
                case IMethodBodyOperation:
                case IAnonymousFunctionOperation:
                    return GuardResult.NoGuard;
            }

            current = current.Parent;
        }

        return GuardResult.NoGuard;
    }

    private static bool TryGetFactoryVariant(
        IOperation? instance,
        UnionTypeInfo info,
        out string variantName)
    {
        variantName = string.Empty;
        if (instance is not IInvocationOperation invocation ||
            !invocation.TargetMethod.IsStatic ||
            !HasVariantAttribute(invocation.TargetMethod))
        {
            return false;
        }

        variantName = invocation.TargetMethod.Name;
        return info.VariantNames.Contains(variantName);
    }

    private static bool HasVariantAttribute(IMethodSymbol method)
        => method.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.Name == "VariantAttribute");

    private static GuardResult FindPriorExitGuard(
        IOperation memberReference,
        IBlockOperation block,
        UnionTypeInfo info)
    {
        foreach (var operation in block.Operations)
        {
            if (operation.Syntax.Span.End >= memberReference.Syntax.SpanStart)
                break;

            if (operation is not IConditionalOperation conditional ||
                !ConditionReferencesUnionVariable(conditional.Condition, memberReference) ||
                !AllPathsTerminate(conditional.WhenTrue))
            {
                continue;
            }

            var variants = new List<string>();
            if (conditional.Condition is IBinaryOperation binary &&
                binary.OperatorKind == BinaryOperatorKind.NotEquals)
            {
                // The exiting branch is the `!=` branch. The following code
                // therefore runs only for the excluded, equal variant.
                TryExtractKindVariant(binary.LeftOperand, info, variants);
                TryExtractKindVariant(binary.RightOperand, info, variants);
            }
            if (variants.Count > 0 &&
                !HasMutationAfterGuard(block, conditional, memberReference))
                return GuardResult.WithVariants(variants);
        }

        return GuardResult.NoGuard;
    }

    /// Checks whether every path through an operation returns or throws.
    /// This deliberately does not inspect nested functions: their control flow
    /// is independent of the containing method.
    private static bool AllPathsTerminate(IOperation operation)
    {
        switch (operation)
        {
            case IReturnOperation:
            case IThrowOperation:
                return true;

            case IBlockOperation block when block.Operations.Length > 0:
                return AllPathsTerminate(block.Operations[block.Operations.Length - 1]);

            case IConditionalOperation conditional when conditional.WhenFalse is not null:
                return AllPathsTerminate(conditional.WhenTrue) &&
                       AllPathsTerminate(conditional.WhenFalse);

            default:
                return false;
        }
    }

    private static bool HasMutationAfterGuard(
        IBlockOperation block,
        IConditionalOperation guard,
        IOperation memberReference)
    {
        var targetSymbol = ReferencedSymbol(TargetInstance(memberReference));
        if (targetSymbol is null)
            return false;

        foreach (var operation in block.Operations)
        {
            if (operation == guard)
                continue;

            if (operation.Syntax.SpanStart <= guard.Syntax.Span.End)
                continue;

            if (operation.Syntax.SpanStart >= memberReference.Syntax.SpanStart)
                break;

            if (HasMutation(
                    operation,
                    targetSymbol,
                    guard.Syntax.Span.End,
                    memberReference.Syntax.SpanStart))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasMutation(
        IOperation scope,
        ISymbol targetSymbol,
        int minimumSpanStart,
        int maximumSpanStart)
    {
        if (scope is IAnonymousFunctionOperation or ILocalFunctionOperation)
            return false;

        foreach (var operation in scope.DescendantsAndSelf())
        {
            if (operation.Syntax.SpanStart < minimumSpanStart ||
                operation.Syntax.SpanStart >= maximumSpanStart ||
                IsNestedFunction(operation, scope))
            {
                continue;
            }

            if (IsMutationOf(operation, targetSymbol))
                return true;
        }

        return false;
    }

    private static bool IsNestedFunction(IOperation operation, IOperation scope)
    {
        var current = operation.Parent;
        while (current is not null && current != scope)
        {
            if (current is IAnonymousFunctionOperation or ILocalFunctionOperation)
                return true;

            current = current.Parent;
        }

        return false;
    }

    private static bool IsMutationOf(IOperation operation, ISymbol targetSymbol)
    {
        IOperation? target = operation switch
        {
            ISimpleAssignmentOperation assignment => assignment.Target,
            ICompoundAssignmentOperation compound => compound.Target,
            _ => null,
        };

        return target is not null && MutationTargetsSymbol(target, targetSymbol);
    }

    private static bool MutationTargetsSymbol(IOperation target, ISymbol targetSymbol)
    {
        if (SymbolEqualityComparer.Default.Equals(
                ReferencedSymbol(target),
                targetSymbol))
        {
            return true;
        }

        var targetInstance = target switch
        {
            IFieldReferenceOperation field when field.Instance is not null => field.Instance,
            IPropertyReferenceOperation property when property.Instance is not null => property.Instance,
            _ => null,
        };

        return targetInstance is not null &&
               SymbolEqualityComparer.Default.Equals(
                   ReferencedSymbol(targetInstance),
                   targetSymbol);
    }

    private static bool HasMutationBefore(
        IOperation memberReference,
        IOperation branch,
        IOperation targetReference)
    {
        var targetSymbol = ReferencedSymbol(TargetInstance(targetReference));
        if (targetSymbol is null)
            return false;

        foreach (var operation in branch.DescendantsAndSelf())
        {
            if (operation.Syntax.SpanStart >= memberReference.Syntax.SpanStart)
                continue;

            if (IsNestedFunction(operation, branch))
                continue;

            if (IsMutationOf(operation, targetSymbol))
            {
                return true;
            }
        }

        return false;
    }

    private static ISwitchOperation? FindEnclosingSwitch(IOperation operation)
    {
        var current = operation.Parent;
        while (current is not null)
        {
            if (current is ISwitchOperation switchOperation)
                return switchOperation;

            if (current is IMethodBodyOperation or IAnonymousFunctionOperation)
                return null;

            current = current.Parent;
        }

        return null;
    }

    private static bool SwitchGuardReferencesSameInstance(
        ISwitchOperation? switchOperation,
        IOperation memberReference)
    {
        return switchOperation is not null &&
               SwitchValueReferencesSameInstance(switchOperation.Value, memberReference);
    }

    private static bool SwitchGuardReferencesSameInstance(
        ISwitchExpressionOperation? switchOperation,
        IOperation memberReference)
    {
        return switchOperation is not null &&
               SwitchValueReferencesSameInstance(switchOperation.Value, memberReference);
    }

    private static bool SwitchValueReferencesSameInstance(
        IOperation switchValue,
        IOperation memberReference)
    {
        while (switchValue is IConversionOperation conversion)
            switchValue = conversion.Operand;

        var targetInstance = TargetInstance(memberReference);
        if (targetInstance is null)
            return false;

        if (switchValue is IFieldReferenceOperation or IPropertyReferenceOperation)
        {
            if (IsKindAccessOnSameVariable(switchValue, targetInstance))
                return true;
        }

        return ReferenceSameVariable(switchValue, targetInstance);
    }

    private static IOperation TargetInstance(IOperation operation)
        => operation switch
        {
            IPropertyReferenceOperation property when property.Instance is not null => property.Instance,
            IFieldReferenceOperation field when field.Instance is not null => field.Instance,
            _ => operation,
        };

    private static ISymbol? ReferencedSymbol(IOperation operation)
    {
        while (operation is IConversionOperation conversion)
            operation = conversion.Operand;

        return operation switch
        {
            ILocalReferenceOperation local => local.Local,
            IParameterReferenceOperation parameter => parameter.Parameter,
            IFieldReferenceOperation field => field.Field,
            _ => null,
        };
    }

    /// Extracts guarded variant names from an if-condition.
    private static List<string> ExtractVariantsFromCondition(IOperation condition, UnionTypeInfo info)
    {
        var variants = new List<string>();

        switch (condition)
        {
            case IIsPatternOperation isPattern:
                ExhaustivenessChecker.CollectVariants(
                    isPattern.Pattern, info.KindEnumType, info.VariantNames,
                    info.VariantTypes, info.IsStructUnion, variants);
                break;

            case IBinaryOperation binary when
                binary.OperatorKind == BinaryOperatorKind.Equals:
                // kind == Kind.X or Kind.X == kind — extract the Kind constant
                TryExtractKindVariant(binary.LeftOperand, info, variants);
                TryExtractKindVariant(binary.RightOperand, info, variants);
                break;

            case IBinaryOperation binary when
                binary.OperatorKind == BinaryOperatorKind.NotEquals:
                {
                    var excluded = new List<string>();
                    TryExtractKindVariant(binary.LeftOperand, info, excluded);
                    TryExtractKindVariant(binary.RightOperand, info, excluded);
                    foreach (var variant in info.VariantNames)
                    {
                        if (!excluded.Contains(variant))
                            variants.Add(variant);
                    }
                }
                break;
        }

        return variants;
    }

    /// Tries to resolve an operation to a Kind enum constant and map it to a variant name.
    private static void TryExtractKindVariant(IOperation operand, UnionTypeInfo info, List<string> variants)
    {
        if (info.KindEnumType is null)
            return;

        // The operand might be a field reference like Shape.Kind.Circle
        if (operand is IFieldReferenceOperation fieldRef &&
            SymbolEqualityComparer.Default.Equals(fieldRef.Field.ContainingType, info.KindEnumType) &&
            fieldRef.Field.HasConstantValue)
        {
            int ordinal;
            switch (fieldRef.Field.ConstantValue)
            {
                case byte b: ordinal = b; break;
                case ushort u: ordinal = u; break;
                case int i: ordinal = i; break;
                case uint ui: ordinal = (int)ui; break;
                default: return;
            }

            if (ordinal >= 0 && ordinal < info.VariantNames.Length)
                variants.Add(info.VariantNames[ordinal]);
        }
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

    /// Checks whether the condition references the same union variable as the member access.
    /// Handles: is-pattern (s is { kind: Kind.Circle }), kind comparison (s.kind == Kind.Circle).
    private static bool ConditionReferencesUnionVariable(IOperation condition, IOperation memberRef)
    {
        var targetInstance = memberRef switch
        {
            IFieldReferenceOperation f => f.Instance,
            IPropertyReferenceOperation p => p.Instance,
            _ => null,
        };
        if (targetInstance is null)
            return false;

        switch (condition)
        {
            case IIsPatternOperation isPattern:
                return ReferenceSameVariable(isPattern.Value, targetInstance);

            case IBinaryOperation binary when
                binary.OperatorKind == BinaryOperatorKind.Equals ||
                binary.OperatorKind == BinaryOperatorKind.NotEquals:
                // Check if either side is a .kind access on the same variable
                return IsKindAccessOnSameVariable(binary.LeftOperand, targetInstance) ||
                       IsKindAccessOnSameVariable(binary.RightOperand, targetInstance);

            default:
                return false;
        }
    }

    /// Checks whether an operation is a .kind field/property access on the same variable.
    private static bool IsKindAccessOnSameVariable(IOperation operation, IOperation targetInstance)
    {
        if (operation is IFieldReferenceOperation kindFieldRef &&
            kindFieldRef.Field.Name == "kind" &&
            kindFieldRef.Instance is not null)
        {
            return ReferenceSameVariable(kindFieldRef.Instance, targetInstance);
        }

        if (operation is IPropertyReferenceOperation kindPropRef &&
            kindPropRef.Property.Name == "kind" &&
            kindPropRef.Instance is not null)
        {
            return ReferenceSameVariable(kindPropRef.Instance, targetInstance);
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
