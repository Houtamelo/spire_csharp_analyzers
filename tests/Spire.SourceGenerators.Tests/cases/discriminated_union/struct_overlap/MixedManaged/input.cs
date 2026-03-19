using Spire;

namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Event
    {
        [Variant] static partial void Click(int x, int y, string target);
        [Variant] static partial void Hover(float posX, float posY);
    }
}
