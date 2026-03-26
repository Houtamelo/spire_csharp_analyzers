global using System;
global using System.Collections.Generic;
global using System.Threading.Tasks;
global using Houtamelo.Spire.Core;

// [EnforceInitialization] struct without parameterless ctor — new T() == default(T)
[EnforceInitialization]
public struct EnforceInitializationNoCtor
{
    public int Value;
    public string Name;

    public EnforceInitializationNoCtor(int value, string name)
    {
        Value = value;
        Name = name;
    }
}

// [EnforceInitialization] struct WITH user-defined parameterless ctor — new T() should NOT be flagged
[EnforceInitialization]
public struct EnforceInitializationWithCtor
{
    public int Value;

    public EnforceInitializationWithCtor()
    {
        Value = 42;
    }
}

// [EnforceInitialization] struct where ALL fields have initializers — new T() should NOT be flagged
[EnforceInitialization]
public struct EnforceInitializationAllFieldsInitialized
{
    public int Value = 10;
    public string Name = "default";

    public EnforceInitializationAllFieldsInitialized(int value, string name)
    {
        Value = value;
        Name = name;
    }
}

// [EnforceInitialization] struct where SOME (not all) fields have initializers — new T() SHOULD be flagged
[EnforceInitialization]
public struct EnforceInitializationPartialFieldsInitialized
{
    public int Value = 10;
    public string Name;

    public EnforceInitializationPartialFieldsInitialized(int value, string name)
    {
        Value = value;
        Name = name;
    }
}

// [EnforceInitialization] record struct — no user-defined parameterless ctor
[EnforceInitialization]
public record struct EnforceInitializationRecordNoCtor(int Value);

// [EnforceInitialization] struct with auto-properties, no parameterless ctor
[EnforceInitialization]
public struct EnforceInitializationAutoPropsNoCtor
{
    public int Value { get; set; }
    public string Name { get; set; }
}

// [EnforceInitialization] struct with all auto-properties initialized — should NOT be flagged
[EnforceInitialization]
public struct EnforceInitializationAutoPropsAllInitialized
{
    public int Value { get; set; } = 10;
    public string Name { get; set; } = "default";

    public EnforceInitializationAutoPropsAllInitialized(int value, string name)
    {
        Value = value;
        Name = name;
    }
}

// [EnforceInitialization] struct with some auto-properties initialized — SHOULD be flagged
[EnforceInitialization]
public struct EnforceInitializationAutoPropsPartialInitialized
{
    public int Value { get; set; } = 10;
    public string Name { get; set; }

    public EnforceInitializationAutoPropsPartialInitialized(int value, string name)
    {
        Value = value;
        Name = name;
    }
}

// Plain struct (no [EnforceInitialization]) — should never be flagged
public struct PlainStruct
{
    public int Value;
}

// [EnforceInitialization] empty struct — should NOT be flagged (no fields to initialize)
[EnforceInitialization]
public struct EmptyEnforceInitializationStruct { }

// [EnforceInitialization] struct with only non-auto (computed) properties — no instance fields
[EnforceInitialization]
public struct EnforceInitializationComputedOnly
{
    public int Value { get => 42; }
}

// [EnforceInitialization] struct with mix of fields and auto-properties, all initialized
[EnforceInitialization]
public struct EnforceInitializationMixedAllInitialized
{
    public int Field = 5;
    public string Prop { get; set; } = "hello";

    public EnforceInitializationMixedAllInitialized(int field, string prop)
    {
        Field = field;
        Prop = prop;
    }
}

// [EnforceInitialization] struct with mix of fields and auto-properties, not all initialized
[EnforceInitialization]
public struct EnforceInitializationMixedPartialInitialized
{
    public int Field = 5;
    public string Prop { get; set; }

    public EnforceInitializationMixedPartialInitialized(int field, string prop)
    {
        Field = field;
        Prop = prop;
    }
}

// [EnforceInitialization] record struct with regular fields (no primary ctor) — new T() SHOULD be flagged
[EnforceInitialization]
public record struct EnforceInitializationRecordStructWithFields
{
    public int Value;
    public string Name;
}

// [EnforceInitialization] readonly struct without parameterless ctor — new T() SHOULD be flagged
[EnforceInitialization]
public readonly struct EnforceInitializationReadonlyNoCtor
{
    public readonly int Value;
    public readonly string Name;

    public EnforceInitializationReadonlyNoCtor(int value, string name)
    {
        Value = value;
        Name = name;
    }
}

// [EnforceInitialization] ref struct without parameterless ctor — new T() SHOULD be flagged
[EnforceInitialization]
public ref struct EnforceInitializationRefNoCtor
{
    public int Value;
    public string Name;
}

// [EnforceInitialization] readonly ref struct without parameterless ctor — new T() SHOULD be flagged
[EnforceInitialization]
public readonly ref struct EnforceInitializationReadonlyRefNoCtor
{
    public readonly int Value;
    public readonly string Name;
}

// Simple class for false-positive testing — new SomeClass() should never be flagged
public class SomeClass { }

/// [EnforceInitialization] enum with no zero-valued member — default(T) = 0 is unnamed
[EnforceInitialization]
public enum EnforceInitializationEnumNoZero { Active = 1, Inactive = 2, Pending = 3 }

/// [EnforceInitialization] enum with zero-valued member — default(T) = None, valid
[EnforceInitialization]
public enum EnforceInitializationEnumWithZero { None = 0, Active = 1, Inactive = 2 }

/// Plain enum (no [EnforceInitialization]) — never flagged
public enum PlainEnum { A, B, C }
