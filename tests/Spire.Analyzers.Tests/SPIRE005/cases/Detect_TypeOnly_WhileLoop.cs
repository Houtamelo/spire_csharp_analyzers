//@ should_fail
// Ensure that SPIRE005 IS triggered when Activator.CreateInstance(typeof(MustInitStruct)) is inside a while loop body.
public class Detect_TypeOnly_WhileLoop
{
    public void Method(bool condition)
    {
        while (condition)
        {
            var result = Activator.CreateInstance(typeof(MustInitStruct)); //~ ERROR
        }
    }
}
