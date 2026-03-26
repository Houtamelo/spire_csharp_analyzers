//@ should_pass
// Ensure that SPIRE005 is NOT triggered when CreateInstance<PlainStruct>() is used on an unmarked struct.
public class NoReport_PlainStruct_GenericOverload
{
    public void Method()
    {
        var x = Activator.CreateInstance<PlainStruct>();
    }
}
