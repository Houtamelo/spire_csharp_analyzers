//@ should_pass
// Ensure that SPIRE003 is NOT triggered when `default` is assigned to an `EmptyEnforceInitializationStruct` variable, because the type is fieldless.
public class NoReport_EmptyEnforceInitializationStruct_DefaultLiteral
{
    public void Method()
    {
        EmptyEnforceInitializationStruct s = default;
        _ = s;
    }
}
