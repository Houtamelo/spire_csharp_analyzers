//@ should_pass
// default(UnionLikeStruct?) produces null — Nullable<T> does not have [MustBeInit]
public class NoReport_UnionStruct_DefaultNullable
{
    public void Method()
    {
        UnionLikeStruct? val = default(UnionLikeStruct?);
    }
}
