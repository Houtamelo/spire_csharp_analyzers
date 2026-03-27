//@ exhaustive
// All concrete subtypes covered via type patterns
#nullable enable
public class AllSubtypes
{
    public int Test(Animal a) => a switch
    {
        Dog => 1,
        Cat => 2,
        Bird => 3,
    };
}
