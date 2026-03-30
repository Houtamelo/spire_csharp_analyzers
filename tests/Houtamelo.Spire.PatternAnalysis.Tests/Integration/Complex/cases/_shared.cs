global using System;
global using Houtamelo.Spire;

public enum Color { Red, Green, Blue, Yellow, Purple }

public enum Suit { Hearts, Diamonds, Clubs, Spades }

public struct TwoFlags
{
    public bool A { get; set; }
    public bool B { get; set; }
}

public struct EnumPair
{
    public Color Color { get; set; }
    public Suit Suit { get; set; }
}
