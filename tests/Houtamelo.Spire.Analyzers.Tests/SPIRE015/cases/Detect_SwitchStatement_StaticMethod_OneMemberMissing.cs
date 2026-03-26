//@ should_fail
// Ensure that SPIRE015 IS triggered when an incomplete switch statement on Color appears inside a static method.
public class Detect_SwitchStatement_StaticMethod_OneMemberMissing
{
    public static void Method(Color value)
    {
        switch (value) //~ ERROR
        {
            case Color.Red:
                break;
            case Color.Green:
                break;
        }
    }
}
