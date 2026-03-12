//@ should_fail
// Ensure that SPIRE005 IS triggered when Activator.CreateInstance(typeof(MustInitStruct)) is in a switch expression arm.
public class Detect_TypeOnly_SwitchExpressionArm
{
    public object Method(int x)
    {
        return x switch
        {
            1 => Activator.CreateInstance(typeof(MustInitStruct)), //~ ERROR
            _ => new object()
        };
    }
}
