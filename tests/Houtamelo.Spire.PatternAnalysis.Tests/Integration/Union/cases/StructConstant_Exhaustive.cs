//@ exhaustive
// Kind enum constants matched directly (sanity check — uses EnumDomain, not struct DU path)
public class StructConstant_Exhaustive
{
    public int Test(Shape.Kind k) => k switch
    {
        Shape.Kind.Circle => 1,
        Shape.Kind.Rectangle => 2,
        Shape.Kind.Triangle => 3,
    };
}
