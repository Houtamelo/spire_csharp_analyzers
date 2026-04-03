public class Test
{
    public void Method(int v)
    {
        if ([|Enum.IsDefined(typeof(Status), v)|])
        {
        }
    }
}
