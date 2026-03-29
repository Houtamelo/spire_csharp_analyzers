//@ not_exhaustive
// Internal base missing one subtype
#nullable enable

[EnforceExhaustiveness]
internal abstract class InternalCmd { }
internal sealed class InternalStart : InternalCmd { }
internal sealed class InternalStop : InternalCmd { }
internal sealed class InternalPause : InternalCmd { }

public class Internal_MissingOne
{
    internal int Test(InternalCmd c) => c switch
    {
        InternalStart => 1,
        InternalStop => 2,
        //~ InternalPause
    };
}
