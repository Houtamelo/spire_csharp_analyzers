//@ should_pass
// default literal inferred as Nullable<MustInitStruct> produces null — not flagged
public class NoReport_DefaultLiteral_NullableStructVar
{
    public void Method()
    {
        MustInitStruct? val = default;
    }
}
