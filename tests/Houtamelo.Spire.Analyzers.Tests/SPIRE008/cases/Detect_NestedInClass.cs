//@ should_fail
// Ensure that SPIRE008 IS triggered when GetUninitializedObject is called inside a method of a nested inner class.
class OuterClass
{
    class InnerClass
    {
        void Method()
        {
            var obj = RuntimeHelpers.GetUninitializedObject(typeof(EnforceInitializationStruct)); //~ ERROR
        }
    }
}
