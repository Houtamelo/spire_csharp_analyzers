//@ should_pass
// Ensure that SPIRE004 is NOT triggered when new T() is passed as a method argument for a plain struct without [EnforceInitialization].
public class NoReport_PlainStruct_MethodArgument
{
    public void Consume(PlainStruct value) { }

    public void Method()
    {
        Consume(new PlainStruct());
    }
}
