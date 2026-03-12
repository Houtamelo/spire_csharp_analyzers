//@ should_fail
// Ensure that SPIRE005 IS triggered when Activator.CreateInstance(typeof(MustInitStruct)) is assigned to a local variable.
public class Detect_TypeOnly_LocalVariable
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(MustInitStruct)); //~ ERROR
    }
}
