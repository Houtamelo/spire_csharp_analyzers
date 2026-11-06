using System;
using Houtamelo.Spire;

[Flags]
[EnforceExhaustiveness]
enum Permission
{
    None = 0,
    Read = 1,
    Write = 2,
    ReadWrite = Read | Write,
    Execute = 4,
}

class Consumer
{
    int GetValue(Permission permission) => permission switch
    {
        Permission.None => 0,
        Permission.Read => 1,
        Permission.Write => 2,
        Permission.ReadWrite => 3,
    };
}
