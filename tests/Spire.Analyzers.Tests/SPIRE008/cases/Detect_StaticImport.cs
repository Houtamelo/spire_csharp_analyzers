//@ should_fail
// Ensure that SPIRE008 IS triggered when GetUninitializedObject is called via a using static import.
using static System.Runtime.CompilerServices.RuntimeHelpers;

class TestClass
{
    void Method()
    {
        var obj = GetUninitializedObject(typeof(MustInitStruct)); //~ ERROR
    }
}
