//@ should_pass
// Ensure that SPIRE004 is NOT triggered when a parameterized constructor is returned for a [EnforceInitialization] struct.
public class NoReport_ParameterizedCtor_ReturnStatement
{
    public EnforceInitializationNoCtor Method()
    {
        return new EnforceInitializationNoCtor(42, "hello");
    }
}
