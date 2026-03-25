//@ should_pass
// Ensure that SPIRE008 is NOT triggered when the target is a fieldless [EnforceInitialization] struct (SPIRE002 handles this case).
public class NoReport_EmptyEnforceInitializationStruct
{
    void Method()
    {
        var obj = RuntimeHelpers.GetUninitializedObject(typeof(EmptyEnforceInitializationStruct));
    }
}
