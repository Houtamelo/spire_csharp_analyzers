//@ should_pass
// Ensure that SPIRE005 is NOT triggered when CreateInstance<PlainStruct>() is used on an unmarked struct.
public class NoReport_GenericOverload_PlainStruct
{
    public void Method()
    {
        var x = Activator.CreateInstance<PlainStruct>();
    }
}
