//@ should_fail
// Ensure that SPIRE008 IS triggered when GetUninitializedObject is called via fully qualified type name.
class TestClass
{
    void Method()
    {
        var obj = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(EnforceInitializationStruct)); //~ ERROR
    }
}
