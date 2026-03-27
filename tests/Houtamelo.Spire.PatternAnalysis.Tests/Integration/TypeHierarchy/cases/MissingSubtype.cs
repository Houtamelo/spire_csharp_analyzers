//@ not_exhaustive
// Missing Bird
#nullable enable
public class MissingSubtype
{
    public int Test(Animal a) => a switch
    {
        Dog => 1,
        Cat => 2,
    };
}
