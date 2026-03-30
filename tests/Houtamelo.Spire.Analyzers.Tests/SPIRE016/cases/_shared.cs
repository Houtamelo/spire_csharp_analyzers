global using System;
global using System.Collections.Generic;
global using System.Threading.Tasks;
global using System.Runtime.CompilerServices;
global using Houtamelo.Spire;

/// Enum with no zero-valued member — default(StatusNoZero) = 0 is unnamed
[EnforceInitialization]
public enum StatusNoZero { Active = 1, Inactive = 2, Pending = 3 }

/// Enum with a zero-valued member — default(StatusWithZero) = None, which is valid
[EnforceInitialization]
public enum StatusWithZero { None = 0, Active = 1, Inactive = 2 }

/// Enum with implicit zero from first member — default(Color) = Red = 0, valid
[EnforceInitialization]
public enum ColorImplicitZero { Red, Green, Blue }

/// [Flags] enum with zero as "None"
[EnforceInitialization]
[Flags]
public enum FlagsWithZero { None = 0, Read = 1, Write = 2, Execute = 4 }

/// [Flags] enum without zero member
[EnforceInitialization]
[Flags]
public enum FlagsNoZero { Read = 1, Write = 2, Execute = 4 }

/// Unmarked enum — should never be flagged
public enum PlainEnum { A, B, C }
