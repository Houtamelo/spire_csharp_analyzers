global using System;
global using Houtamelo.Spire;

public enum Axis { X, Y }

public enum Toggle { On, Off }

[EnforceExhaustiveness]
public abstract class Vehicle { }
public sealed class Car : Vehicle { }
public sealed class Bike : Vehicle { }
