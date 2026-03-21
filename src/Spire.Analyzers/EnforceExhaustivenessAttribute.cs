using System;

namespace Spire.Analyzers;

[AttributeUsage(AttributeTargets.Enum)]
public sealed class EnforceExhaustivenessAttribute : MustBeInitAttribute
{
}
