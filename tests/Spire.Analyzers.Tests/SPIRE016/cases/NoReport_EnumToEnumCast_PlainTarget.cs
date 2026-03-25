//@ should_pass
// Ensure that SPIRE016 is NOT triggered when the target enum is not marked [MustBeInit].
public class NoReport_EnumToEnumCast_PlainTarget
{
    public PlainEnum Method()
    {
        // Target is PlainEnum (no [MustBeInit]), so no diagnostic
        return (PlainEnum)(StatusNoZero.Active);
    }
}
