//@ should_pass
// Ensure that SPIRE005 is NOT triggered for the CreateInstance(assemblyName, typeName) string-based overload.
public class NoReport_StringOverload_AssemblyAndTypeName
{
    public void Method()
    {
        var x = Activator.CreateInstance("System", "System.Int32");
    }
}
