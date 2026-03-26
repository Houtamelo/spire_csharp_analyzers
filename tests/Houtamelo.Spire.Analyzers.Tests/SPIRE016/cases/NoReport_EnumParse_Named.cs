//@ should_pass
// Ensure that SPIRE016 is NOT triggered when using Enum.Parse to produce a [EnforceInitialization] enum value.
public class NoReport_EnumParse_Named
{
    void M()
    {
        StatusNoZero parsed = Enum.Parse<StatusNoZero>("Active");
        StatusNoZero parsed2 = (StatusNoZero)Enum.Parse(typeof(StatusNoZero), "Inactive");
        object boxed = Enum.Parse(typeof(StatusNoZero), "Pending");

        StatusNoZero ignoreCase = Enum.Parse<StatusNoZero>("active", ignoreCase: true);

        FlagsNoZero flagsParsed = Enum.Parse<FlagsNoZero>("Read");
        StatusWithZero withZeroParsed = Enum.Parse<StatusWithZero>("None");
    }
}
