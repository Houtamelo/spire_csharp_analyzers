//@ should_pass
// Ensure that SPIRE004 is NOT triggered when a parameterized constructor is used on a [EnforceInitialization] struct.
public class NoReport_ParameterizedCtor_LocalVariable
{
    public void Method()
    {
        var x = new EnforceInitializationNoCtor(42, "hello");
    }
}
