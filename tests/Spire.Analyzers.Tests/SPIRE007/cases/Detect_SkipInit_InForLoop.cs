//@ should_fail
// Ensure that SPIRE007 IS triggered when Unsafe.SkipInit appears inside a for loop.
public class Detect_SkipInit_InForLoop
{
    public void Method()
    {
        for (int i = 0; i < 10; i++)
        {
            Unsafe.SkipInit(out MustInitStruct s); //~ ERROR
        }
    }
}
