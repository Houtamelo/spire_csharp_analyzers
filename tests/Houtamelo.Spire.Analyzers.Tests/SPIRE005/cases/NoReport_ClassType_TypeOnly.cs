//@ should_pass
// Ensure that SPIRE005 is NOT triggered when CreateInstance(typeof(object)) is used on a class.
public class NoReport_ClassType_TypeOnly
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(object));
    }
}
