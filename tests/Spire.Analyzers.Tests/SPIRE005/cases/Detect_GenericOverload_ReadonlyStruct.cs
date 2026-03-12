//@ should_fail
// Ensure that SPIRE005 IS triggered when CreateInstance<MustInitReadonlyStruct>() is used on a [MustBeInit] readonly struct.
public class Detect_GenericOverload_ReadonlyStruct
{
    public void Method()
    {
        var x = Activator.CreateInstance<MustInitReadonlyStruct>(); //~ ERROR
    }
}
