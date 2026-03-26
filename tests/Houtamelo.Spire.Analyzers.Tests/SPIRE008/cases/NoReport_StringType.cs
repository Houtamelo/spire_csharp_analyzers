//@ should_pass
// Ensure that SPIRE008 is NOT triggered when the target is typeof(string).
public class NoReport_StringType
{
    void Method()
    {
        var obj = RuntimeHelpers.GetUninitializedObject(typeof(string));
    }
}
