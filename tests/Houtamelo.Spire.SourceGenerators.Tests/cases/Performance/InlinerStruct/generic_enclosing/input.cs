using Houtamelo.Spire;

namespace Enclosing;

public interface IMarker { }

public partial class Box<TOuter>
    where TOuter : unmanaged
{
    public partial class Inner<TInner>
        where TInner : class, IMarker, new()
    {
        [InlinerStruct]
        public TInner Pick(TOuter outer, TInner inner) => inner;
    }
}
