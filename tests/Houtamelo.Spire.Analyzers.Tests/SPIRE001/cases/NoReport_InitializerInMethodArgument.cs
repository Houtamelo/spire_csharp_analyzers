//@ should_pass
// Ensure that SPIRE001 is NOT triggered when creating an array with an explicit initializer as a method argument.
public class NoReport_InitializerInMethodArgument
{
    public void Consume(EnforceInitializationStruct[] arr) { }

    public void Method()
    {
        Consume(new EnforceInitializationStruct[1] { new EnforceInitializationStruct(1) });
    }
}
