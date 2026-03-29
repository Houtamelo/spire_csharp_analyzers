//@ exhaustive
// Interface with all implementors covered
#nullable enable

[EnforceExhaustiveness]
public interface IResult { }
public sealed class Success : IResult { }
public sealed class Failure : IResult { }

public class Interface_AllImplementors
{
    public int Test(IResult r) => r switch
    {
        Success => 1,
        Failure => 2,
    };
}
