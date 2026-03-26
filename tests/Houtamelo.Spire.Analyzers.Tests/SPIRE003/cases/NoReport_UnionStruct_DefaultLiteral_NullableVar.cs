//@ should_pass
// default literal inferred as Nullable<UnionLikeStruct> produces null — not flagged
public class NoReport_UnionStruct_DefaultLiteral_NullableVar
{
    public void Method()
    {
        UnionLikeStruct? val = default;
    }
}
