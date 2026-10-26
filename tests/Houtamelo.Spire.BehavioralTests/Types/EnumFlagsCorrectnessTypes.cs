using System;

namespace Spire.BehavioralTests.Types;

[Flags]
public enum NativeSignedByteFlags : sbyte
{
    None = 0,
    Read = 1,
    Write = 2,
    HighBit = sbyte.MinValue,
    All = -1,
}

[Flags]
public enum NativeUnsignedByteFlags : byte
{
    None = 0,
    Read = 1,
    Write = 2,
    All = byte.MaxValue,
}

[Flags]
public enum NativeSignedShortFlags : short
{
    None = 0,
    Read = 1,
    Write = 2,
    HighBit = short.MinValue,
    All = -1,
}

[Flags]
public enum NativeUnsignedShortFlags : ushort
{
    None = 0,
    Read = 1,
    Write = 2,
    All = ushort.MaxValue,
}

[Flags]
public enum NativeSignedIntFlags : int
{
    None = 0,
    Read = 1,
    Write = 2,
    HighBit = int.MinValue,
    All = -1,
}

[Flags]
public enum NativeUnsignedIntFlags : uint
{
    None = 0,
    Read = 1,
    Write = 2,
    All = uint.MaxValue,
}

[Flags]
public enum NativeSignedLongFlags : long
{
    None = 0,
    Read = 1,
    Write = 2,
    HighBit = long.MinValue,
    All = -1,
}

[Flags]
public enum NativeUnsignedLongFlags : ulong
{
    None = 0,
    Read = 1,
    Write = 2,
    HighBit = 0x8000000000000000UL,
    All = ulong.MaxValue,
}
