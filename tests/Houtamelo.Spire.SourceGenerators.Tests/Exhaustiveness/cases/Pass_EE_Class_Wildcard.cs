//@ should_pass
// Abstract class with wildcard arm covering all cases — no diagnostic
#nullable enable
using Houtamelo.Spire;
namespace TestNs
{
    [EnforceExhaustiveness]
    public abstract class Expr { }
    public sealed class Literal : Expr { public int Value { get; init; } }
    public sealed class Add : Expr { }
    public sealed class Multiply : Expr { }

    class Consumer
    {
        int Eval(Expr e) => e switch
        {
            Literal lit => lit.Value,
            _ => 0,
        };
    }
}
