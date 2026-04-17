//@ should_fail
// SPIRE021: parameter captured by a nested lambda

public partial class Host_021_Lambda
{
    public static Action<int> Call([Inlinable] Action<int> action)
    {
        return x => action(x); //~ ERROR
    }
}
