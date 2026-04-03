global using System;
global using Houtamelo.Spire;

public enum Status { Active = 1, Inactive = 2, Pending = 3 }

[Flags]
public enum Perms { None = 0, Read = 1, Write = 2, Execute = 4 }
