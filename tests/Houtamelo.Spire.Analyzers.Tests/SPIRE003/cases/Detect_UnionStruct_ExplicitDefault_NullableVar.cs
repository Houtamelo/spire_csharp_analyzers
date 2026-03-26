//@ should_fail
// default(UnionLikeStruct) assigned to UnionLikeStruct? — still a zeroed struct, not null
public class Detect_UnionStruct_ExplicitDefault_NullableVar
{
    public void Method()
    {
        UnionLikeStruct? val = default(UnionLikeStruct); //~ ERROR
    }
}
