//@ should_fail
// Ensure that SPIRE005 IS triggered when Activator.CreateInstance(typeof(EnforceInitializationStruct), nonPublic: false) is called.
public class Detect_NonPublic_FalseFlag
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(EnforceInitializationStruct), false); //~ ERROR
    }
}
