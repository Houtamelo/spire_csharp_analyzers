global using System;
global using System.Collections.Generic;
global using System.Threading.Tasks;
global using System.Reflection;
global using Spire;

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

/// [MustBeInit] enum with no zero-valued member — default(T) = 0 is unnamed
[MustBeInit]
public enum MustInitEnumNoZero { Active = 1, Inactive = 2, Pending = 3 }

/// [MustBeInit] enum with zero-valued member — default(T) = None, valid
[MustBeInit]
public enum MustInitEnumWithZero { None = 0, Active = 1, Inactive = 2 }

/// Plain enum (no [MustBeInit]) — never flagged
public enum PlainEnum { A, B, C }
