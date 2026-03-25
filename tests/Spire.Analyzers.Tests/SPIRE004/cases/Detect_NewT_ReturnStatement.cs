//@ should_fail
// Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` is used in a return statement.
public class Detect_NewT_ReturnStatement
{
    public EnforceInitializationNoCtor Method()
    {
        return new EnforceInitializationNoCtor(); //~ ERROR
    }
}
