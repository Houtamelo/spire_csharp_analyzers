//@ should_fail
// Ensure that SPIRE008 IS triggered when the call appears as the right-hand side of a null-coalescing expression.
class TestClass
{
    object Method(object value)
    {
        return value ?? RuntimeHelpers.GetUninitializedObject(typeof(EnforceInitializationStruct)); //~ ERROR
    }
}
