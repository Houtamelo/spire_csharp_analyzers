//@ not_exhaustive
// Internal base missing two subtypes
#nullable enable

[EnforceExhaustiveness]
internal abstract class InternalPriority { }
internal sealed class InternalLow : InternalPriority { }
internal sealed class InternalMedium : InternalPriority { }
internal sealed class InternalHigh : InternalPriority { }
internal sealed class InternalCritical : InternalPriority { }

public class Internal_MissingTwo
{
    internal int Test(InternalPriority p) => p switch
    {
        InternalLow => 1,
        InternalMedium => 2,
        //~ InternalHigh
        //~ InternalCritical
    };
}
