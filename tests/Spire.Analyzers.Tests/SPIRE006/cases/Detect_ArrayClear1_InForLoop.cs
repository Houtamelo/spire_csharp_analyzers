//@ should_fail
// Ensure that SPIRE006 IS triggered when Array.Clear(arr) appears inside a for loop body.
public class Detect_ArrayClear1_InForLoop
{
    public void Method()
    {
        var arr = new MustInitStruct[5];
        for (int i = 0; i < 1; i++)
        {
            Array.Clear(arr); //~ ERROR
        }
    }
}
