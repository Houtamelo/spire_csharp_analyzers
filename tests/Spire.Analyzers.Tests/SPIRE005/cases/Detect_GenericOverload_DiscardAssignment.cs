//@ should_fail
// Ensure that SPIRE005 IS triggered when CreateInstance<EnforceInitializationStruct>() result is discarded.
public class Detect_GenericOverload_DiscardAssignment
{
    public void Method()
    {
        _ = Activator.CreateInstance<EnforceInitializationStruct>(); //~ ERROR
    }
}
