//@ exhaustive
// (Axis, Toggle) — all 4 combinations
public class EnumCrossProduct
{
    public int Test(Axis a, Toggle t) => (a, t) switch
    {
        (Axis.X, Toggle.On) => 1,
        (Axis.X, Toggle.Off) => 2,
        (Axis.Y, Toggle.On) => 3,
        (Axis.Y, Toggle.Off) => 4,
    };
}
