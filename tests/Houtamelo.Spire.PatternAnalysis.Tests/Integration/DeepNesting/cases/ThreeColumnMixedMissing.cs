//@ not_exhaustive
// (Axis, Toggle, bool) missing (Y, Off, false)
public class ThreeColumnMixedMissing
{
    public int Test(Axis a, Toggle t, bool b) => (a, t, b) switch
    {
        (Axis.X, _, _) => 1,
        (Axis.Y, Toggle.On, _) => 2,
        (Axis.Y, Toggle.Off, true) => 3,
    };
}
