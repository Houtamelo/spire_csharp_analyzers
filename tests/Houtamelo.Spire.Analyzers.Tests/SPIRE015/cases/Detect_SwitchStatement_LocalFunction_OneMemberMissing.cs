//@ should_fail
// Ensure that SPIRE015 IS triggered when an incomplete switch statement on Color appears inside a local function.
public class Detect_SwitchStatement_LocalFunction_OneMemberMissing
{
    public void Method(Color value)
    {
        Process(value);

        void Process(Color c)
        {
            switch (c) //~ ERROR
            {
                case Color.Red:
                    break;
                case Color.Green:
                    break;
            }
        }
    }
}
