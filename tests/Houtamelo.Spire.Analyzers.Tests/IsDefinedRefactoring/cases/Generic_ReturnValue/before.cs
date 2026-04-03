public class Test
{
    public bool Method(Status v)
    {
        return [|Enum.IsDefined<Status>(v)|];
    }
}
