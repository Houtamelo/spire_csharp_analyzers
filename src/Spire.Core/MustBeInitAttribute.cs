using System;

namespace Spire;

[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class | AttributeTargets.Enum)]
public class MustBeInitAttribute : Attribute
{
}
