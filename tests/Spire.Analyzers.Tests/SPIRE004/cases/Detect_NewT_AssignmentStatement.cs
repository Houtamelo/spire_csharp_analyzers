//@ should_fail
// Ensure that SPIRE004 IS triggered when new MustInitNoCtor() appears in a standalone assignment statement.
public class Detect_NewT_AssignmentStatement
{
    public void Method()
    {
        MustInitNoCtor x;
        x = new MustInitNoCtor(); //~ ERROR
    }
}
