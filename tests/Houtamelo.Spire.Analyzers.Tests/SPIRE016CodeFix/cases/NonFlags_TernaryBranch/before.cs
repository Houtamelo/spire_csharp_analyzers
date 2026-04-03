public class Test
{
    public StatusNoZero Method(int v, bool b)
    {
        return b ? StatusNoZero.Active : (StatusNoZero)v;
    }
}
