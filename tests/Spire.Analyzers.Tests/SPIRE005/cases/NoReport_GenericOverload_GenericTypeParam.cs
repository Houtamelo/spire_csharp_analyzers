//@ should_pass
// Ensure that SPIRE005 is NOT triggered when CreateInstance<T>() is used on an unresolvable generic type parameter.
public class NoReport_GenericOverload_GenericTypeParam
{
    public void Method<T>() where T : struct
    {
        var x = Activator.CreateInstance<T>();
    }
}
