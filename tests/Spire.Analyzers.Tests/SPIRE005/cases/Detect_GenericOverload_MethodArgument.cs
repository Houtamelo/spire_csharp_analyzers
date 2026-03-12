//@ should_fail
// Ensure that SPIRE005 IS triggered when CreateInstance<MustInitStruct>() is passed as a method argument.
public class Detect_GenericOverload_MethodArgument
{
    public void Consume(MustInitStruct s) { }

    public void Method()
    {
        Consume(Activator.CreateInstance<MustInitStruct>()); //~ ERROR
    }
}
