//@ not_exhaustive
// Interface missing two implementors
#nullable enable

[EnforceExhaustiveness]
public interface ILogLevel { }
public sealed class Debug2 : ILogLevel { }
public sealed class Info : ILogLevel { }
public sealed class Warn : ILogLevel { }
public sealed class Error2 : ILogLevel { }

public class Interface_MissingTwo
{
    public int Test(ILogLevel l) => l switch
    {
        Debug2 => 1,
        Info => 2,
        //~ Warn
        //~ Error2
    };
}
