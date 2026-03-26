//@ should_fail
// Ensure that SPIRE015 IS triggered when an incomplete switch statement on Color appears inside a foreach loop body.
public class Detect_SwitchStatement_InForeachLoop
{
    public void Method(Color[] colors)
    {
        foreach (var color in colors)
        {
            switch (color) //~ ERROR
            {
                case Color.Red:
                    break;
                case Color.Green:
                    break;
            }
        }
    }
}
