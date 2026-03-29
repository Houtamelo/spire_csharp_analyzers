//@ exhaustive
// Wildcard covers remaining
#nullable enable
public class RecordWildcard_Exhaustive
{
    public int Test(Option o) => o switch
    {
        Option.Some s => s.Value,
        _ => 0,
    };
}
