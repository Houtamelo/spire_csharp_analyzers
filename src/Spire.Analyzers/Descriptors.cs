using Microsoft.CodeAnalysis;
// ReSharper disable InconsistentNaming

namespace Spire.Analyzers;

internal static class Descriptors
{
    public static readonly DiagnosticDescriptor SPIRE001_ArrayOfMustBeInitStruct = new(
        id: "SPIRE001",
        title: "Non-empty array of [MustBeInit] type produces uninitialized elements",
        messageFormat: "Non-empty array of type '{0}' marked with [MustBeInit] will contain uninitialized elements",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Creating a non-empty array of a type marked with [MustBeInit] fills all elements with default(T), "
                   + "bypassing any required initialization. For structs this is a zeroed instance; for classes this is null. "
                   + "Use an empty array or provide an explicit initializer.",
        helpLinkUri: "https://github.com/TODO/docs/rules/SPIRE001.md"
    );

    public static readonly DiagnosticDescriptor SPIRE002_MustBeInitOnFieldlessType = new(
        id: "SPIRE002",
        title: "[MustBeInit] on fieldless type has no effect",
        messageFormat: "[MustBeInit] on type '{0}' has no effect because it has no instance fields",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The [MustBeInit] attribute marks types whose default value is considered uninitialized. "
                   + "A type with no instance fields has only one possible value (the default), so the attribute serves no purpose. "
                   + "Note that auto-properties generate backing fields and do count; non-auto (computed) properties do not.",
        helpLinkUri: "https://github.com/TODO/docs/rules/SPIRE002.md"
    );

    public static readonly DiagnosticDescriptor SPIRE003_DefaultOfMustBeInitStruct = new(
        id: "SPIRE003",
        title: "default(T) where T is a [MustBeInit] type produces an uninitialized value",
        messageFormat: "default value of type '{0}' marked with [MustBeInit] bypasses required initialization",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Types marked with [MustBeInit] require explicit initialization. Using default(T) or the default "
                   + "literal produces an uninitialized value (zeroed struct or null reference), defeating the purpose of the attribute.",
        helpLinkUri: "https://github.com/TODO/docs/rules/SPIRE003.md"
    );

    public static readonly DiagnosticDescriptor SPIRE004_NewOfMustBeInitStructWithoutCtor = new(
        id: "SPIRE004",
        title: "new T() on [MustBeInit] struct without parameterless constructor is equivalent to default(T)",
        messageFormat: "'new {0}()' is equivalent to 'default({0})' because '{0}' has no user-defined parameterless constructor",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "For structs without a user-defined parameterless constructor, 'new T()' produces the same "
                   + "default (zeroed) instance as 'default(T)'. When the struct is marked with [MustBeInit], "
                   + "this bypasses required initialization. Use a constructor with parameters or add a parameterless "
                   + "constructor that initializes the struct's fields.",
        helpLinkUri: "https://github.com/TODO/docs/rules/SPIRE004.md"
    );

    public static readonly DiagnosticDescriptor SPIRE005_ActivatorCreateInstanceOfMustBeInitStruct = new(
        id: "SPIRE005",
        title: "Activator.CreateInstance on [MustBeInit] struct produces a default instance",
        messageFormat: "Activator.CreateInstance produces a default instance of struct '{0}' marked with [MustBeInit]",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Activator.CreateInstance<T>() and Activator.CreateInstance(typeof(T)) produce default (zeroed) "
                   + "instances of value types, bypassing any required initialization. When T is a struct marked with "
                   + "[MustBeInit], this defeats the purpose of the attribute.",
        helpLinkUri: "https://github.com/TODO/docs/rules/SPIRE005.md"
    );

    public static readonly DiagnosticDescriptor SPIRE006_ClearOfMustBeInitElements = new(
        id: "SPIRE006",
        title: "Clearing array or span of [MustBeInit] type produces uninitialized elements",
        messageFormat: "{0} resets elements of type '{1}' marked with [MustBeInit] to their uninitialized state",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Array.Clear and Span<T>.Clear() reset elements to default(T). When T is a type marked with "
                   + "[MustBeInit], this produces uninitialized elements (zeroed structs or null references), "
                   + "defeating the purpose of the attribute.",
        helpLinkUri: "https://github.com/TODO/docs/rules/SPIRE006.md"
    );

    public static readonly DiagnosticDescriptor SPIRE007_UnsafeSkipInitOfMustBeInitStruct = new(
        id: "SPIRE007",
        title: "Unsafe.SkipInit on [MustBeInit] struct leaves it uninitialized",
        messageFormat: "Unsafe.SkipInit leaves struct '{0}' marked with [MustBeInit] completely uninitialized",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Unsafe.SkipInit<T>(out T) bypasses zero-initialization entirely, leaving the value as "
                   + "whatever was previously in that memory location. When T is a struct marked with [MustBeInit], "
                   + "this is worse than default — the instance contains garbage data.",
        helpLinkUri: "https://github.com/TODO/docs/rules/SPIRE007.md"
    );

    public static readonly DiagnosticDescriptor SPIRE008_GetUninitializedObjectOfMustBeInitStruct = new(
        id: "SPIRE008",
        title: "RuntimeHelpers.GetUninitializedObject on [MustBeInit] struct bypasses all constructors",
        messageFormat: "RuntimeHelpers.GetUninitializedObject produces an uninitialized instance of struct '{0}' marked with [MustBeInit]",
        category: "Correctness",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "RuntimeHelpers.GetUninitializedObject(Type) bypasses all constructors and field initializers, "
                   + "producing a completely uninitialized instance. When the type is a struct marked with [MustBeInit], "
                   + "this defeats the purpose of the attribute.",
        helpLinkUri: "https://github.com/TODO/docs/rules/SPIRE008.md"
    );
}
