//@ should_pass
// Abstract class with 3 sealed subtypes, all covered via type patterns — no diagnostic
#nullable enable
using Houtamelo.Spire;
namespace TestNs
{
    [EnforceExhaustiveness]
    public abstract class Shape { }
    public sealed class Circle : Shape { public double Radius { get; init; } }
    public sealed class Rectangle : Shape { public double Width { get; init; } public double Height { get; init; } }
    public sealed class Triangle : Shape { public double Base { get; init; } public double Height { get; init; } }

    class Consumer
    {
        double Area(Shape s) => s switch
        {
            Circle c => System.Math.PI * c.Radius * c.Radius,
            Rectangle r => r.Width * r.Height,
            Triangle t => 0.5 * t.Base * t.Height,
        };
    }
}
