//@ should_fail
// All enum members covered on Color? but no null arm and no default — SPIRE015
public class Detect_SwitchStatement_NullableEnum_MissingNull
{
    public void Method(Color? value)
    {
        switch (value) //~ ERROR
        {
            case Color.Red:
                break;
            case Color.Green:
                break;
            case Color.Blue:
                break;
        }
    }
}
