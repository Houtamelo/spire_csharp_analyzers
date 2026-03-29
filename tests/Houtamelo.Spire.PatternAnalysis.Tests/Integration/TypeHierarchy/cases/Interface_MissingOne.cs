//@ not_exhaustive
// Interface missing one implementor
#nullable enable

[EnforceExhaustiveness]
public interface IState { }
public sealed class Idle : IState { }
public sealed class Running : IState { }
public sealed class Stopped : IState { }

public class Interface_MissingOne
{
    public int Test(IState s) => s switch
    {
        Idle => 1,
        Running => 2,
        //~ Stopped
    };
}
