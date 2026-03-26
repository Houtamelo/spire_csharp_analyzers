//@ should_pass
#nullable enable
// All generic record variants covered with type patterns
using Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    public partial record Either<L, R>
    {
        public partial record Left(L Value) : Either<L, R>;
        public partial record Right(R Value) : Either<L, R>;
    }
    class C
    {
        int Test(Either<string, int> e) => e switch
        {
            Either<string, int>.Left => -1,
            Either<string, int>.Right => 1,
        };
    }
}
