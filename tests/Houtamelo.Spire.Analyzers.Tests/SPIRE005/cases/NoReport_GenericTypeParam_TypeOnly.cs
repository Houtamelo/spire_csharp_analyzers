//@ should_pass
// Ensure that SPIRE005 is NOT triggered when typeof(T) is used with a generic type parameter.
public class NoReport_GenericTypeParam_TypeOnly
{
    public void Method<T>()
    {
        var x = Activator.CreateInstance(typeof(T));
    }
}
