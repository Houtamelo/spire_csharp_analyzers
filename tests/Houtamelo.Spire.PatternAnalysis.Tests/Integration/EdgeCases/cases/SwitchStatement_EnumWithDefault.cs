//@ exhaustive
// Switch statement with default arm covering remaining
public class SwitchStatement_EnumWithDefault
{
    public int Test(TwoCases c)
    {
        switch (c)
        {
            case TwoCases.A: return 1;
            default: return 2;
        }
    }
}
