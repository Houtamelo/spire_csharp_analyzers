//@ exhaustive
// Only matching on A; B is implicitly wildcarded
public class TwoProperties_WildcardSecond
{
    public int Test(TwoFlags f) => f switch
    {
        { A: true } => 1,
        { A: false } => 2,
    };
}
