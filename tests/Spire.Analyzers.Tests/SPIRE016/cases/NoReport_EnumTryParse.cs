//@ should_pass
// Ensure that SPIRE016 is NOT triggered when using Enum.TryParse for [EnforceInitialization] enum values.
public class NoReport_EnumTryParse
{
    void M()
    {
        bool success1 = Enum.TryParse<StatusNoZero>("Active", out var result1);
        bool success2 = Enum.TryParse<StatusNoZero>("active", ignoreCase: true, out var result2);
        bool success3 = Enum.TryParse<FlagsNoZero>("Read", out FlagsNoZero flagsResult);
        bool success4 = Enum.TryParse<StatusWithZero>("None", out StatusWithZero withZeroResult);

        if (Enum.TryParse<StatusNoZero>("Pending", out var pending))
        {
            _ = pending;
        }
    }
}
