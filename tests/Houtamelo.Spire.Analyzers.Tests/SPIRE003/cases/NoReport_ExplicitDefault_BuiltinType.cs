//@ should_pass
// Ensure that SPIRE003 is NOT triggered when `default(int)` is used with a built-in numeric type.
public class NoReport_ExplicitDefault_BuiltinType
{
    public void Method()
    {
        var n = default(int);
    }
}
