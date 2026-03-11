//@ should_pass
// Ensure that SPIRE002 is NOT triggered when [MustBeInit] is applied to a generic struct S<T> that has an explicit int instance field.
[MustBeInit]
public struct SWithConcreteField<T>
{
    public int X;
}
