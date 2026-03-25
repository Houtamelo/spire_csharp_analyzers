//@ should_fail
// Ensure that SPIRE005 IS triggered when Activator.CreateInstance(typeof(EnforceInitializationStruct), true) appears in a return statement.
public class Detect_NonPublic_ReturnStatement
{
    public object Method()
    {
        return Activator.CreateInstance(typeof(EnforceInitializationStruct), true); //~ ERROR
    }
}
