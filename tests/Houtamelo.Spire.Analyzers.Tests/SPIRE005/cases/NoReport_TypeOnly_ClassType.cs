//@ should_pass
// Ensure that SPIRE005 is NOT triggered when Activator.CreateInstance(typeof(object)) is used on a reference type.
public class NoReport_TypeOnly_ClassType
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(object));
    }
}
