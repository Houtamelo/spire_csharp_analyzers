//@ should_pass

public partial class Host_021_Alias
{
    public static void Call([Inlinable] Action<int>? action, int x)
    {
        var a = action;
        if (a != null)
            a(x);
    }
}
