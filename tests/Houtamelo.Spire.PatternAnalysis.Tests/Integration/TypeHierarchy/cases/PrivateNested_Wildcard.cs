//@ exhaustive
// Private-ctor base covered by wildcard
#nullable enable

[EnforceExhaustiveness]
public abstract class Cmd2
{
    private Cmd2() { }
    public sealed class Run : Cmd2 { }
    public sealed class Stop : Cmd2 { }
}

public class PrivateNested_Wildcard
{
    public int Test(Cmd2 c) => c switch
    {
        Cmd2.Run => 1,
        _ => 2,
    };
}
