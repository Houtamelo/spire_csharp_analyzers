//@ should_pass
// Ensure that SPIRE003 is NOT triggered when `return default` is used in an unconstrained generic method returning `T`.
public class NoReport_GenericTypeParam_DefaultLiteral
{
    public T Method<T>()
    {
        return default;
    }
}
