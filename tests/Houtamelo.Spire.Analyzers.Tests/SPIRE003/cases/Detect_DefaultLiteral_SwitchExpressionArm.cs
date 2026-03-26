//@ should_fail
// Ensure that SPIRE003 IS triggered when default is the value of a switch expression arm returning EnforceInitializationStruct.
public class Detect_DefaultLiteral_SwitchExpressionArm
{
    public EnforceInitializationStruct Method(int x)
    {
        return x switch
        {
            1 => new EnforceInitializationStruct(1),
            _ => default, //~ ERROR
        };
    }
}
