//@ exhaustive
// Private nested subtypes all covered
#nullable enable

[EnforceExhaustiveness]
public abstract class Expr
{
    private Expr() { }
    public sealed class Literal : Expr { public int Value; }
    public sealed class Add : Expr { public Expr Left, Right; }
    public sealed class Neg : Expr { public Expr Inner; }
}

public class PrivateNested_AllCovered
{
    public int Test(Expr e) => e switch
    {
        Expr.Literal => 1,
        Expr.Add => 2,
        Expr.Neg => 3,
    };
}
