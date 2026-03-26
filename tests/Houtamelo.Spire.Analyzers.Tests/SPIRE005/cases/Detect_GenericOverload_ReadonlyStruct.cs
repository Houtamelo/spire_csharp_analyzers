//@ should_fail
// Ensure that SPIRE005 IS triggered when CreateInstance<EnforceInitializationReadonlyStruct>() is used on a [EnforceInitialization] readonly struct.
public class Detect_GenericOverload_ReadonlyStruct
{
    public void Method()
    {
        var x = Activator.CreateInstance<EnforceInitializationReadonlyStruct>(); //~ ERROR
    }
}
