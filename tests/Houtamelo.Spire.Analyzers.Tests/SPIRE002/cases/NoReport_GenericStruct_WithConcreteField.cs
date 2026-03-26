//@ should_pass
// Ensure that SPIRE002 is NOT triggered when [EnforceInitialization] is applied to a generic struct S<T> that has an explicit int instance field.
[EnforceInitialization]
public struct SWithConcreteField<T>
{
    public int X;
}
