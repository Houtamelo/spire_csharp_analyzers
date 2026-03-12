//@ should_fail
// Ensure that SPIRE003 IS triggered when default is the value of a switch expression arm returning MustInitStruct.
public class Detect_DefaultLiteral_SwitchExpressionArm
{
    public MustInitStruct Method(int x)
    {
        return x switch
        {
            1 => new MustInitStruct(1),
            _ => default, //~ ERROR
        };
    }
}
