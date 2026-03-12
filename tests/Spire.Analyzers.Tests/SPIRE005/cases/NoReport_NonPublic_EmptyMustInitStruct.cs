//@ should_pass
// Ensure that SPIRE005 is NOT triggered when Activator.CreateInstance(typeof(EmptyMustInitStruct), true) is called on a fieldless [MustBeInit] struct.
public class NoReport_NonPublic_EmptyMustInitStruct
{
    public void Method()
    {
        var x = Activator.CreateInstance(typeof(EmptyMustInitStruct), true);
    }
}
