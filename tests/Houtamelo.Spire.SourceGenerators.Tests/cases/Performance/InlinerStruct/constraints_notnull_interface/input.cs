using Houtamelo.Spire;

namespace Constraints;

public interface IMarker { }

public static partial class Factory
{
    [InlinerStruct]
    public static T Echo<T>(T value)
        where T : notnull, IMarker
        => value;
}
