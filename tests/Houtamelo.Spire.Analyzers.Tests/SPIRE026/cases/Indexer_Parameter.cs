//@ should_fail
// SPIRE026: [Inlinable] on indexer parameter

public partial class Host_026_Indexer
{
    public int this[[Inlinable] Action<int> action] //~ ERROR
    {
        get { action(0); return 0; }
    }
}
