using Houtamelo.Spire;

[DiscriminatedUnion(json: JsonLibrary.SystemTextJson, jsonDiscriminator: "type")]
partial struct Token
{
    [Variant] public static partial Token Number(double value);
    [Variant] public static partial Token Eof();
}
