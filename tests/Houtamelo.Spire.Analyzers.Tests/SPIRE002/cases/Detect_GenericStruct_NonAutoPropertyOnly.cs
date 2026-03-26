//@ should_fail
// Ensure that SPIRE002 IS triggered when [EnforceInitialization] is applied to a generic struct S<T> whose only member is a non-auto property.
[EnforceInitialization] //~ ERROR
public struct SWithNonAutoProp<T>
{
    public T Value => default;
}
