//@ should_pass
// Expression-bodied property with switch, all variants covered — no diagnostic
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct ExprShape
    {
        [Variant] public static partial ExprShape ExprCircle(double exprRadius);
        [Variant] public static partial ExprShape ExprRect(float exprWidth, float exprHeight);
    }

    class ExprShapeHolder
    {
        private readonly ExprShape _shape;
        public ExprShapeHolder(ExprShape shape) { _shape = shape; }

        public double Area => _shape switch
        {
            (ExprShape.Kind.ExprCircle, double r) => System.Math.PI * r * r,
            (ExprShape.Kind.ExprRect, float w, float h) => w * h,
        };
    }
}
