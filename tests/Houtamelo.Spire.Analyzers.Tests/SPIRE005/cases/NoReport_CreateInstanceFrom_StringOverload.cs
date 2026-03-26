//@ should_pass
// Ensure that SPIRE005 is NOT triggered for the Activator.CreateInstanceFrom(assemblyFile, typeName) overload.
public class NoReport_CreateInstanceFrom_StringOverload
{
    public void Method()
    {
        var x = Activator.CreateInstanceFrom("Some.dll", "SomeType");
    }
}
