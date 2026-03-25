//@ should_fail
// Ensure that SPIRE005 IS triggered when Activator.CreateInstance(typeof(EnforceInitializationStruct)) is in a switch expression arm.
public class Detect_TypeOnly_SwitchExpressionArm
{
    public object Method(int x)
    {
        return x switch
        {
            1 => Activator.CreateInstance(typeof(EnforceInitializationStruct)), //~ ERROR
            _ => new object()
        };
    }
}
