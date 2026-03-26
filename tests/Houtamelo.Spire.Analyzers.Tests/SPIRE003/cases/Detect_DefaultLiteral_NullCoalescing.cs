//@ should_fail
// Ensure that SPIRE003 IS triggered when default is the right side of ?? where the left is EnforceInitializationStruct?.
public class Detect_DefaultLiteral_NullCoalescing
{
    public void Method(EnforceInitializationStruct? nullable)
    {
        EnforceInitializationStruct c = nullable ?? default; //~ ERROR
    }
}
