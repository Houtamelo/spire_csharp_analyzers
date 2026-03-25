//@ should_fail
// Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor { }` (object initializer, no parens) is assigned to a local variable.
public class Detect_ObjectInit_LocalVariable
{
    public void Method()
    {
        var x = new EnforceInitializationNoCtor { }; //~ ERROR
    }
}
