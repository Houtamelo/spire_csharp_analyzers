//@ should_fail
// Ensure that SPIRE005 IS triggered when Activator.CreateInstance(typeof(EnforceInitializationStruct)) is assigned to a local variable.
public class Detect_TypeOnly_LocalVariable
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(EnforceInitializationStruct)); //~ ERROR
    }
}
