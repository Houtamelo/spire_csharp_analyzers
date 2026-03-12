//@ should_fail
// Ensure that SPIRE004 IS triggered when `new MustInitNoCtor()` is assigned to a local variable.
public class Detect_NewT_LocalVariable
{
    public void Method()
    {
        var x = new MustInitNoCtor(); //~ ERROR
    }
}
