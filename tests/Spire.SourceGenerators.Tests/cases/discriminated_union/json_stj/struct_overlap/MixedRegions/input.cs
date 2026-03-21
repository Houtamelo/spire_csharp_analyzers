using Spire;

namespace TestNs;

[DiscriminatedUnion(Json = JsonLibrary.SystemTextJson)]
partial struct Event
{
    [Variant] public static partial Event Click(int x, int y, string target);
    [Variant] public static partial Event Hover(float posX, float posY);
}
