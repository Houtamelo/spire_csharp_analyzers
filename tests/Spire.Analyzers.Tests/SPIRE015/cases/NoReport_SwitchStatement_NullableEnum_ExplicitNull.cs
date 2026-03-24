//@ should_pass
// All enum members + explicit null arm on Color? — no diagnostic
public class NoReport_SwitchStatement_NullableEnum_ExplicitNull
{
    public void Method(Color? value)
    {
        switch (value)
        {
            case null:
                break;
            case Color.Red:
                break;
            case Color.Green:
                break;
            case Color.Blue:
                break;
        }
    }
}
