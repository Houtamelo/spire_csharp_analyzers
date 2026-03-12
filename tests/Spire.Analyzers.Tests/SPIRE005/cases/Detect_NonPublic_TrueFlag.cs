//@ should_fail
// Ensure that SPIRE005 IS triggered when Activator.CreateInstance(typeof(MustInitStruct), nonPublic: true) is called.
public class Detect_NonPublic_TrueFlag
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(MustInitStruct), true); //~ ERROR
    }
}
