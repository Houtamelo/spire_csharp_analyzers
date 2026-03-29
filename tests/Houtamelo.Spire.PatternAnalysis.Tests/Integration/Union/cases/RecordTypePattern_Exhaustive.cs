//@ exhaustive
// All record DU variants covered via type patterns
#nullable enable
public class RecordTypePattern_Exhaustive
{
    public int Test(Option o) => o switch
    {
        Option.Some s => s.Value,
        Option.None => 0,
    };
}
