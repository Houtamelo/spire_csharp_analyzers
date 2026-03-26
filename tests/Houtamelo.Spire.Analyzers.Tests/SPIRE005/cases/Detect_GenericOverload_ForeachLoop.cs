//@ should_fail
// Ensure that SPIRE005 IS triggered when CreateInstance<EnforceInitializationStruct>() is inside a foreach loop body.
public class Detect_GenericOverload_ForeachLoop
{
    public void Method()
    {
        var list = new List<EnforceInitializationStruct>();
        foreach (var item in list)
        {
            var result = Activator.CreateInstance<EnforceInitializationStruct>(); //~ ERROR
        }
    }
}
