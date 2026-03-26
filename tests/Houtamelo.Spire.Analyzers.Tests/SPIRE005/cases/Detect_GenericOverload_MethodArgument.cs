//@ should_fail
// Ensure that SPIRE005 IS triggered when CreateInstance<EnforceInitializationStruct>() is passed as a method argument.
public class Detect_GenericOverload_MethodArgument
{
    public void Consume(EnforceInitializationStruct s) { }

    public void Method()
    {
        Consume(Activator.CreateInstance<EnforceInitializationStruct>()); //~ ERROR
    }
}
