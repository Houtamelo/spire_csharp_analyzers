//@ should_pass
// Ensure that SPIRE005 is NOT triggered when the Type argument is a variable in the nonPublic overload.
public class NoReport_TypeVariable_NonPublic
{
    public void Method()
    {
        Type t = typeof(EnforceInitializationStruct);
        var x = Activator.CreateInstance(t, nonPublic: true);
    }
}
