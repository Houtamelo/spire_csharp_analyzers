//@ should_fail
// Ensure that SPIRE003 IS triggered when default is assigned to an out EnforceInitializationStruct parameter inside a method.
public class Detect_DefaultLiteral_OutParameterAssignment
{
    public void Method(out EnforceInitializationStruct s)
    {
        s = default; //~ ERROR
    }
}
