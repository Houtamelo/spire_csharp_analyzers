//@ exhaustive
// Interface with all implementors covered
#nullable enable
[EnforceExhaustiveness]
public interface IShape { }
public sealed class Circle : IShape { }
public sealed class Rectangle : IShape { }

public class InterfaceHierarchy
{
    public int Test(IShape s) => s switch
    {
        Circle => 1,
        Rectangle => 2,
    };
}
