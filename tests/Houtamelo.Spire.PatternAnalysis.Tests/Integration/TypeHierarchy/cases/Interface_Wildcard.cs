//@ exhaustive
// Interface covered by wildcard
#nullable enable

[EnforceExhaustiveness]
public interface IAction { }
public sealed class Jump : IAction { }
public sealed class Duck : IAction { }
public sealed class Shoot : IAction { }

public class Interface_Wildcard
{
    public int Test(IAction a) => a switch
    {
        Jump => 1,
        _ => 2,
    };
}
