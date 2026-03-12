//@ should_pass
// Ensure that SPIRE005 is NOT triggered when CreateInstance(typeof(EmptyMustInitStruct)) is used on a fieldless [MustBeInit] struct.
public class NoReport_EmptyMustInit_TypeOnly
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(EmptyMustInitStruct));
    }
}
