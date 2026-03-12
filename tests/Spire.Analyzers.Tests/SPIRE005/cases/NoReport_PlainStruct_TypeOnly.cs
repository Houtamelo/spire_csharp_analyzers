//@ should_pass
// Ensure that SPIRE005 is NOT triggered when CreateInstance(typeof(PlainStruct)) is used on an unmarked struct.
public class NoReport_PlainStruct_TypeOnly
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(PlainStruct));
    }
}
