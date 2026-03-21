//@ should_fail
// Ensure that SPIRE015 IS triggered when an incomplete switch statement on Color appears inside a while loop body.
public class Detect_SwitchStatement_InWhileLoop
{
    public void Method(Color color, bool condition)
    {
        while (condition)
        {
            switch (color) //~ ERROR
            {
                case Color.Red:
                    break;
            }
        }
    }
}
