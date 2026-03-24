//@ should_fail
// Ensure that SPIRE016 IS triggered when casting 8 to FlagsWithZero — bit 3 is not covered by any named member
public class Detect_CastToFlags_InvalidBit
{
    void M()
    {
        FlagsWithZero f = (FlagsWithZero)8; //~ ERROR
        _ = f;
    }
}
