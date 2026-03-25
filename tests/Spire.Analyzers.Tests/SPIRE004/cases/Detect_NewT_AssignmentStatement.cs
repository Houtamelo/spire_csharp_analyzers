//@ should_fail
// Ensure that SPIRE004 IS triggered when new EnforceInitializationNoCtor() appears in a standalone assignment statement.
public class Detect_NewT_AssignmentStatement
{
    public void Method()
    {
        EnforceInitializationNoCtor x;
        x = new EnforceInitializationNoCtor(); //~ ERROR
    }
}
