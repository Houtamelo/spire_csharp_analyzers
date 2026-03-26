//@ should_fail
// Ensure that SPIRE016 IS triggered when casting 9 (1|8) to FlagsWithZero — bit 3 (value 8) is invalid even though bit 0 (Read=1) is valid
public class Detect_CastToFlags_MixedValidInvalid
{
    void M()
    {
        FlagsWithZero f = (FlagsWithZero)9; //~ ERROR
        _ = f;
    }
}
