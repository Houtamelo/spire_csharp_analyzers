//@ should_pass
// default literal inferred as Nullable<EnforceInitializationStruct> produces null — not flagged
public class NoReport_DefaultLiteral_NullableStructVar
{
    public void Method()
    {
        EnforceInitializationStruct? val = default;
    }
}
