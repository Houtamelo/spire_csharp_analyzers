using Houtamelo.Spire;

[DiscriminatedUnion(Json = JsonLibrary.SystemTextJson, JsonDiscriminator = "type")]
partial struct Token
{
    [Variant] public static partial Token Number(double value);
    [Variant] public static partial Token Eof();
}
