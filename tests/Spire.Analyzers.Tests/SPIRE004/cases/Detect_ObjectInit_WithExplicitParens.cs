//@ should_fail
// Ensure that SPIRE004 IS triggered when `new EnforceInitializationNoCtor() { }` (explicit empty parens + empty initializer) is used in a local variable.
public class Detect_ObjectInit_WithExplicitParens
{
    public void Method()
    {
        var x = new EnforceInitializationNoCtor() { }; //~ ERROR
    }
}
