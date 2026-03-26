//@ should_fail
// Ensure that SPIRE006 IS triggered when Array.Clear(arr) appears inside a while loop body.
public class Detect_ArrayClear1_InWhileLoop
{
    public void Method()
    {
        var arr = new EnforceInitializationStruct[5];
        bool condition = false;
        while (condition)
        {
            Array.Clear(arr); //~ ERROR
        }
    }
}
