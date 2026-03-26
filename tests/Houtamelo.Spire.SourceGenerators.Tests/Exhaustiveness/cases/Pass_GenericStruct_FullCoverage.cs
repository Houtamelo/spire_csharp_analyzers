//@ should_pass
// Generic struct Option<T> all variants covered — no diagnostic
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct GenericOption<T>
    {
        [Variant] public static partial GenericOption<T> GenericSome(T genVal);
        [Variant] public static partial GenericOption<T> GenericNone();
    }

    class PassGenericOptionConsumer
    {
        int Unwrap(GenericOption<int> opt) => opt switch
        {
            (GenericOption<int>.Kind.GenericSome, int v) => v,
            (GenericOption<int>.Kind.GenericNone, _) => -1,
        };
    }
}
