//@ should_fail
// Ensure that SPIRE008 IS triggered when the call appears in a switch expression arm.
class TestClass
{
    object Method(int value)
    {
        return value switch
        {
            1 => RuntimeHelpers.GetUninitializedObject(typeof(EnforceInitializationStruct)), //~ ERROR
            _ => null,
        };
    }
}
