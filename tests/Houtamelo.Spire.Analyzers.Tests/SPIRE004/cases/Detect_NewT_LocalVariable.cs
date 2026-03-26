//@ should_fail
// Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor()` is assigned to a local variable.
public class Detect_NewT_LocalVariable
{
    public void Method()
    {
        var x = new EnforceInitializationNoCtor(); //~ ERROR
    }
}
