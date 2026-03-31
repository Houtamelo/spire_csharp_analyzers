namespace Houtamelo.Spire.GlobalConfig.EndToEnd.Types;

// No [EnforceExhaustiveness] — global config makes SPIRE015 fire on these

public enum Direction { Up, Down, Left, Right }

public enum Priority { Low = 1, Medium = 2, High = 3 } // no zero member

public enum Color { Red = 0, Green = 1, Blue = 2 } // has zero member (Red)

[System.Flags]
public enum Permissions { Read = 1, Write = 2, Execute = 4 }
