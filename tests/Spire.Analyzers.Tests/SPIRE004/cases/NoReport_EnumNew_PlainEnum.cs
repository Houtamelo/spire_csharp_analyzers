//@ should_pass
// Ensure that SPIRE004 is NOT triggered when using new T() on a plain enum not marked with [EnforceInitialization].
public class NoReport_EnumNew_PlainEnum
{
    void M()
    {
        var x = new PlainEnum();
    }
}
