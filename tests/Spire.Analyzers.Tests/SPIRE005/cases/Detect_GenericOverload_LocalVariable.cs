//@ should_fail
// Ensure that SPIRE005 IS triggered when CreateInstance<MustInitStruct>() is assigned to a local variable.
public class Detect_GenericOverload_LocalVariable
{
    public void Method()
    {
        var x = Activator.CreateInstance<MustInitStruct>(); //~ ERROR
    }
}
