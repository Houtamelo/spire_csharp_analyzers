//@ should_pass
// Ensure that SPIRE005 is NOT triggered when the type comes from a method return value.
public class NoReport_MethodReturnType_TypeOnly
{
    private static Type GetSomeType() => typeof(MustInitStruct);

    public void Method()
    {
        var x = Activator.CreateInstance(GetSomeType());
    }
}
