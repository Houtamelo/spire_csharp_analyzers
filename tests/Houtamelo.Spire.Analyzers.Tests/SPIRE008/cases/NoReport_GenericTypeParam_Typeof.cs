//@ should_pass
// Ensure that SPIRE008 is NOT triggered when typeof(T) is used inside a generic method where T is an unconstrained type parameter.
public class NoReport_GenericTypeParam_Typeof
{
    void Foo<T>()
    {
        var obj = RuntimeHelpers.GetUninitializedObject(typeof(T));
    }
}
