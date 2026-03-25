//@ should_pass
// Ensure that SPIRE003 is NOT triggered when `default(T)` is used in an unconstrained generic method, because T is not a concrete [EnforceInitialization] type at the definition site.
public class NoReport_GenericTypeParam_ExplicitDefault
{
    public T Method<T>()
    {
        return default(T);
    }
}
