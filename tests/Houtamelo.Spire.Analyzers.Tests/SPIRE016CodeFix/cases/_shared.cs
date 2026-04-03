global using System;
global using Houtamelo.Spire;

[EnforceInitialization]
public enum StatusNoZero { Active = 1, Inactive = 2, Pending = 3 }

[EnforceInitialization]
[Flags]
public enum FlagsNoZero { Read = 1, Write = 2, Execute = 4 }

public enum PlainEnum { A = 0, B = 1, C = 2 }
