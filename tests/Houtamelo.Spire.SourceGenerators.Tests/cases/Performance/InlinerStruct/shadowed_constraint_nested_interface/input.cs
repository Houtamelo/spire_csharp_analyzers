using Houtamelo.Spire;

namespace Shadowed;

public interface IMarker { }

public readonly struct Marker : global::Shadowed.IMarker
{
    public Marker(int value) => Value = value;

    public int Value { get; }
}

public partial class Container
{
    public interface IMarker { }

    [InlinerStruct]
    public static T Echo<T>(T value)
        where T : global::Shadowed.IMarker
        => value;
}
