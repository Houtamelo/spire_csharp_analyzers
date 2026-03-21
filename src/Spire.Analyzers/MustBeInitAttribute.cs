using System;

namespace Spire.Analyzers;

[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class | AttributeTargets.Enum)]
public class MustBeInitAttribute : Attribute
{
}
