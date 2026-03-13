//@ should_fail
// Ensure that SPIRE008 IS triggered when the call is passed as a method argument.
class TestClass
{
    void Consume(object o) { }

    void Method()
    {
        Consume(RuntimeHelpers.GetUninitializedObject(typeof(MustInitStruct))); //~ ERROR
    }
}
