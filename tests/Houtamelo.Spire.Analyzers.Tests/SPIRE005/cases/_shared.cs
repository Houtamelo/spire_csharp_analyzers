global using System;
global using System.Collections.Generic;
global using System.Threading.Tasks;
global using System.Reflection;
global using Houtamelo.Spire;

[EnforceInitialization]
public struct EnforceInitializationStruct
{
    public int Value;

    public EnforceInitializationStruct(int value)
    {
        Value = value;
    }
}

[EnforceInitialization]
public record struct EnforceInitializationRecordStruct(int Value);

[EnforceInitialization]
public readonly struct EnforceInitializationReadonlyStruct
{
    public readonly int Value;

    public EnforceInitializationReadonlyStruct(int value)
    {
        Value = value;
    }
}

public struct PlainStruct
{
    public int Value;
}

[EnforceInitialization]
public struct EmptyEnforceInitializationStruct { }

/// [EnforceInitialization] enum with no zero-valued member — default(T) = 0 is unnamed
[EnforceInitialization]
public enum EnforceInitializationEnumNoZero { Active = 1, Inactive = 2, Pending = 3 }

/// [EnforceInitialization] enum with zero-valued member — default(T) = None, valid
[EnforceInitialization]
public enum EnforceInitializationEnumWithZero { None = 0, Active = 1, Inactive = 2 }

/// Plain enum (no [EnforceInitialization]) — never flagged
public enum PlainEnum { A, B, C }
