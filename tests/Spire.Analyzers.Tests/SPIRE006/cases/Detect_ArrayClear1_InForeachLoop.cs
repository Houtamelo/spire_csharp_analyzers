//@ should_fail
// Ensure that SPIRE006 IS triggered when Array.Clear(arr) appears inside a foreach loop body.
public class Detect_ArrayClear1_InForeachLoop
{
    public void Method()
    {
        var arr = new MustInitStruct[5];
        var items = new[] { 1, 2, 3 };
        foreach (var item in items)
        {
            Array.Clear(arr); //~ ERROR
        }
    }
}
