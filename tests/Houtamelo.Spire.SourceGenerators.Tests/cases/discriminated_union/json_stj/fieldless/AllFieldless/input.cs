using Houtamelo.Spire;

[DiscriminatedUnion(json: JsonLibrary.SystemTextJson)]
partial struct Token
{
    [Variant] public static partial Token Ident();
    [Variant] public static partial Token Number();
    [Variant] public static partial Token Eof();
}
