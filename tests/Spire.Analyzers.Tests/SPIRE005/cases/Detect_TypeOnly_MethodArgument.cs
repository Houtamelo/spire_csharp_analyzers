//@ should_fail
// Ensure that SPIRE005 IS triggered when Activator.CreateInstance(typeof(EnforceInitializationStruct)) is passed as a method argument.
public class Detect_TypeOnly_MethodArgument
{
    public void Consume(object o) { }

    public void Method()
    {
        Consume(Activator.CreateInstance(typeof(EnforceInitializationStruct))); //~ ERROR
    }
}
