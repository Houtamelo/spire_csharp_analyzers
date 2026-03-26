//@ should_fail
// Ensure that SPIRE001 IS triggered when stackalloc-ing an array of a [EnforceInitialization] struct with an auto-property (has backing field).
public class Detect_AutoPropertyStruct_StackAlloc
{
    public void Method()
    {
        System.Span<EnforceInitializationStructWithAutoProperty> span = stackalloc EnforceInitializationStructWithAutoProperty[5]; //~ ERROR
    }
}
