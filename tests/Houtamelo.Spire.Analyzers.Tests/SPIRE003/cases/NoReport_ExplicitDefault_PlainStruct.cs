//@ should_pass
// Ensure that SPIRE003 is NOT triggered when `default(PlainStruct)` is used and PlainStruct has no [EnforceInitialization] attribute.
public class NoReport_ExplicitDefault_PlainStruct
{
    public void Method()
    {
        var s = default(PlainStruct);
    }
}
