//@ should_pass
// Record-class unions use the existing record-union path.
using Houtamelo.Spire;

[DiscriminatedUnion]
public abstract partial record Shape
{
    public sealed partial record Circle(int Radius) : Shape;
    public sealed partial record Empty : Shape;
}
