using Houtamelo.Spire;

namespace Houtamelo.Spire.GlobalConfig.EndToEnd.Types;

// No explicit Layout — should use Additive from global config
[DiscriminatedUnion]
public partial struct Shape
{
    [Variant] public static partial Shape Circle(float radius);
    [Variant] public static partial Shape Rect(float w, float h);
}
