//@ should_pass
// Ensure that SPIRE016 is NOT triggered when casting a [MustBeInit] enum value to int (enum-to-int, not int-to-enum).
public class NoReport_EnumToInt_Cast
{
    void M()
    {
        int a = (int)StatusNoZero.Active;
        int b = (int)StatusNoZero.Inactive;
        int c = (int)StatusNoZero.Pending;

        long d = (long)FlagsNoZero.Read;
        byte e = (byte)ColorImplicitZero.Green;
        uint f = (uint)StatusWithZero.Active;

        // Named member in intermediate expression, then cast to int
        StatusNoZero named = StatusNoZero.Active;
        int g = (int)named;

        // Enum arithmetic on named members to produce int
        int combined = (int)FlagsNoZero.Read | (int)FlagsNoZero.Write;
    }
}
