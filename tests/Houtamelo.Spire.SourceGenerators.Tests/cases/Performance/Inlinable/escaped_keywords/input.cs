using System;
using Houtamelo.Spire;

namespace @event;

public static partial class @class
{
    public static @struct @event<@struct>(
        @struct @value,
        [Inlinable] Func<@struct, @struct> @delegate)
        where @struct : unmanaged
    {
        return @delegate(@value);
    }
}
