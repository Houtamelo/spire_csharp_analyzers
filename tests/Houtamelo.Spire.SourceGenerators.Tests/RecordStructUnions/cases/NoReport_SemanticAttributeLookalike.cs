//@ should_pass
// Lookalike attributes from another namespace are not Spire declarations.
using System;

namespace Lookalike
{
    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class DiscriminatedUnionAttribute : Attribute { }
}

[Lookalike.DiscriminatedUnion]
public partial record struct NotASpireUnion
{
}
