//@ should_pass
// Ensure that SPIRE003 is NOT triggered when `default(T)` is used in a generic method where T has no [EnforceInitialization] constraint.
public class NoReport_ExplicitDefault_GenericTypeParam
{
    public T Method<T>()
    {
        return default(T);
    }
}
