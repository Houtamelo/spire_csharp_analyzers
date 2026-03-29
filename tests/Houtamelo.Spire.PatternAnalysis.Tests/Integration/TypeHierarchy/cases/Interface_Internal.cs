//@ exhaustive
// Internal interface with internal implementors
#nullable enable

[EnforceExhaustiveness]
internal interface IInternalOp { }
internal sealed class InternalRead : IInternalOp { }
internal sealed class InternalWrite : IInternalOp { }

public class Interface_Internal
{
    internal int Test(IInternalOp op) => op switch
    {
        InternalRead => 1,
        InternalWrite => 2,
    };
}
