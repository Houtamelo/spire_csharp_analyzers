//@ should_pass
// Ensure that SPIRE005 is NOT triggered when CreateInstance(typeof(EmptyMustInitStruct), (object[])null) is used on a fieldless [MustBeInit] struct.
public class NoReport_EmptyMustInit_ParamsNullArgs
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(EmptyMustInitStruct), (object[])null);
    }
}
