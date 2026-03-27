//@ exhaustive
// All 4 combinations of two bool properties
public class TwoProperties_AllCombos
{
    public int Test(TwoFlags f) => f switch
    {
        { A: true, B: true } => 1,
        { A: true, B: false } => 2,
        { A: false, B: true } => 3,
        { A: false, B: false } => 4,
    };
}
