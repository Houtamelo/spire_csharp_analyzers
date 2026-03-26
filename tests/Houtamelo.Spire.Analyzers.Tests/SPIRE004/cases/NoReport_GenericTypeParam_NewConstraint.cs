//@ should_pass
// Ensure that SPIRE004 is NOT triggered when new T() is used on a generic type parameter constrained with new(), not a concrete [EnforceInitialization] struct.
public class NoReport_GenericTypeParam_NewConstraint
{
    public T Create<T>() where T : new()
    {
        return new T();
    }
}
