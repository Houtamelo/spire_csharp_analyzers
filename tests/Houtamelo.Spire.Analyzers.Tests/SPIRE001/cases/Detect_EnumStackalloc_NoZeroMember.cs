//@ should_fail
// Ensure that SPIRE001 IS triggered when stackalloc of EnforceInitializationEnumNoZero produces unnamed default(0) elements.
public class Detect_EnumStackalloc_NoZeroMember
{
    void M()
    {
        Span<EnforceInitializationEnumNoZero> span = stackalloc EnforceInitializationEnumNoZero[5]; //~ ERROR
    }
}
