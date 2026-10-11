using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Outcome
    {
        [Variant] public static partial Outcome Ok(int value);
        [Variant] public static partial Outcome Err(string message);
    }

    class Consumer
    {
        int Test(Outcome o) => o switch
        {
            (Outcome.Kind.Ok, var v) => v,
            { kind: Outcome.Kind.Err, message: var message } => throw new System.NotImplementedException()
        };
    }
}
