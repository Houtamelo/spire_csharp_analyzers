using System;

namespace Houtamelo.Spire.Core;

[AttributeUsage(AttributeTargets.Enum | AttributeTargets.Class | AttributeTargets.Interface)]
public sealed class EnforceExhaustivenessAttribute : EnforceInitializationAttribute
{
}
