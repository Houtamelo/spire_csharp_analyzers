//@ should_fail
// SPIRE026: [Inlinable] on property setter value parameter via [param:]

public partial class Host_026_PropSet
{
    private Action<int>? _store;
    public Action<int>? Handler
    {
        get => _store;
        [param: Inlinable] //~ ERROR
        set => _store = value;
    }
}
