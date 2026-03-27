//@ exhaustive
// (Axis, Toggle, bool) — enum x enum x bool cross-product
public class ThreeColumnMixed
{
    public int Test(Axis a, Toggle t, bool b) => (a, t, b) switch
    {
        (Axis.X, _, _) => 1,
        (Axis.Y, Toggle.On, _) => 2,
        (Axis.Y, Toggle.Off, true) => 3,
        (Axis.Y, Toggle.Off, false) => 4,
    };
}
