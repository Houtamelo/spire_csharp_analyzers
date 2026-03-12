//@ should_pass
// Ensure that SPIRE004 is NOT triggered when a parameterized constructor is returned for a [MustBeInit] struct.
public class NoReport_ParameterizedCtor_ReturnStatement
{
    public MustInitNoCtor Method()
    {
        return new MustInitNoCtor(42, "hello");
    }
}
