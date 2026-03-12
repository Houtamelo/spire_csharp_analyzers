//@ should_pass
// Ensure that SPIRE005 is NOT triggered when CreateInstance<string>() is used on a reference type.
public class NoReport_GenericOverload_ClassType
{
    public void Method()
    {
        var x = Activator.CreateInstance<string>();
    }
}
