//@ not_exhaustive
// Missing { A: false, B: false } combination
public class TwoProperties_MissingCombo
{
    public int Test(TwoFlags f) => f switch
    {
        { A: true, B: true } => 1,
        { A: true, B: false } => 2,
        { A: false, B: true } => 3,
        //~ { A: false, B: false }
    };
}
