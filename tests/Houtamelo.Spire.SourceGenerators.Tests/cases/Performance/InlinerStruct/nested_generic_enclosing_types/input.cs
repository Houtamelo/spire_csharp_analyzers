using Houtamelo.Spire;

namespace Enclosing;

public interface IMarker { }

public partial class Outer<TOuter>
    where TOuter : unmanaged
{
    public partial class Middle<TMiddle>
        where TMiddle : class, IMarker, new()
    {
        public partial class Inner<TInner>
            where TInner : notnull, IMarker
        {
            [InlinerStruct]
            public TInner Pick(TOuter outer, TMiddle middle, TInner inner) => inner;
        }
    }
}
