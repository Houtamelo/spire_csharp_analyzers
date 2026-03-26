//@ should_fail
// Ensure that SPIRE004 IS triggered when new EnforceInitializationNoCtor() appears inside a while loop body.
public class Detect_NewT_WhileLoopBody
{
    public void Method(bool condition)
    {
        EnforceInitializationNoCtor x;
        while (condition)
        {
            x = new EnforceInitializationNoCtor(); //~ ERROR
        }
    }
}
