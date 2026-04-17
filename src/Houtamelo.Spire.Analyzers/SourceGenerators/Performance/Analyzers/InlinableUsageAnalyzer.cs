using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Performance.Analyzers;

/// Enforces SPIRE021-SPIRE026 on methods carrying [Inlinable] parameters.
/// Runs alongside the InlinableTwinGenerator — this analyzer is the authoritative
/// surface for developer-facing diagnostics; the generator additionally reports
/// the same IDs but only when it chooses to skip emission.
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InlinableUsageAnalyzer : DiagnosticAnalyzer
{
    private const string InlinableAttrFullName = "Houtamelo.Spire.InlinableAttribute";

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        InlinerDescriptors.SPIRE021_UnsupportedBodyUsage,
        InlinerDescriptors.SPIRE022_NonDelegateParameter,
        InlinerDescriptors.SPIRE023_ContainerNotPartial,
        InlinerDescriptors.SPIRE024_DelegateArityExceeded,
        InlinerDescriptors.SPIRE025_UnsupportedRefKind,
        InlinerDescriptors.SPIRE026_PropertyOrIndexerParameter);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterCompilationStartAction(compCtx =>
        {
            var inlinableAttr = compCtx.Compilation.GetTypeByMetadataName(InlinableAttrFullName);
            if (inlinableAttr is null)
                return;

            compCtx.RegisterSymbolAction(sc => AnalyzeMethod(sc, inlinableAttr), SymbolKind.Method);
        });
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context, INamedTypeSymbol inlinableAttr)
    {
        var method = (IMethodSymbol)context.Symbol;

        // Collect [Inlinable] parameters.
        var inlinableParamsWithAttr = new List<(IParameterSymbol Param, AttributeData Attr)>();
        foreach (var p in method.Parameters)
        {
            foreach (var attr in p.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, inlinableAttr))
                {
                    inlinableParamsWithAttr.Add((p, attr));
                    break;
                }
            }
        }

        if (inlinableParamsWithAttr.Count == 0)
            return;

        // SPIRE026: property accessor / indexer parameters.
        bool methodLevelBlocker = false;
        if (method.MethodKind == MethodKind.PropertyGet
            || method.MethodKind == MethodKind.PropertySet
            || method.AssociatedSymbol is IPropertySymbol)
        {
            // Report once per inlinable param on the attribute location so the diagnostic
            // points at the offending attribute, not the accessor keyword.
            foreach (var (param, attr) in inlinableParamsWithAttr)
            {
                var loc = GetAttributeLocation(attr, param);
                context.ReportDiagnostic(Diagnostic.Create(
                    InlinerDescriptors.SPIRE026_PropertyOrIndexerParameter,
                    loc));
            }
            methodLevelBlocker = true;
        }

        // SPIRE023: containing type (and enclosing types) must be partial.
        var containingNotPartial = FindNonPartialEnclosingType(method);
        if (containingNotPartial is not null)
        {
            // Report on the method location once per method (first inlinable param's name).
            var firstParamName = inlinableParamsWithAttr[0].Param.Name;
            var methodLoc = method.Locations.Length > 0 ? method.Locations[0] : Location.None;
            context.ReportDiagnostic(Diagnostic.Create(
                InlinerDescriptors.SPIRE023_ContainerNotPartial,
                methodLoc,
                firstParamName));
            methodLevelBlocker = true;
        }

        // Per-parameter checks (SPIRE025, SPIRE022, SPIRE024).
        // Track parameters that survived the gates so we can analyze the body.
        var wellFormedParams = new List<IParameterSymbol>();
        foreach (var (param, attr) in inlinableParamsWithAttr)
        {
            var paramLoc = GetAttributeLocation(attr, param);

            // SPIRE025: ref-kinds.
            if (param.RefKind == RefKind.Ref
                || param.RefKind == RefKind.In
                || param.RefKind == RefKind.Out
                || param.RefKind == RefKind.RefReadOnlyParameter)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InlinerDescriptors.SPIRE025_UnsupportedRefKind,
                    paramLoc,
                    param.Name));
                continue;
            }

            // SPIRE022: must be System.Action / System.Action<...> / System.Func<...>.
            if (!IsSupportedDelegateType(param.Type, out int arity))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InlinerDescriptors.SPIRE022_NonDelegateParameter,
                    paramLoc,
                    param.Name,
                    param.Type.ToDisplayString()));
                continue;
            }

            // SPIRE024: arity must be <= 8.
            if (arity > 8)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InlinerDescriptors.SPIRE024_DelegateArityExceeded,
                    paramLoc,
                    param.Name,
                    arity));
                continue;
            }

            wellFormedParams.Add(param);
        }

        // SPIRE021: body usage analysis. Skip if the method itself is not well-formed
        // (property/indexer/non-partial) or if no parameter is well-formed.
        if (methodLevelBlocker || wellFormedParams.Count == 0)
            return;

        AnalyzeBodyUsage(context, method, wellFormedParams);
    }

    private static Location GetAttributeLocation(AttributeData attr, IParameterSymbol fallback)
    {
        var syntaxRef = attr.ApplicationSyntaxReference;
        if (syntaxRef is not null)
            return Location.Create(syntaxRef.SyntaxTree, syntaxRef.Span);
        return fallback.Locations.Length > 0 ? fallback.Locations[0] : Location.None;
    }

    private static INamedTypeSymbol? FindNonPartialEnclosingType(IMethodSymbol method)
    {
        var current = method.ContainingType;
        while (current is not null)
        {
            if (!IsDeclaredPartial(current))
                return current;
            current = current.ContainingType;
        }
        return null;
    }

    private static bool IsDeclaredPartial(INamedTypeSymbol type)
    {
        foreach (var syntaxRef in type.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax() is TypeDeclarationSyntax tds)
            {
                foreach (var mod in tds.Modifiers)
                {
                    if (mod.IsKind(SyntaxKind.PartialKeyword))
                        return true;
                }
            }
        }
        return false;
    }

    private static bool IsSupportedDelegateType(ITypeSymbol type, out int arity)
    {
        arity = 0;
        if (type is not INamedTypeSymbol named)
            return false;

        var originalDef = named.OriginalDefinition.ToDisplayString();
        bool isAction = originalDef == "System.Action"
            || originalDef.StartsWith("System.Action<", System.StringComparison.Ordinal);
        bool isFunc = originalDef.StartsWith("System.Func<", System.StringComparison.Ordinal);

        if (!isAction && !isFunc)
            return false;

        if (isFunc)
        {
            // Func<T1..Tn, TR>: arity is TypeArguments.Length - 1.
            arity = named.TypeArguments.Length - 1;
            if (arity < 0) arity = 0;
        }
        else
        {
            arity = named.TypeArguments.Length;
        }

        return true;
    }

    private static void AnalyzeBodyUsage(
        SymbolAnalysisContext context,
        IMethodSymbol method,
        List<IParameterSymbol> wellFormedParams)
    {
        // Find the method declaration syntax. Skip partial method split / abstract.
        MethodDeclarationSyntax? mds = null;
        foreach (var syntaxRef in method.DeclaringSyntaxReferences)
        {
            if (syntaxRef.GetSyntax(context.CancellationToken) is MethodDeclarationSyntax m
                && (m.Body is not null || m.ExpressionBody is not null))
            {
                mds = m;
                break;
            }
        }
        if (mds is null)
            return;

        // Build the set of tracked names: the parameter names, plus any single-assignment
        // `var` aliases whose initializer is another tracked name. Propagate to fixed point.
        var paramNames = new HashSet<string>(wellFormedParams.Select(p => p.Name));
        var trackedNames = new HashSet<string>(paramNames);

        // Identify var-alias locals (var x = <tracked>;) and propagate.
        //
        // Collect the ancestor body root (either mds.Body or mds.ExpressionBody).
        SyntaxNode bodyRoot = (SyntaxNode?)mds.Body ?? mds.ExpressionBody!;

        // Collect all var-declarators that could form aliases.
        var allVarDeclarators = bodyRoot.DescendantNodes().OfType<VariableDeclaratorSyntax>().ToList();
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var v in allVarDeclarators)
            {
                if (v.Parent is not VariableDeclarationSyntax vds) continue;
                if (vds.Type is not IdentifierNameSyntax idType || idType.Identifier.Text != "var") continue;
                if (vds.Variables.Count != 1) continue; // must be single-declarator alias
                if (v.Initializer?.Value is not IdentifierNameSyntax rhsId) continue;
                if (!trackedNames.Contains(rhsId.Identifier.Text)) continue;

                var localName = v.Identifier.Text;
                if (trackedNames.Add(localName))
                    changed = true;
            }
        }

        // Collect identifier references inside the body root and classify each.
        foreach (var id in bodyRoot.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            var name = id.Identifier.Text;
            if (!trackedNames.Contains(name))
                continue;

            // Skip: the identifier that introduces the tracked alias itself is a
            // VariableDeclaratorSyntax.Identifier (token, not IdentifierNameSyntax).
            // IdentifierNameSyntax appears only when the name is *used* — so no skip
            // needed for declarator names.

            // Skip: 'nameof(p)' — the argument is an IdentifierNameSyntax inside an
            // InvocationExpression whose target name is "nameof".
            if (IsInsideNameof(id))
                continue;

            // Captured by nested lambda / local function: the twin rewrite cannot reach
            // inside a nested function, so any tracked reference inside one is a violation.
            if (IsInsideNestedFunction(id, bodyRoot))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InlinerDescriptors.SPIRE021_UnsupportedBodyUsage,
                    id.GetLocation(),
                    name,
                    "captured by a nested lambda or local function (forces a heap-allocated closure and a delegate wrapper around the struct)"));
                continue;
            }

            if (IsAllowedUsage(id, trackedNames, paramNames, out var violationHint))
                continue;

            // Report SPIRE021 on the identifier's location.
            var hint = violationHint ?? DescribeUsage(id);
            context.ReportDiagnostic(Diagnostic.Create(
                InlinerDescriptors.SPIRE021_UnsupportedBodyUsage,
                id.GetLocation(),
                name,
                hint));
        }
    }

    private static bool IsInsideNestedFunction(IdentifierNameSyntax id, SyntaxNode bodyRoot)
    {
        for (SyntaxNode? n = id.Parent; n is not null && n != bodyRoot; n = n.Parent)
        {
            if (n is AnonymousFunctionExpressionSyntax
                || n is LocalFunctionStatementSyntax)
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsInsideNameof(IdentifierNameSyntax id)
    {
        // 'nameof' is a contextual keyword that surfaces as an InvocationExpression
        // whose Expression is IdentifierNameSyntax("nameof").
        for (SyntaxNode? n = id.Parent; n is not null; n = n.Parent)
        {
            if (n is InvocationExpressionSyntax inv
                && inv.Expression is IdentifierNameSyntax target
                && target.Identifier.Text == "nameof")
            {
                return true;
            }
            // Don't walk past statements — nameof is always syntactically enclosing.
            if (n is StatementSyntax) break;
        }
        return false;
    }

    private static bool IsAllowedUsage(
        IdentifierNameSyntax id,
        HashSet<string> trackedNames,
        HashSet<string> paramNames,
        out string? violationHint)
    {
        violationHint = null;
        var parent = id.Parent;
        if (parent is null)
        {
            violationHint = "used as a bare reference";
            return false;
        }

        // Direct invocation: `p(x)` — id is InvocationExpression.Expression.
        if (parent is InvocationExpressionSyntax inv && inv.Expression == id)
            return true;

        // Null-conditional: `p?.Invoke(...)` / `p?.Member...` — id is ConditionalAccess.Expression.
        if (parent is ConditionalAccessExpressionSyntax cae && cae.Expression == id)
            return true;

        // Pass-through as argument: `OtherMethod(p)` / `OtherMethod(p, ...)`.
        if (parent is ArgumentSyntax)
            return true;

        // Null comparison: `p == null`, `p != null`, `null == p`, `null != p`.
        if (parent is BinaryExpressionSyntax bin
            && (bin.IsKind(SyntaxKind.EqualsExpression) || bin.IsKind(SyntaxKind.NotEqualsExpression)))
        {
            var other = bin.Left == id ? bin.Right : bin.Left;
            if (other is LiteralExpressionSyntax lit && lit.IsKind(SyntaxKind.NullLiteralExpression))
                return true;
        }

        // Pattern null-check: `p is null`, `p is not null`, `p is {}`, etc. — the
        // identifier is the pattern subject of an IsPatternExpressionSyntax.
        if (parent is IsPatternExpressionSyntax isPat && isPat.Expression == id)
            return true;

        // Classic `p is SomeType` — type-check expression, treated as pattern above in
        // modern C# via IsPatternExpressionSyntax; legacy BinaryExpression(IsExpression)
        // with delegate type on the right is exotic — conservatively allow.
        if (parent is BinaryExpressionSyntax binIs && binIs.IsKind(SyntaxKind.IsExpression) && binIs.Left == id)
            return true;

        // Initializer of a new tracked-alias local: `var other = p;` — id is the RHS,
        // parent is EqualsValueClauseSyntax, grandparent is VariableDeclaratorSyntax,
        // great-grandparent is VariableDeclarationSyntax with Type=var. The alias is
        // already tracked so this is effectively a second reference — allowed.
        if (parent is EqualsValueClauseSyntax eq
            && eq.Parent is VariableDeclaratorSyntax vd
            && vd.Initializer == eq
            && vd.Parent is VariableDeclarationSyntax vds
            && vds.Type is IdentifierNameSyntax varTy && varTy.Identifier.Text == "var"
            && vds.Variables.Count == 1
            && trackedNames.Contains(vd.Identifier.Text))
        {
            return true;
        }

        // Reassignment: `alias = ...;` where alias is a tracked local.
        // This catches both `alias = someOther` and `alias = null`.
        if (parent is AssignmentExpressionSyntax assign && assign.Left == id)
        {
            // Parameters can't be reassigned via var aliasing rules; but a parameter
            // itself being reassigned (`p = other;`) is also a violation.
            violationHint = paramNames.Contains(id.Identifier.Text)
                ? "reassigned (the synthesized twin's struct parameter cannot be overwritten)"
                : "reassigned after its initial var-alias declaration (aliases must be single-assignment)";
            return false;
        }

        // Conditional expression where id is a branch and the whole expr is not being
        // invoked — e.g., `var a = cond ? p : q;` is a tracked-alias seed only when the
        // expression sits under a VariableDeclarator of var type; the propagation pass
        // above handles it. Here, treat it as a violation by default.
        //
        // Let specific shapes through: being stored in an `EqualsValueClauseSyntax` of
        // a tracked alias is covered above; other shapes are not allowed.

        // Default: disallowed (field store, cast, delegate combine, await, lambda capture,
        // member access other than .Invoke via conditional access, etc.).
        violationHint = DescribeUsage(id);
        return false;
    }

    private static string DescribeUsage(IdentifierNameSyntax id)
    {
        var p = id.Parent;
        return p switch
        {
            AssignmentExpressionSyntax => "stored into a non-alias destination (a field, property, or explicitly delegate-typed local)",
            MemberAccessExpressionSyntax => "accessed via a member other than .Invoke (e.g. .Target, .Method) — the twin's struct does not expose System.Delegate members",
            CastExpressionSyntax => "used in a cast — the twin's struct parameter is not assignable to the delegate type",
            LambdaExpressionSyntax => "captured by a lambda (forces a heap-allocated closure and a delegate wrapper around the struct)",
            AnonymousFunctionExpressionSyntax => "captured by an anonymous function (forces a heap-allocated closure and a delegate wrapper around the struct)",
            ReturnStatementSyntax => "returned as a delegate — the twin's struct cannot be implicitly converted to the declared return type",
            _ => "used in a form the [Inlinable] twin generator cannot rewrite",
        };
    }
}
