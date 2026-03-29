//@ not_exhaustive
// Missing None variant
#nullable enable
public class RecordTypePattern_Missing
{
    public int Test(Option o) => o switch
    {
        Option.Some s => s.Value,
        //~ Option.None
    };
}
