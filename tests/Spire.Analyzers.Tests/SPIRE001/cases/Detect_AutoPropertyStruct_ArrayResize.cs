//@ should_fail
// Ensure that SPIRE001 IS triggered when using Array.Resize with a [MustBeInit] struct with an auto-property (has backing field).
public class Detect_AutoPropertyStruct_ArrayResize
{
    public void Method()
    {
        var arr = new MustInitStructWithAutoProperty[0];
        System.Array.Resize(ref arr, 10); //~ ERROR
    }
}
