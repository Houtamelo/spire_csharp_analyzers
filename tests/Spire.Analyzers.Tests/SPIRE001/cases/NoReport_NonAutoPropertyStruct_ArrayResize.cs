//@ should_pass
// Ensure that SPIRE001 is NOT triggered when using Array.Resize with a [EnforceInitialization] struct whose only property is non-auto (no backing field).
public class NoReport_NonAutoPropertyStruct_ArrayResize
{
    public void Method()
    {
        var arr = new EnforceInitializationStructWithNonAutoProperty[0];
        System.Array.Resize(ref arr, 10);
    }
}
