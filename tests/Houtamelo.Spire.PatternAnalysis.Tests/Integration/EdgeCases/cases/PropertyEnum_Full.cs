//@ exhaustive
// Property pattern covering all enum values
public class PropertyEnum_Full
{
    public int Test(SingleField s) => s switch
    {
        { Value: TwoCases.A } => 1,
        { Value: TwoCases.B } => 2,
    };
}
