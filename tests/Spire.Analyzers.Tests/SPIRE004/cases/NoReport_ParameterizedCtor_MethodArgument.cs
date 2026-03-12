//@ should_pass
// Ensure that SPIRE004 is NOT triggered when a parameterized constructor is passed as a method argument for a [MustBeInit] struct.
public class NoReport_ParameterizedCtor_MethodArgument
{
    public void Consume(MustInitNoCtor value) { }

    public void Method()
    {
        Consume(new MustInitNoCtor(42, "hello"));
    }
}
