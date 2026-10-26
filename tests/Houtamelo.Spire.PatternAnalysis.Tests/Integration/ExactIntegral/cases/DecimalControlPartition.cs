//@ exhaustive
// Decimal relational patterns remain on the floating/decimal path.
public class DecimalControlPartition
{
    public int Test(decimal value) => value switch
    {
        <= 0m => 0,
        > 0m => 1,
    };
}
