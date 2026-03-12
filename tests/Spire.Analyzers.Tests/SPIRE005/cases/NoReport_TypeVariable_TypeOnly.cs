//@ should_pass
// Ensure that SPIRE005 is NOT triggered when the Type argument is a local variable.
public class NoReport_TypeVariable_TypeOnly
{
    public void Method()
    {
        Type t = typeof(MustInitStruct);
        var x = Activator.CreateInstance(t);
    }
}
