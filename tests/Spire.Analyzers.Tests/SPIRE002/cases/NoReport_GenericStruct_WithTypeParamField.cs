//@ should_pass
// Ensure that SPIRE002 is NOT triggered when [EnforceInitialization] is applied to a generic struct S<T> that has an instance field of type T.
[EnforceInitialization]
public struct SWithTypeParamField<T>
{
    public T Value;
}
