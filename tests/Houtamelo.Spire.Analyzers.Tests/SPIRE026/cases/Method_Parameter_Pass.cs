//@ should_pass

public partial class Host_026_Method
{
    public static void Call([Inlinable] Action<int> action, int x)
    {
        action(x);
    }
}
