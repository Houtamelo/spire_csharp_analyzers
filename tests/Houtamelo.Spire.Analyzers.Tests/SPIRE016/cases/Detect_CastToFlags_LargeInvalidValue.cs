//@ should_fail
// Ensure that SPIRE016 IS triggered when casting 255 to FlagsWithZero — many bits beyond named members (Read=1, Write=2, Execute=4 cover only bits 0-2)
public class Detect_CastToFlags_LargeInvalidValue
{
    void M()
    {
        FlagsWithZero f = (FlagsWithZero)255; //~ ERROR
        _ = f;
    }
}
