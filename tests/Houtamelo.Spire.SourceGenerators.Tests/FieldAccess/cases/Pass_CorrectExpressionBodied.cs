//@ should_pass
// Expression-bodied switch with correct field access in each arm
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Node
    {
        [Variant] public static partial Node Leaf(int leafValue);
        [Variant] public static partial Node Branch(string branchLabel);
    }
    class C
    {
        string Describe(Node n) => n switch
        {
            (Node.Kind.Leaf, _) => n.leafValue.ToString(),
            (Node.Kind.Branch, _) => n.branchLabel,
            _ => "?",
        };
    }
}
