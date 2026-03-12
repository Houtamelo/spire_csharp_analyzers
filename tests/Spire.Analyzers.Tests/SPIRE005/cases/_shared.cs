global using System;
global using System.Collections.Generic;
global using System.Threading.Tasks;
global using System.Reflection;
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

[MustBeInit]
public readonly struct MustInitReadonlyStruct
{
    public readonly int Value;

    public MustInitReadonlyStruct(int value)
    {
        Value = value;
    }
}

public struct PlainStruct
{
    public int Value;
}

[MustBeInit]
public struct EmptyMustInitStruct { }
