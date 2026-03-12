//@ should_pass
// Ensure that SPIRE005 is NOT triggered when CreateInstance<List<int>>() is used on a reference type.
public class NoReport_ClassType_GenericOverload
{
    public void Method()
    {
        var x = Activator.CreateInstance<List<int>>();
    }
}
