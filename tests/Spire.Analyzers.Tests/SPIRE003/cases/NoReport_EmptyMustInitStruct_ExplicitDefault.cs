//@ should_pass
// Ensure that SPIRE003 is NOT triggered when `default(EmptyMustInitStruct)` is used, because EmptyMustInitStruct is fieldless so default is its only value.
public class NoReport_EmptyMustInitStruct_ExplicitDefault
{
    public void Method()
    {
        var s = default(EmptyMustInitStruct);
        _ = s;
    }
}
