//@ should_pass
// Ensure that SPIRE003 is NOT triggered when `s == default` compares a `EnforceInitializationStruct` to `default` (equality check, not creation).
public class NoReport_Equality_DefaultLiteral
{
    public bool Method()
    {
        var s = new EnforceInitializationRecordStruct(1);
        return s == default;
    }
}
