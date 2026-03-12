//@ should_fail
// Ensure that SPIRE005 IS triggered when Activator.CreateInstance(typeof(MustInitStruct), nonPublic: false) is called.
public class Detect_NonPublic_FalseFlag
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(MustInitStruct), false); //~ ERROR
    }
}
