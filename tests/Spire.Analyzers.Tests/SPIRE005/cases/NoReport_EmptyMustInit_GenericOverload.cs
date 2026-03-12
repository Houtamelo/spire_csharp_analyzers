//@ should_pass
// Ensure that SPIRE005 is NOT triggered when CreateInstance<EmptyMustInitStruct>() is used on a fieldless [MustBeInit] struct.
public class NoReport_EmptyMustInit_GenericOverload
{
    public void Method()
    {
        var x = Activator.CreateInstance<EmptyMustInitStruct>();
    }
}
