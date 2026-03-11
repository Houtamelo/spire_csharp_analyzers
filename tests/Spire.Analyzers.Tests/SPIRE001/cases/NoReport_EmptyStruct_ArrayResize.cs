//@ should_pass
// Ensure that SPIRE001 is NOT triggered when using Array.Resize with a [MustBeInit] struct with no fields.
public class NoReport_EmptyStruct_ArrayResize
{
    public void Method()
    {
        var arr = new EmptyMustInitStruct[0];
        System.Array.Resize(ref arr, 10);
    }
}
