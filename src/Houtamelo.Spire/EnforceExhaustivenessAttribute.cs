using System;

namespace Houtamelo.Spire;

/// <summary>
/// Marks a type for exhaustive switch checking. The Spire analyzer (SPIRE015)
/// will report errors when a switch expression or statement does not handle all
/// known subtypes or enum members of the annotated type.
/// </summary>
/// <remarks>
/// Inherits from <see cref="EnforceInitializationAttribute"/>, so all initialization
/// rules (SPIRE001-008) also apply. Valid targets: enums, classes, and interfaces.
/// For class/interface hierarchies, the analyzer discovers all sealed subtypes in the
/// compilation and requires each to be handled.
/// </remarks>
[AttributeUsage(AttributeTargets.Enum | AttributeTargets.Class | AttributeTargets.Interface)]
public sealed class EnforceExhaustivenessAttribute : EnforceInitializationAttribute
{
}
