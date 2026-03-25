//@ should_pass
// Ensure that SPIRE003 is NOT triggered when `default` is assigned to a `EnforceInitializationStructWithNonAutoProperty` variable, because the type is fieldless.
public class NoReport_NonAutoPropertyStruct_DefaultLiteral
{
    public void Method()
    {
        EnforceInitializationStructWithNonAutoProperty s = default;
        _ = s;
    }
}
