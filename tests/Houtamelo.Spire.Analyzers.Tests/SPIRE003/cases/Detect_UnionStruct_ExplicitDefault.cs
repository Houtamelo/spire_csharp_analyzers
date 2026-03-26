//@ should_fail
// default(UnionLikeStruct) produces zeroed union-like struct — flagged
public class Detect_UnionStruct_ExplicitDefault
{
    public void Method()
    {
        UnionLikeStruct s = default(UnionLikeStruct); //~ ERROR
    }
}
