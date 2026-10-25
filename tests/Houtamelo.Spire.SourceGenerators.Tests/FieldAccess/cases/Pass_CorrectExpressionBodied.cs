//@ should_pass
// Expression-bodied switch with correct field access in each arm
using Houtamelo.Spire;
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
            { kind: Node.Kind.Leaf, leaf: { leafValue: var value } } => value.ToString(),
            { kind: Node.Kind.Branch, branch: { branchLabel: var label } } => label,
            _ => "?",
        };
    }
}
