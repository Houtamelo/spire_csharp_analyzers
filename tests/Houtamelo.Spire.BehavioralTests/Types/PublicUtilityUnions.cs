using Houtamelo.Spire;

namespace Spire.BehavioralTests.Types;

[DiscriminatedUnion]
public partial struct PublicUtilityUnion
{
    [Variant] public static partial PublicUtilityUnion First(int id);
    [Variant] public static partial PublicUtilityUnion Second(int id);
    [Variant] public static partial PublicUtilityUnion Third(int id);
}
