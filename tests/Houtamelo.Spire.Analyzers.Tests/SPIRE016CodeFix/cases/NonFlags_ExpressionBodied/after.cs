public class Test
{
    private int _raw;
    public StatusNoZero Status => SpireEnum<StatusNoZero>.From(_raw);
}
