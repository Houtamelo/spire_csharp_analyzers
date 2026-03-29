//@ exhaustive
// Internal base with all internal subtypes covered
#nullable enable

[EnforceExhaustiveness]
internal abstract class InternalShape { }
internal sealed class InternalCircle : InternalShape { }
internal sealed class InternalSquare : InternalShape { }

public class Internal_AllCovered
{
    internal int Test(InternalShape s) => s switch
    {
        InternalCircle => 1,
        InternalSquare => 2,
    };
}
