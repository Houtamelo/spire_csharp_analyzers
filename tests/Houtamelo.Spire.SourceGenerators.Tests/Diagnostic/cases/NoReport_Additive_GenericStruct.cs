//@ should_pass
// No diagnostics: Additive layout on generic struct is valid
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion(Layout.Additive)]
    partial struct Either<TLeft, TRight>
    {
        [Variant] public static partial Either<TLeft, TRight> Left(TLeft leftVal);
        [Variant] public static partial Either<TLeft, TRight> Right(TRight rightVal);
    }
}
