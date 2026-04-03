public class Test
{
    public void Method(Status v)
    {
        bool valid = [|Enum.IsDefined<Status>(v)|];
    }
}
