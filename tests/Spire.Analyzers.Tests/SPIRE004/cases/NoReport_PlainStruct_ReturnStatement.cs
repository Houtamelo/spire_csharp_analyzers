//@ should_pass
// Ensure that SPIRE004 is NOT triggered when new T() is returned for a plain struct without [EnforceInitialization].
public class NoReport_PlainStruct_ReturnStatement
{
    public PlainStruct Method()
    {
        return new PlainStruct();
    }
}
