//@ should_fail
// Ensure that SPIRE004 IS triggered when `new MustInitNoCtor { }` (object initializer, no parens) is assigned to a local variable.
public class Detect_ObjectInit_LocalVariable
{
    public void Method()
    {
        var x = new MustInitNoCtor { }; //~ ERROR
    }
}
