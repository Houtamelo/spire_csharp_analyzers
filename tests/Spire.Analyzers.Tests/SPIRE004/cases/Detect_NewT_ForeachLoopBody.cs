//@ should_fail
// Ensure that SPIRE004 IS triggered when new MustInitNoCtor() appears inside a foreach loop body.
public class Detect_NewT_ForeachLoopBody
{
    public void Method(int[] items)
    {
        MustInitNoCtor x;
        foreach (int item in items)
        {
            x = new MustInitNoCtor(); //~ ERROR
        }
    }
}
