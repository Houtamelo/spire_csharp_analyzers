using System;

namespace Spire.Analyzers;

[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public sealed class MustBeInitAttribute : Attribute
{
}
