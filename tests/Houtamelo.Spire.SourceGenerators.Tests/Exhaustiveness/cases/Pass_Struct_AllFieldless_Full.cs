//@ should_pass
// All-fieldless union all variants covered via (Kind.X, _) — no diagnostic
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct FieldlessLight
    {
        [Variant] public static partial FieldlessLight FieldlessRed();
        [Variant] public static partial FieldlessLight FieldlessYellow();
        [Variant] public static partial FieldlessLight FieldlessGreen();
    }

    class PassFieldlessLightConsumer
    {
        int Delay(FieldlessLight light) => light switch
        {
            (FieldlessLight.Kind.FieldlessRed, _) => 30,
            (FieldlessLight.Kind.FieldlessYellow, _) => 5,
            (FieldlessLight.Kind.FieldlessGreen, _) => 0,
        };
    }
}
