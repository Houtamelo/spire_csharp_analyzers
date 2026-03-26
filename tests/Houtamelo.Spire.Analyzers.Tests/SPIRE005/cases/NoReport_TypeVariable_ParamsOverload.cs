//@ should_pass
// Ensure that SPIRE005 is NOT triggered when the Type argument is a variable in the params overload.
public class NoReport_TypeVariable_ParamsOverload
{
    public void Method()
    {
        Type t = typeof(EnforceInitializationStruct);
        var x = Activator.CreateInstance(t, (object[])null);
    }
}
