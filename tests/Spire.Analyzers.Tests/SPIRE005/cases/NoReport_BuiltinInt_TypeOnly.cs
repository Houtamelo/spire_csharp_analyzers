//@ should_pass
// Ensure that SPIRE005 is NOT triggered when CreateInstance(typeof(long)) is used on a built-in value type.
public class NoReport_BuiltinInt_TypeOnly
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(long));
    }
}
