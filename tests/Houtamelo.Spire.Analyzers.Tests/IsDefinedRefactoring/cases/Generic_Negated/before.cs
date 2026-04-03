public class Test
{
    public void Method(Status v)
    {
        if (![|Enum.IsDefined<Status>(v)|])
        {
        }
    }
}
