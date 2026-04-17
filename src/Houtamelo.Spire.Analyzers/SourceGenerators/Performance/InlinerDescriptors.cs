using Microsoft.CodeAnalysis;

namespace Houtamelo.Spire.Analyzers.SourceGenerators.Performance;

internal static class InlinerDescriptors
{
    private const string SourceGenCategory = "SourceGeneration";
    private const string CorrectnessCategory = "Correctness";

    public static readonly DiagnosticDescriptor SPIRE017_UnsupportedParameterModifier = new(
        id: "SPIRE017",
        title: "[InlinerStruct] method parameter has unsupported modifier",
        messageFormat: "Parameter '{0}' uses an unsupported modifier ({1}); [InlinerStruct] methods cannot have ref/in/out/ref readonly/params parameters",
        category: SourceGenCategory, defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SPIRE018_RefStructDeclaringType = new(
        id: "SPIRE018",
        title: "[InlinerStruct] not supported on ref struct",
        messageFormat: "[InlinerStruct] cannot be applied to a method declared on a ref struct",
        category: SourceGenCategory, defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SPIRE019_ArityExceeded = new(
        id: "SPIRE019",
        title: "[InlinerStruct] arity exceeds 8",
        messageFormat: "Method '{0}' has arity {1} (instance methods count the receiver), which exceeds the supported maximum of 8",
        category: SourceGenCategory, defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SPIRE020_NameCollision = new(
        id: "SPIRE020",
        title: "[InlinerStruct] generated struct name collides with existing type",
        messageFormat: "Generated struct '{0}' collides with an existing type in the same namespace/nesting",
        category: SourceGenCategory, defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SPIRE021_UnsupportedBodyUsage = new(
        id: "SPIRE021",
        title: "[Inlinable] parameter used in unsupported form",
        messageFormat: "[Inlinable] parameter '{0}' may only be invoked or aliased via single-assignment 'var'; {1}",
        category: CorrectnessCategory, defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SPIRE022_NonDelegateParameter = new(
        id: "SPIRE022",
        title: "[Inlinable] applied to non-delegate parameter",
        messageFormat: "[Inlinable] can only be applied to Action or Func parameters; '{0}' has type {1}",
        category: CorrectnessCategory, defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SPIRE023_ContainerNotPartial = new(
        id: "SPIRE023",
        title: "[Inlinable] method's containing type is not partial",
        messageFormat: "The type containing '[Inlinable]' parameter '{0}' must be declared 'partial' (and so must every enclosing type)",
        category: SourceGenCategory, defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SPIRE024_DelegateArityExceeded = new(
        id: "SPIRE024",
        title: "[Inlinable] delegate arity exceeds 8",
        messageFormat: "Delegate parameter '{0}' has arity {1}; supported maximum is 8",
        category: SourceGenCategory, defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SPIRE025_UnsupportedRefKind = new(
        id: "SPIRE025",
        title: "[Inlinable] parameter has unsupported ref-kind",
        messageFormat: "[Inlinable] parameter '{0}' cannot be declared with ref/in/out/ref readonly",
        category: CorrectnessCategory, defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SPIRE026_PropertyOrIndexerParameter = new(
        id: "SPIRE026",
        title: "[Inlinable] not supported on property/indexer parameters",
        messageFormat: "[Inlinable] is not supported on indexer or property accessor parameters in v1",
        category: CorrectnessCategory, defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor SPIRE027_EnclosingTypeNotPartial = new(
        id: "SPIRE027",
        title: "Declaring type (or enclosing type) is not partial",
        messageFormat: "Type '{0}' must be declared 'partial' for inliner-struct generation",
        category: SourceGenCategory, defaultSeverity: DiagnosticSeverity.Error, isEnabledByDefault: true);
}
