//@ should_pass
// Ensure that SPIRE001 is NOT triggered when creating an array of a plain enum not marked with [EnforceInitialization].
public class NoReport_EnumArray_PlainEnum
{
    void M()
    {
        var arr = new PlainEnum[5];
    }
}
