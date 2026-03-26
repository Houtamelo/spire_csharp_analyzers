//@ should_fail
// Ensure that SPIRE003 IS triggered when return default; is used in a method returning EnforceInitializationStruct.
public class Detect_DefaultLiteral_ReturnStatement
{
    public EnforceInitializationStruct Method()
    {
        return default; //~ ERROR
    }
}
