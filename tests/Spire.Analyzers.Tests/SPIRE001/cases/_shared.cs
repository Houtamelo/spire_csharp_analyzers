using System;
using System.Buffers;
using System.Collections.Immutable;
using Spire.Analyzers;

[MustBeInit]
public struct MustInitStruct
{
    public int Value;

    public MustInitStruct(int value)
    {
        Value = value;
    }
}

[MustBeInit]
public record struct MustInitRecordStruct(int Value);

public struct PlainStruct
{
    public int Value;
}
