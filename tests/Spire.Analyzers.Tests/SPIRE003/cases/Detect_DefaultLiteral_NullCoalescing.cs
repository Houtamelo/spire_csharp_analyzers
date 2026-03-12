//@ should_fail
// Ensure that SPIRE003 IS triggered when default is the right side of ?? where the left is MustInitStruct?.
public class Detect_DefaultLiteral_NullCoalescing
{
    public void Method(MustInitStruct? nullable)
    {
        MustInitStruct c = nullable ?? default; //~ ERROR
    }
}
