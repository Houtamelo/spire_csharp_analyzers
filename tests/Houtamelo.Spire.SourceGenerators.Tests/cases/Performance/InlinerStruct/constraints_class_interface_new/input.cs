using Houtamelo.Spire;

namespace Constraints;

public interface IMarker { }

public sealed class Marker : IMarker
{
    public Marker() { }
}

public static partial class Factory
{
    [InlinerStruct]
    public static T Create<T>(T value)
        where T : class, IMarker, new()
        => value;
}
