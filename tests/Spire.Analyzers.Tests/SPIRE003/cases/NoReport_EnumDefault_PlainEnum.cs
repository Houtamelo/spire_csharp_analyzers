//@ should_pass
// Ensure that SPIRE003 is NOT triggered when using default(T) on a plain enum not marked with [MustBeInit].
public class NoReport_EnumDefault_PlainEnum
{
    void M()
    {
        var x = default(PlainEnum);
    }
}
