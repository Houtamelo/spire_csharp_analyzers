//@ should_pass
// Ensure that SPIRE008 is NOT triggered when the target is a reference type.
public class NoReport_ClassType
{
    void Method()
    {
        var obj = RuntimeHelpers.GetUninitializedObject(typeof(object));
    }
}
