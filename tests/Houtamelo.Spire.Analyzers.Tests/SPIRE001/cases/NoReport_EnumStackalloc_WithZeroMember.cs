//@ should_pass
// Ensure that SPIRE001 is NOT triggered when stackalloc of EnforceInitializationEnumWithZero produces default(0) = None, which is a named member.
public class NoReport_EnumStackalloc_WithZeroMember
{
    void M()
    {
        Span<EnforceInitializationEnumWithZero> span = stackalloc EnforceInitializationEnumWithZero[5];
    }
}
