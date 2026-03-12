//@ should_pass
// Ensure that SPIRE005 is NOT triggered when Activator.CreateInstance(typeof(int)) is used on a built-in value type.
public class NoReport_TypeOnly_BuiltinType
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(int));
    }
}
