//@ exhaustive
// (Axis?, Toggle) — nullable enum in tuple position
public class NullableEnumInTuple
{
    public int Test(Axis? a, Toggle t) => (a, t) switch
    {
        (null, _) => 0,
        (Axis.X, _) => 1,
        (Axis.Y, Toggle.On) => 2,
        (Axis.Y, Toggle.Off) => 3,
    };
}
