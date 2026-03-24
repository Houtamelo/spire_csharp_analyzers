//@ should_pass
// Ensure that SPIRE016 is NOT triggered when casting 3 (Read|Write) to FlagsNoZero — valid composite even without a zero member
public class NoReport_CastToFlags_ValidComposite_NoZeroEnum
{
    void M()
    {
        FlagsNoZero f = (FlagsNoZero)3;
        _ = f;
    }
}
