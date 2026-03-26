global using System;
global using System.Buffers;
global using System.Collections.Generic;
global using System.Collections.Immutable;
global using System.Threading.Tasks;
global using Houtamelo.Spire.Core;

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

public struct PlainStruct
{
    public int Value;
}

[EnforceInitialization]
public struct EmptyEnforceInitializationStruct { }

[EnforceInitialization]
public record struct EmptyEnforceInitializationRecordStruct;

[EnforceInitialization]
public struct EnforceInitializationStructWithNonAutoProperty
{
    public int Value { get => 42; }
}

[EnforceInitialization]
public struct EnforceInitializationStructWithAutoProperty
{
    public int Value { get; set; }
}

#nullable enable

[EnforceInitialization]
public class EnforceInitializationClass
{
    public int Value;
    public EnforceInitializationClass(int value) { Value = value; }
}

[EnforceInitialization]
public record EnforceInitializationRecord(int Value);

public class PlainClass
{
    public int Value;
}

/// [EnforceInitialization] enum with no zero-valued member — default(T) = 0 is unnamed
[EnforceInitialization]
public enum EnforceInitializationEnumNoZero { Active = 1, Inactive = 2, Pending = 3 }

/// [EnforceInitialization] enum with zero-valued member — default(T) = None, valid
[EnforceInitialization]
public enum EnforceInitializationEnumWithZero { None = 0, Active = 1, Inactive = 2 }

/// Plain enum (no [EnforceInitialization]) — never flagged
public enum PlainEnum { A, B, C }
