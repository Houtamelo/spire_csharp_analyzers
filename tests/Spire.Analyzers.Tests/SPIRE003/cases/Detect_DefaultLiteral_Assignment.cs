//@ should_fail
// Ensure that SPIRE003 IS triggered when default is assigned to an existing EnforceInitializationStruct variable (standalone assignment).
public class Detect_DefaultLiteral_Assignment
{
    public void Method()
    {
        EnforceInitializationStruct c = new EnforceInitializationStruct(1);
        c = default; //~ ERROR
    }
}
