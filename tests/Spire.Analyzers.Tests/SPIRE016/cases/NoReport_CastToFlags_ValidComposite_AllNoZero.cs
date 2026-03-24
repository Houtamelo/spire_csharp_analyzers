//@ should_pass
// Ensure that SPIRE016 is NOT triggered when casting 7 (Read|Write|Execute) to FlagsNoZero — all named flags combined
public class NoReport_CastToFlags_ValidComposite_AllNoZero
{
    void M()
    {
        FlagsNoZero f = (FlagsNoZero)7;
        _ = f;
    }
}
