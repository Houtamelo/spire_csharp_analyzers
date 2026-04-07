using Microsoft.CodeAnalysis;
// ReSharper disable InconsistentNaming

namespace Houtamelo.Spire.Analyzers;

internal static class Descriptors
{
    public static readonly DiagnosticDescriptor SPIRE001_ArrayOfEnforceInitializationStruct = new(
        id: "SPIRE001",
        title: "Non-empty array of [EnforceInitialization] type produces uninitialized elements",
        messageFormat: "Non-empty array of type '{0}' marked with [EnforceInitialization] will contain uninitialized elements",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Creating a non-empty array of a type marked with [EnforceInitialization] fills all elements with default(T), "
                   + "bypassing any required initialization. For structs this is a zeroed instance; for classes this is null. "
                   + "Use an empty array or provide an explicit initializer.",
        helpLinkUri: "https://github.com/TODO/docs/rules/SPIRE001.md"
    );

    public static readonly DiagnosticDescriptor SPIRE002_EnforceInitializationOnFieldlessType = new(
        id: "SPIRE002",
        title: "[EnforceInitialization] on fieldless type has no effect",
        messageFormat: "[EnforceInitialization] on type '{0}' has no effect because it has no instance fields",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The [EnforceInitialization] attribute marks types whose default value is considered uninitialized. "
                   + "A type with no instance fields has only one possible value (the default), so the attribute serves no purpose. "
                   + "Note that auto-properties generate backing fields and do count; non-auto (computed) properties do not.",
        helpLinkUri: "https://github.com/TODO/docs/rules/SPIRE002.md"
    );

    public static readonly DiagnosticDescriptor SPIRE003_DefaultOfEnforceInitializationStruct = new(
        id: "SPIRE003",
        title: "default(T) where T is a [EnforceInitialization] type produces an uninitialized value",
        messageFormat: "default value of type '{0}' marked with [EnforceInitialization] bypasses required initialization",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Types marked with [EnforceInitialization] require explicit initialization. Using default(T) or the default "
                   + "literal produces an uninitialized value (zeroed struct or null reference), defeating the purpose of the attribute.",
        helpLinkUri: "https://github.com/TODO/docs/rules/SPIRE003.md"
    );

    public static readonly DiagnosticDescriptor SPIRE004_NewOfEnforceInitializationStructWithoutCtor = new(
        id: "SPIRE004",
        title: "new T() on [EnforceInitialization] struct without parameterless constructor is equivalent to default(T)",
        messageFormat: "'new {0}()' is equivalent to 'default({0})' because '{0}' has no user-defined parameterless constructor",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "For structs without a user-defined parameterless constructor, 'new T()' produces the same "
                   + "default (zeroed) instance as 'default(T)'. When the struct is marked with [EnforceInitialization], "
                   + "this bypasses required initialization. Use a constructor with parameters or add a parameterless "
                   + "constructor that initializes the struct's fields.",
        helpLinkUri: "https://github.com/TODO/docs/rules/SPIRE004.md"
    );

    public static readonly DiagnosticDescriptor SPIRE005_ActivatorCreateInstanceOfEnforceInitializationStruct = new(
        id: "SPIRE005",
        title: "Activator.CreateInstance on [EnforceInitialization] struct produces a default instance",
        messageFormat: "Activator.CreateInstance produces a default instance of struct '{0}' marked with [EnforceInitialization]",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Activator.CreateInstance<T>() and Activator.CreateInstance(typeof(T)) produce default (zeroed) "
                   + "instances of value types, bypassing any required initialization. When T is a struct marked with "
                   + "[EnforceInitialization], this defeats the purpose of the attribute.",
        helpLinkUri: "https://github.com/TODO/docs/rules/SPIRE005.md"
    );

    public static readonly DiagnosticDescriptor SPIRE006_ClearOfEnforceInitializationElements = new(
        id: "SPIRE006",
        title: "Clearing array or span of [EnforceInitialization] type produces uninitialized elements",
        messageFormat: "{0} resets elements of type '{1}' marked with [EnforceInitialization] to their uninitialized state",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Array.Clear and Span<T>.Clear() reset elements to default(T). When T is a type marked with "
                   + "[EnforceInitialization], this produces uninitialized elements (zeroed structs or null references), "
                   + "defeating the purpose of the attribute.",
        helpLinkUri: "https://github.com/TODO/docs/rules/SPIRE006.md"
    );

    public static readonly DiagnosticDescriptor SPIRE007_UnsafeSkipInitOfEnforceInitializationStruct = new(
        id: "SPIRE007",
        title: "Unsafe.SkipInit on [EnforceInitialization] struct leaves it uninitialized",
        messageFormat: "Unsafe.SkipInit leaves struct '{0}' marked with [EnforceInitialization] completely uninitialized",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Unsafe.SkipInit<T>(out T) bypasses zero-initialization entirely, leaving the value as "
                   + "whatever was previously in that memory location. When T is a struct marked with [EnforceInitialization], "
                   + "this is worse than default — the instance contains garbage data.",
        helpLinkUri: "https://github.com/TODO/docs/rules/SPIRE007.md"
    );

    public static readonly DiagnosticDescriptor SPIRE008_GetUninitializedObjectOfEnforceInitializationStruct = new(
        id: "SPIRE008",
        title: "RuntimeHelpers.GetUninitializedObject on [EnforceInitialization] struct bypasses all constructors",
        messageFormat: "RuntimeHelpers.GetUninitializedObject produces an uninitialized instance of struct '{0}' marked with [EnforceInitialization]",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "RuntimeHelpers.GetUninitializedObject(Type) bypasses all constructors and field initializers, "
                   + "producing a completely uninitialized instance. When the type is a struct marked with [EnforceInitialization], "
                   + "this defeats the purpose of the attribute.",
        helpLinkUri: "https://github.com/TODO/docs/rules/SPIRE008.md"
    );

    public static readonly DiagnosticDescriptor SPIRE015_ExhaustiveEnumSwitch = new(
        id: "SPIRE015",
        title: "Switch does not handle all members of [EnforceExhaustiveness] enum",
        messageFormat: "Switch on '{0}' does not handle member(s): {1}",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Switch statements and expressions on enum types marked with [EnforceExhaustiveness] must "
                   + "explicitly handle every named member. An unguarded catch-all (default:, _, or var x) "
                   + "opts out of the check — the developer has chosen to handle any unmatched value.",
        helpLinkUri: "https://github.com/TODO/docs/rules/SPIRE015.md"
    );

    public static readonly DiagnosticDescriptor SPIRE016_InvalidEnforceInitializationEnumValue = new(
        id: "SPIRE016",
        title: "Operation may produce invalid value of [EnforceInitialization] enum",
        messageFormat: "{0} may produce a value of enum '{1}' marked with [EnforceInitialization] that is not a named member",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Enums marked with [EnforceInitialization] require all values to correspond to named members. "
                   + "This rule flags operations that may produce unnamed values: default expressions (when no "
                   + "zero-valued member exists), integer-to-enum casts with unknown or invalid values, and "
                   + "unsafe operations that bypass zero-initialization entirely.",
        helpLinkUri: "https://github.com/TODO/docs/rules/SPIRE016.md"
    );

    public static readonly DiagnosticDescriptor SPIRE016_InvalidEnforceInitializationEnumConstantValue = new(
        id: "SPIRE016",
        title: "Cast to [EnforceInitialization] enum uses invalid constant value",
        messageFormat: "{0} to '{1}' — value {2} does not map to a valid member",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A constant integer value is cast to an [EnforceInitialization] enum but does not correspond "
                   + "to any named member (or valid flags combination).",
        helpLinkUri: "https://github.com/TODO/docs/rules/SPIRE016.md"
    );
}
