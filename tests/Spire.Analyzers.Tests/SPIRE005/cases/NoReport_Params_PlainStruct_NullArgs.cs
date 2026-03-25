//@ should_pass
// Ensure that SPIRE005 is NOT triggered when CreateInstance targets a plain struct without [EnforceInitialization].
public class NoReport_Params_PlainStruct_NullArgs
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(PlainStruct), (object[])null);
    }
}
