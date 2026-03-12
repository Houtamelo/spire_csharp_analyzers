//@ should_pass
// Ensure that SPIRE005 is NOT triggered when CreateInstance targets EmptyMustInitStruct with (object[])null.
public class NoReport_Params_EmptyMustInitStruct_NullArgs
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(EmptyMustInitStruct), (object[])null);
    }
}
