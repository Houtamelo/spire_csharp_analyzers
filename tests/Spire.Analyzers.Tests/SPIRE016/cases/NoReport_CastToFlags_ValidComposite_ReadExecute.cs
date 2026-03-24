//@ should_pass
// Ensure that SPIRE016 is NOT triggered when casting 5 (Read|Execute) to FlagsWithZero — valid composite value
public class NoReport_CastToFlags_ValidComposite_ReadExecute
{
    void M()
    {
        FlagsWithZero f = (FlagsWithZero)5;
        _ = f;
    }
}
