//@ should_fail
// default literal assigned to UnionLikeStruct — flagged
public class Detect_UnionStruct_DefaultLiteral
{
    public void Method()
    {
        UnionLikeStruct s = default; //~ ERROR
    }
}
