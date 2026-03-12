//@ should_pass
// Ensure that SPIRE005 is NOT triggered when T is an unconstrained generic type parameter.
public class NoReport_GenericTypeParam_GenericOverload
{
    public T Create<T>() where T : new()
    {
        return Activator.CreateInstance<T>();
    }
}
