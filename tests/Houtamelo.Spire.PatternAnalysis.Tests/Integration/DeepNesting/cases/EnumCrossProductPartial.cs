//@ not_exhaustive
// (Axis, Toggle) — missing (Y, Off)
public class EnumCrossProductPartial
{
    public int Test(Axis a, Toggle t) => (a, t) switch
    {
        (Axis.X, Toggle.On) => 1,
        (Axis.X, Toggle.Off) => 2,
        (Axis.Y, Toggle.On) => 3,
        //~ (Axis.Y, Toggle.Off)
    };
}
