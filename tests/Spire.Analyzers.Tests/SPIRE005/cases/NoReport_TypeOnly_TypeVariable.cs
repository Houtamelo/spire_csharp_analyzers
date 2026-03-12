//@ should_pass
// Ensure that SPIRE005 is NOT triggered when Activator.CreateInstance is called with a non-typeof Type variable.
public class NoReport_TypeOnly_TypeVariable
{
    public void Method()
    {
        Type t = typeof(MustInitStruct);
        var x = Activator.CreateInstance(t);
    }
}
