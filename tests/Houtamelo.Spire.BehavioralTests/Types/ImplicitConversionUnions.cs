using Houtamelo.Spire;

namespace Spire.BehavioralTests.Types;

[DiscriminatedUnion]
internal partial struct ImplicitConversionShape
{
    [Variant] public static partial ImplicitConversionShape Circle(float radius);
    [Variant] public static partial ImplicitConversionShape Rectangle(float width, float height);
    [Variant] public static partial ImplicitConversionShape Point();
}

[DiscriminatedUnion]
internal partial struct ImplicitConversionOption<T> where T : unmanaged
{
    [Variant] public static partial ImplicitConversionOption<T> Some(T value, float scale);
    [Variant] public static partial ImplicitConversionOption<T> None();
}

internal struct ImplicitManagedPair
{
    public ImplicitManagedPair(string name, int count)
    {
        Name = name;
        Count = count;
    }

    public string Name { get; }

    public int Count { get; }
}

internal sealed class ImplicitReferencePayload
{
    public ImplicitReferencePayload(int id)
    {
        Id = id;
    }

    public int Id { get; }
}

[DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Shared)]
internal partial struct ImplicitConversionManaged
{
    [Variant] public static partial ImplicitConversionManaged Text(string? value);
    [Variant] public static partial ImplicitConversionManaged Pair((string? label, int count) value);
    [Variant] public static partial ImplicitConversionManaged Managed(ImplicitManagedPair value);
    [Variant] public static partial ImplicitConversionManaged Reference(ImplicitReferencePayload value);
    [Variant] public static partial ImplicitConversionManaged Empty();
}

internal static partial class ImplicitConversionNested
{
    [DiscriminatedUnion]
    internal partial struct Command
    {
        [Variant] public static partial Command Run(float speed);
        [Variant] public static partial Command Stop();
    }
}

[DiscriminatedUnion]
internal readonly partial struct ImplicitConversionReadonly
{
    [Variant] public static partial ImplicitConversionReadonly Value(int value);
    [Variant] public static partial ImplicitConversionReadonly Empty();
}
