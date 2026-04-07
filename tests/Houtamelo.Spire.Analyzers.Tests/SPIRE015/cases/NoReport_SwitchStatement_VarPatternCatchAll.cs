//@ should_pass
// SPIRE015 does NOT fire when a switch statement has a `case var x:` catch-all.
public class NoReport_SwitchStatement_VarPatternCatchAll
{
    public void Method(Color color)
    {
        switch (color)
        {
            case Color.Red:
                break;
            case var c:
                _ = c;
                break;
        }
    }
}
