//@ should_fail
// Ensure that SPIRE015 IS triggered when an incomplete switch statement on Color appears inside a lambda body.
public class Detect_SwitchStatement_InLambda
{
    public void Method()
    {
        Action<Color> handler = color =>
        {
            switch (color) //~ ERROR
            {
                case Color.Red:
                    break;
            }
        };
    }
}
