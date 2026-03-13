//@ should_pass
// Ensure that SPIRE008 is NOT triggered when the target is a fieldless [MustBeInit] struct (SPIRE002 handles this case).
public class NoReport_EmptyMustInitStruct
{
    void Method()
    {
        var obj = RuntimeHelpers.GetUninitializedObject(typeof(EmptyMustInitStruct));
    }
}
