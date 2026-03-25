//@ should_pass
// Ensure that SPIRE008 is NOT triggered when the target is a plain struct without [EnforceInitialization].
public class NoReport_PlainStruct
{
    void Method()
    {
        var obj = RuntimeHelpers.GetUninitializedObject(typeof(PlainStruct));
    }
}
