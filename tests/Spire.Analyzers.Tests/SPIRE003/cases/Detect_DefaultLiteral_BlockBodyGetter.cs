//@ should_fail
// Ensure that SPIRE003 IS triggered when return default; is inside a block-body property getter of type MustInitStruct.
public class Detect_DefaultLiteral_BlockBodyGetter
{
    public MustInitStruct Property
    {
        get
        {
            return default; //~ ERROR
        }
    }
}
