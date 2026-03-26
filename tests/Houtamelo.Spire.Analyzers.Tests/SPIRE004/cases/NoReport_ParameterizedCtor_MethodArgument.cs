//@ should_pass
// Ensure that SPIRE004 is NOT triggered when a parameterized constructor is passed as a method argument for a [EnforceInitialization] struct.
public class NoReport_ParameterizedCtor_MethodArgument
{
    public void Consume(EnforceInitializationNoCtor value) { }

    public void Method()
    {
        Consume(new EnforceInitializationNoCtor(42, "hello"));
    }
}
