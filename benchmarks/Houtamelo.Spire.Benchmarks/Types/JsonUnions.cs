using Houtamelo.Spire;

namespace Houtamelo.Spire.Benchmarks.Types;

// JSON benchmark representations are generated from one declaration.
[BenchmarkUnion(Name = "EventJson")]
public partial struct EventJsonBench
{
    [Variant] public static partial EventJsonBench Point();
    [Variant] public static partial EventJsonBench Circle(double radius);
    [Variant] public static partial EventJsonBench Label(string text);
    [Variant] public static partial EventJsonBench Rectangle(float width, float height);
    [Variant] public static partial EventJsonBench ColoredLine(int x1, int y1, string color);
    [Variant] public static partial EventJsonBench Transform(float x, float y, float z, float w);
    [Variant] public static partial EventJsonBench RichText(string text, int size, bool bold, string font, double spacing);
    [Variant] public static partial EventJsonBench Error(string message);
}
