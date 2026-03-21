//@ should_fail
// Ensure that SPIRE015 IS triggered when an incomplete switch statement on Color appears inside a for loop body.
public class Detect_SwitchStatement_InForLoop
{
    public void Method(Color[] colors)
    {
        for (int i = 0; i < colors.Length; i++)
        {
            switch (colors[i]) //~ ERROR
            {
                case Color.Red:
                    break;
            }
        }
    }
}
