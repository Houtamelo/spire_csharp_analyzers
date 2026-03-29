//@ exhaustive
// Internal base with three subtypes all covered
#nullable enable

[EnforceExhaustiveness]
internal abstract class InternalColor { }
internal sealed class InternalRed : InternalColor { }
internal sealed class InternalGreen : InternalColor { }
internal sealed class InternalBlue : InternalColor { }

public class Internal_ThreeSubtypes
{
    internal int Test(InternalColor c) => c switch
    {
        InternalRed => 1,
        InternalGreen => 2,
        InternalBlue => 3,
    };
}
