//@ should_pass
// Ensure that SPIRE002 is NOT triggered when [MustBeInit] is applied to a generic struct S<T> that has an instance field of type T.
[MustBeInit]
public struct SWithTypeParamField<T>
{
    public T Value;
}
