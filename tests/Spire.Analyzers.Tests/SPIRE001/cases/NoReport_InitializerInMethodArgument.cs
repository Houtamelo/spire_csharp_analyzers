//@ should_pass
// Ensure that SPIRE001 is NOT triggered when creating an array with an explicit initializer as a method argument.
public class NoReport_InitializerInMethodArgument
{
    public void Consume(MustInitStruct[] arr) { }

    public void Method()
    {
        Consume(new MustInitStruct[1] { new MustInitStruct(1) });
    }
}
