//@ should_fail
// Ensure that SPIRE006 IS triggered when Array.Clear(arr) appears in a static method.
public class Detect_ArrayClear1_InStaticMethod
{
    public static void Method()
    {
        var arr = new EnforceInitializationStruct[5];
        Array.Clear(arr); //~ ERROR
    }
}
