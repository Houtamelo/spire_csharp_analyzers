//@ should_fail
// Ensure that SPIRE005 IS triggered when CreateInstance<MustInitStruct>() is inside a foreach loop body.
public class Detect_GenericOverload_ForeachLoop
{
    public void Method()
    {
        var list = new List<MustInitStruct>();
        foreach (var item in list)
        {
            var result = Activator.CreateInstance<MustInitStruct>(); //~ ERROR
        }
    }
}
