//@ should_fail
// Ensure that SPIRE001 IS triggered when using ArrayPool.Rent with a [EnforceInitialization] struct with an auto-property (has backing field).
public class Detect_AutoPropertyStruct_ArrayPoolRent
{
    public void Method()
    {
        var arr = System.Buffers.ArrayPool<EnforceInitializationStructWithAutoProperty>.Shared.Rent(5); //~ ERROR
    }
}
