//@ should_fail
// Ensure that SPIRE005 IS triggered when Activator.CreateInstance(typeof(EnforceInitializationReadonlyStruct)) is used on a [EnforceInitialization] readonly struct.
public class Detect_TypeOnly_ReadonlyStruct
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(EnforceInitializationReadonlyStruct)); //~ ERROR
    }
}
