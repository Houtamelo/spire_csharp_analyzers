//@ should_pass
// Ensure that SPIRE005 is NOT triggered when Activator.CreateInstance(typeof(PlainStruct), true) is called on a non-[MustBeInit] struct.
public class NoReport_NonPublic_PlainStruct
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(PlainStruct), true);
    }
}
