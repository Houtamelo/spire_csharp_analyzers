using Houtamelo.Spire;

[DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Typed)]
public partial struct Shape
{
    [Variant] public static partial Shape Circle(double radius);
    [Variant] public static partial Shape Square(int side);
    [Variant] public static partial Shape Point();
}
