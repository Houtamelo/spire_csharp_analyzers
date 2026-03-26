//@ should_pass
// Ensure that SPIRE003 is NOT triggered when `s != default` compares a `EnforceInitializationStruct` to `default` (inequality check, not creation).
public class NoReport_Inequality_DefaultLiteral
{
    public bool Method()
    {
        var s = new EnforceInitializationRecordStruct(1);
        return s != default;
    }
}
