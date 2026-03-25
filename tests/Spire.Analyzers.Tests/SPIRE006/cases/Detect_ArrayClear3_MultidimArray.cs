//@ should_fail
// Ensure that SPIRE006 IS triggered when Array.Clear(arr, 0, n) is called with a EnforceInitializationStruct[,] argument.
public class Detect_ArrayClear3_MultidimArray
{
    public void Method()
    {
        var arr = new EnforceInitializationStruct[3, 3];
        Array.Clear(arr, 0, arr.Length); //~ ERROR
    }
}
