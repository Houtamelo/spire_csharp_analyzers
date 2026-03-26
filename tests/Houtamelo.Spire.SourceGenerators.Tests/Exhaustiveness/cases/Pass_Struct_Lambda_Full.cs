//@ should_pass
// Switch in lambda, all variants covered — no diagnostic
using Houtamelo.Spire;
using System;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct LambdaFullShape
    {
        [Variant] public static partial LambdaFullShape LambdaFullCircle(double lambdaFullRadius);
        [Variant] public static partial LambdaFullShape LambdaFullSquare(int lambdaFullSide);
    }

    class PassLambdaFullConsumer
    {
        Func<LambdaFullShape, int> GetMapper()
        {
            return s => s switch
            {
                (LambdaFullShape.Kind.LambdaFullCircle, double r) => (int)r,
                (LambdaFullShape.Kind.LambdaFullSquare, int side) => side,
            };
        }
    }
}
