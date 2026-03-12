//@ should_fail
// Ensure that SPIRE004 IS triggered when new MustInitNoCtor() appears inside a for loop body.
public class Detect_NewT_ForLoopBody
{
    public void Method()
    {
        MustInitNoCtor x;
        for (int i = 0; i < 10; i++)
        {
            x = new MustInitNoCtor(); //~ ERROR
        }
    }
}
