//@ should_pass
// Ensure that SPIRE005 is NOT triggered when CreateInstance(typeof(string)) is used on a string type.
public class NoReport_StringType_TypeOnly
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(string));
    }
}
