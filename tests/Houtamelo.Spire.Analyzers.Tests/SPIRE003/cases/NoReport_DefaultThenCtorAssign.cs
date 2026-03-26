//@ should_pass
// Ensure that SPIRE003 is NOT triggered when default(T) variable is reassigned via constructor.
public class NoReport_DefaultThenCtorAssign
{
    public void Method()
    {
        var s = default(EnforceInitializationStruct);
        s = new EnforceInitializationStruct(42);
        _ = s;
    }
}
