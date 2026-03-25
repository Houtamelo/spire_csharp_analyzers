//@ should_fail
// Ensure that SPIRE004 IS triggered when new EnforceInitializationNoCtor() appears inside a foreach loop body.
public class Detect_NewT_ForeachLoopBody
{
    public void Method(int[] items)
    {
        EnforceInitializationNoCtor x;
        foreach (int item in items)
        {
            x = new EnforceInitializationNoCtor(); //~ ERROR
        }
    }
}
