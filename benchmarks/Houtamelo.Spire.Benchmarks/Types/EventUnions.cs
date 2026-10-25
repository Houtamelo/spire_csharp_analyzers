using Houtamelo.Spire;

namespace Houtamelo.Spire.Benchmarks.Types;

// A single declaration is expanded into EventBench (Auto), EventAuto,
// EventTyped, EventShared, and EventRecord.
[BenchmarkUnion(Name = "Event")]
public partial struct EventBench
{
    [Variant] public static partial EventBench Point();
    [Variant] public static partial EventBench Circle(double radius);
    [Variant] public static partial EventBench Label(string text);
    [Variant] public static partial EventBench Rectangle(float width, float height);
    [Variant] public static partial EventBench ColoredLine(int x1, int y1, string color);
    [Variant] public static partial EventBench Transform(float x, float y, float z, float w);
    [Variant] public static partial EventBench RichText(string text, int size, bool bold, string font, double spacing);
    [Variant] public static partial EventBench Error(string message);
}
