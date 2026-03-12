//@ should_pass
// Ensure that SPIRE003 is NOT triggered when `default` is assigned to an `EmptyMustInitStruct` variable, because the type is fieldless.
public class NoReport_EmptyMustInitStruct_DefaultLiteral
{
    public void Method()
    {
        EmptyMustInitStruct s = default;
        _ = s;
    }
}
