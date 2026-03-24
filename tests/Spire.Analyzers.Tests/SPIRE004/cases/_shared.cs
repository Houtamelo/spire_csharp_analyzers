global using System;
global using System.Collections.Generic;
global using System.Threading.Tasks;
global using Spire;

// [MustBeInit] struct without parameterless ctor — new T() == default(T)
[MustBeInit]
public struct MustInitNoCtor
{
    public int Value;
    public string Name;

    public MustInitNoCtor(int value, string name)
    {
        Value = value;
        Name = name;
    }
}

// [MustBeInit] struct WITH user-defined parameterless ctor — new T() should NOT be flagged
[MustBeInit]
public struct MustInitWithCtor
{
    public int Value;

    public MustInitWithCtor()
    {
        Value = 42;
    }
}

// [MustBeInit] struct where ALL fields have initializers — new T() should NOT be flagged
[MustBeInit]
public struct MustInitAllFieldsInitialized
{
    public int Value = 10;
    public string Name = "default";

    public MustInitAllFieldsInitialized(int value, string name)
    {
        Value = value;
        Name = name;
    }
}

// [MustBeInit] struct where SOME (not all) fields have initializers — new T() SHOULD be flagged
[MustBeInit]
public struct MustInitPartialFieldsInitialized
{
    public int Value = 10;
    public string Name;

    public MustInitPartialFieldsInitialized(int value, string name)
    {
        Value = value;
        Name = name;
    }
}

// [MustBeInit] record struct — no user-defined parameterless ctor
[MustBeInit]
public record struct MustInitRecordNoCtor(int Value);

// [MustBeInit] struct with auto-properties, no parameterless ctor
[MustBeInit]
public struct MustInitAutoPropsNoCtor
{
    public int Value { get; set; }
    public string Name { get; set; }
}

// [MustBeInit] struct with all auto-properties initialized — should NOT be flagged
[MustBeInit]
public struct MustInitAutoPropsAllInitialized
{
    public int Value { get; set; } = 10;
    public string Name { get; set; } = "default";

    public MustInitAutoPropsAllInitialized(int value, string name)
    {
        Value = value;
        Name = name;
    }
}

// [MustBeInit] struct with some auto-properties initialized — SHOULD be flagged
[MustBeInit]
public struct MustInitAutoPropsPartialInitialized
{
    public int Value { get; set; } = 10;
    public string Name { get; set; }

    public MustInitAutoPropsPartialInitialized(int value, string name)
    {
        Value = value;
        Name = name;
    }
}

// Plain struct (no [MustBeInit]) — should never be flagged
public struct PlainStruct
{
    public int Value;
}

// [MustBeInit] empty struct — should NOT be flagged (no fields to initialize)
[MustBeInit]
public struct EmptyMustInitStruct { }

// [MustBeInit] struct with only non-auto (computed) properties — no instance fields
[MustBeInit]
public struct MustInitComputedOnly
{
    public int Value { get => 42; }
}

// [MustBeInit] struct with mix of fields and auto-properties, all initialized
[MustBeInit]
public struct MustInitMixedAllInitialized
{
    public int Field = 5;
    public string Prop { get; set; } = "hello";

    public MustInitMixedAllInitialized(int field, string prop)
    {
        Field = field;
        Prop = prop;
    }
}

// [MustBeInit] struct with mix of fields and auto-properties, not all initialized
[MustBeInit]
public struct MustInitMixedPartialInitialized
{
    public int Field = 5;
    public string Prop { get; set; }

    public MustInitMixedPartialInitialized(int field, string prop)
    {
        Field = field;
        Prop = prop;
    }
}

// [MustBeInit] record struct with regular fields (no primary ctor) — new T() SHOULD be flagged
[MustBeInit]
public record struct MustInitRecordStructWithFields
{
    public int Value;
    public string Name;
}

// [MustBeInit] readonly struct without parameterless ctor — new T() SHOULD be flagged
[MustBeInit]
public readonly struct MustInitReadonlyNoCtor
{
    public readonly int Value;
    public readonly string Name;

    public MustInitReadonlyNoCtor(int value, string name)
    {
        Value = value;
        Name = name;
    }
}

// [MustBeInit] ref struct without parameterless ctor — new T() SHOULD be flagged
[MustBeInit]
public ref struct MustInitRefNoCtor
{
    public int Value;
    public string Name;
}

// [MustBeInit] readonly ref struct without parameterless ctor — new T() SHOULD be flagged
[MustBeInit]
public readonly ref struct MustInitReadonlyRefNoCtor
{
    public readonly int Value;
    public readonly string Name;
}

// Simple class for false-positive testing — new SomeClass() should never be flagged
public class SomeClass { }

/// [MustBeInit] enum with no zero-valued member — default(T) = 0 is unnamed
[MustBeInit]
public enum MustInitEnumNoZero { Active = 1, Inactive = 2, Pending = 3 }

/// [MustBeInit] enum with zero-valued member — default(T) = None, valid
[MustBeInit]
public enum MustInitEnumWithZero { None = 0, Active = 1, Inactive = 2 }

/// Plain enum (no [MustBeInit]) — never flagged
public enum PlainEnum { A, B, C }
