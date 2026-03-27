//@ exhaustive
// Switch statement with all bool cases
public class SwitchStatement_Bool
{
    public int Test(bool b)
    {
        switch (b)
        {
            case true: return 1;
            case false: return 2;
        }
    }
}
