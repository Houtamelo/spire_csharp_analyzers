using System;

namespace Houtamelo.Spire;

/// <summary>
/// Marks a type as requiring explicit initialization. The Spire analyzers (SPIRE001-008)
/// will report errors when code creates default/uninitialized instances of this type
/// through arrays, <c>default(T)</c>, parameterless construction, or reflection.
/// </summary>
/// <remarks>
/// For enums, the rules only flag when the enum has no zero-valued named member.
/// When a zero member exists (e.g., <c>None = 0</c>), <c>default(T)</c> produces
/// that valid member and is not flagged.
/// </remarks>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class | AttributeTargets.Enum)]
public class EnforceInitializationAttribute : Attribute
{
}
