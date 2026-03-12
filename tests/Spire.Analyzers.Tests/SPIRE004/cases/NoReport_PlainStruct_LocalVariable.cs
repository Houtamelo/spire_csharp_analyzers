//@ should_pass
// Ensure that SPIRE004 is NOT triggered when new T() is used on a plain struct without [MustBeInit].
public class NoReport_PlainStruct_LocalVariable
{
    public void Method()
    {
        var x = new PlainStruct();
    }
}
