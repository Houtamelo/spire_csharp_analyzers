//@ should_fail
// Ensure that SPIRE003 IS triggered when return default; is inside a block-body property getter of type EnforceInitializationStruct.
public class Detect_DefaultLiteral_BlockBodyGetter
{
    public EnforceInitializationStruct Property
    {
        get
        {
            return default; //~ ERROR
        }
    }
}
