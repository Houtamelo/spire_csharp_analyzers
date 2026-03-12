//@ should_fail
// Ensure that SPIRE005 IS triggered when Activator.CreateInstance(typeof(MustInitStruct)) is inside a static method of a static class.
public static class Detect_TypeOnly_NestedInStaticClass
{
    public static void Method()
    {
        var result = Activator.CreateInstance(typeof(MustInitStruct)); //~ ERROR
    }
}
