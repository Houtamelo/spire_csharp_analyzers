global using System;
global using System.Collections.Generic;
global using System.Threading.Tasks;
global using Spire;

[EnforceExhaustiveness]
public enum Color
{
    Red,
    Green,
    Blue,
}

[EnforceExhaustiveness]
public enum SingleMember
{
    Only,
}

public enum PlainEnum
{
    A,
    B,
    C,
}

[EnforceExhaustiveness]
public enum AliasedEnum
{
    First = 0,
    Second = 1,
    AlsoFirst = 0,
}

[Flags]
[EnforceExhaustiveness]
public enum Permission
{
    None = 0,
    Read = 1,
    Write = 2,
    ReadWrite = Read | Write,
    Execute = 4,
}

[EnforceExhaustiveness]
public enum Empty { }
