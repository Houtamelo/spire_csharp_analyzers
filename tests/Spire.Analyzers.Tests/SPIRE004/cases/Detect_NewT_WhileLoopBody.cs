//@ should_fail
// Ensure that SPIRE004 IS triggered when new MustInitNoCtor() appears inside a while loop body.
public class Detect_NewT_WhileLoopBody
{
    public void Method(bool condition)
    {
        MustInitNoCtor x;
        while (condition)
        {
            x = new MustInitNoCtor(); //~ ERROR
        }
    }
}
