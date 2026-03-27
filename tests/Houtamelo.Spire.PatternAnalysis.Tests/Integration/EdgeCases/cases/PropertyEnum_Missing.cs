//@ not_exhaustive
// Property pattern missing TwoCases.B
public class PropertyEnum_Missing
{
    public int Test(SingleField s) => s switch
    {
        { Value: TwoCases.A } => 1,
        //~ { Value: TwoCases.B }
    };
}
