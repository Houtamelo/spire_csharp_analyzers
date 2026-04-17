//@ should_pass

public partial class Host_021_PassThrough
{
    private static void Helper(Action<int> a, int x) => a(x);

    public static void Call([Inlinable] Action<int> action, int x)
    {
        Helper(action, x);
    }
}
