//@ should_pass
// Ensure that SPIRE015 is NOT triggered when switching on a string value (not an enum).
public class NoReport_SwitchStatement_BuiltinString_Incomplete
{
    public void Method(string value)
    {
        switch (value)
        {
            case "hello":
                break;
            case "world":
                break;
        }
    }
}
