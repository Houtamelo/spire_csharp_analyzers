//@ should_pass
// Ensure that SPIRE005 is NOT triggered when CreateInstance<int>() is used on a built-in value type.
public class NoReport_BuiltinInt_GenericOverload
{
    public void Method()
    {
        var x = Activator.CreateInstance<int>();
    }
}
