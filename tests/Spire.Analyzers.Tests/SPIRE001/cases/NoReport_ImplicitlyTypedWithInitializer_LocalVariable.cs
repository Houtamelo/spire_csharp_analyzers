//@ should_pass
// Ensure that SPIRE001 is NOT triggered when creating an implicitly-typed array with an explicit initializer.
public class NoReport_ImplicitlyTypedWithInitializer_LocalVariable
{
    public void Method()
    {
        var arr = new[] { new MustInitStruct(1), new MustInitStruct(2) };
    }
}
