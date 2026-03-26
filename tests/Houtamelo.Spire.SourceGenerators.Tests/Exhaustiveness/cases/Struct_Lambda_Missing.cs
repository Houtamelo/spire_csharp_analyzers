//@ should_fail
// Switch inside Func<LambdaShape, int> lambda, missing LambdaSquare variant — SPIRE009
using Houtamelo.Spire;
using System;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct LambdaShape
    {
        [Variant] public static partial LambdaShape LambdaCircle(double lambdaRadius);
        [Variant] public static partial LambdaShape LambdaSquare(int lambdaSide);
    }

    class LambdaConsumer
    {
        Func<LambdaShape, int> GetMapper()
        {
            return s => s switch //~ ERROR
            {
                (LambdaShape.Kind.LambdaCircle, double r) => (int)r,
            };
        }
    }
}
