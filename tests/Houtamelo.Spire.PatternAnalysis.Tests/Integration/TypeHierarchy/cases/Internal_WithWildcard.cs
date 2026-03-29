//@ exhaustive
// Internal base — wildcard covers all
#nullable enable

[EnforceExhaustiveness]
internal abstract class InternalMsg { }
internal sealed class InternalPing : InternalMsg { }
internal sealed class InternalPong : InternalMsg { }

public class Internal_WithWildcard
{
    internal int Test(InternalMsg m) => m switch
    {
        InternalPing => 1,
        _ => 2,
    };
}
