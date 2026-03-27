global using System;

public enum OneCase { Only }

public enum TwoCases { A, B }

public struct SingleField
{
    public TwoCases Value { get; set; }
}
