//@ should_pass
// Ensure that SPIRE005 is NOT triggered when Activator.CreateInstance(t, true) is called with a non-statically-resolvable Type variable.
public class NoReport_NonPublic_TypeVariable
{
    public void Method(Type t)
    {
        var x = Activator.CreateInstance(t, true);
    }
}
