global using System;
global using System.Buffers;
global using System.Collections.Generic;
global using System.Collections.Immutable;
global using System.Threading.Tasks;
global using Spire.Analyzers;

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

[MustBeInit]
public struct EmptyMustInitStruct { }

[MustBeInit]
public record struct EmptyMustInitRecordStruct;

[MustBeInit]
public struct MustInitStructWithNonAutoProperty
{
    public int Value { get => 42; }
}

[MustBeInit]
public struct MustInitStructWithAutoProperty
{
    public int Value { get; set; }
}

#nullable enable

[MustBeInit]
public class MustInitClass
{
    public int Value;
    public MustInitClass(int value) { Value = value; }
}

[MustBeInit]
public record MustInitRecord(int Value);

public class PlainClass
{
    public int Value;
}
