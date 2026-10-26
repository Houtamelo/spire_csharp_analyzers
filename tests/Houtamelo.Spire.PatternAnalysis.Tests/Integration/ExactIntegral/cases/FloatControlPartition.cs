//@ exhaustive
// Float relational handling remains on its existing path.
public class FloatControlPartition
{
    public int Test(float value) => value switch
    {
        <= 0.0f => 0,
        > 0.0f => 1,
    };
}
