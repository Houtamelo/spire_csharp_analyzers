//@ should_pass
// Ensure that SPIRE016 is NOT triggered when casting 3 (Read|Write) to FlagsWithZero — valid composite value
public class NoReport_CastToFlags_ValidComposite_ReadWrite
{
    void M()
    {
        FlagsWithZero f = (FlagsWithZero)3;
        _ = f;
    }
}
