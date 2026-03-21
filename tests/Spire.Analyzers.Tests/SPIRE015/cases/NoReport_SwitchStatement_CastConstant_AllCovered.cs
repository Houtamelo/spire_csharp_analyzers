//@ should_pass
// Ensure that SPIRE015 is NOT triggered when case (Color)0:, case (Color)1:, case (Color)2: exhaustively cover all Color members by underlying value (enum-typed constants).
public class NoReport_SwitchStatement_CastConstant_AllCovered
{
    public void Method(Color value)
    {
        switch (value)
        {
            case (Color)0:
                break;
            case (Color)1:
                break;
            case (Color)2:
                break;
        }
    }
}
