//@ should_fail
// Ensure that SPIRE016 IS triggered when casting 8 to FlagsNoZero — bit 3 is not covered by any named member
public class Detect_CastToFlags_InvalidBit_NoZero
{
    void M()
    {
        FlagsNoZero f = (FlagsNoZero)8; //~ ERROR
        _ = f;
    }
}
