//@ should_pass
// Ensure that SPIRE016 is NOT triggered when casting 7 (Read|Write|Execute) to FlagsWithZero — all flags combined
public class NoReport_CastToFlags_ValidComposite_All
{
    void M()
    {
        FlagsWithZero f = (FlagsWithZero)7;
        _ = f;
    }
}
