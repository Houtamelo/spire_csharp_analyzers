//@ should_pass
// Ensure that SPIRE003 is NOT triggered when `default(EnforceInitializationStructWithNonAutoProperty)` is used, because the type has no instance fields (computed property only).
public class NoReport_NonAutoPropertyStruct_ExplicitDefault
{
    public void Method()
    {
        var s = default(EnforceInitializationStructWithNonAutoProperty);
        _ = s;
    }
}
