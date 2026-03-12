//@ should_fail
// Ensure that SPIRE003 IS triggered when return default; is used in a method returning MustInitStruct.
public class Detect_DefaultLiteral_ReturnStatement
{
    public MustInitStruct Method()
    {
        return default; //~ ERROR
    }
}
