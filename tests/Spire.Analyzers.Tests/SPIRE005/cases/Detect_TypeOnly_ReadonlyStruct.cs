//@ should_fail
// Ensure that SPIRE005 IS triggered when Activator.CreateInstance(typeof(MustInitReadonlyStruct)) is used on a [MustBeInit] readonly struct.
public class Detect_TypeOnly_ReadonlyStruct
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(MustInitReadonlyStruct)); //~ ERROR
    }
}
