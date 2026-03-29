global using System;

namespace Houtamelo.Spire
{
    [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
    internal sealed class DiscriminatedUnionAttribute : Attribute { }
}

[Houtamelo.Spire.DiscriminatedUnion]
public partial struct Shape
{
    public enum Kind : byte { Circle, Rectangle, Triangle }
    public readonly Kind kind;

    // Deconstruct method for tuple-style matching
    public void Deconstruct(out Kind k, out double v1)
    {
        k = kind;
        v1 = 0;
    }
}
