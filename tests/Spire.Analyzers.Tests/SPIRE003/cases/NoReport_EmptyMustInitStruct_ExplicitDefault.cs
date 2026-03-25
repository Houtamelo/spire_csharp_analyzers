//@ should_pass
// Ensure that SPIRE003 is NOT triggered when `default(EmptyEnforceInitializationStruct)` is used, because EmptyEnforceInitializationStruct is fieldless so default is its only value.
public class NoReport_EmptyEnforceInitializationStruct_ExplicitDefault
{
    public void Method()
    {
        var s = default(EmptyEnforceInitializationStruct);
        _ = s;
    }
}
