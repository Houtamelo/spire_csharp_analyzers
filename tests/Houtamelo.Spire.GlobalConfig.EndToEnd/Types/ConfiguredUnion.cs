using Houtamelo.Spire;

namespace Houtamelo.Spire.GlobalConfig.EndToEnd.Types;

// No explicit reference storage — should use Auto from global config
[DiscriminatedUnion]
public partial struct Shape
{
    [Variant] public static partial Shape Circle(float radius);
    [Variant] public static partial Shape Rect(float w, float h);
}

// Explicit Typed storage overrides global Auto default
[DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Typed)]
public partial struct Event
{
    [Variant] public static partial Event Click(int x, int y);
    [Variant] public static partial Event Key(char ch);
}
