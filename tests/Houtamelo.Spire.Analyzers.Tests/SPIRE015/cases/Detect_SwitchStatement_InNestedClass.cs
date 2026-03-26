//@ should_fail
// Ensure that SPIRE015 IS triggered when an incomplete switch statement on Color appears inside a method of a nested class.
public class Detect_SwitchStatement_InNestedClass
{
    public class Inner
    {
        public void Method(Color color)
        {
            switch (color) //~ ERROR
            {
                case Color.Red:
                    break;
            }
        }
    }
}
