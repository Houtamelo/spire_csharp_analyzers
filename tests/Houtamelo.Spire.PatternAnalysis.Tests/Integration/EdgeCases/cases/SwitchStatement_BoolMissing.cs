//@ not_exhaustive
// Switch statement missing false
public class SwitchStatement_BoolMissing
{
    public int Test(bool b)
    {
        switch (b)
        {
            case true: return 1;
            //~ false
        }
        return -1;
    }
}
