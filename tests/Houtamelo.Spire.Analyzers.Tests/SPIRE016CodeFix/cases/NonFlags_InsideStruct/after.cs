public struct Wrapper
{
    public StatusNoZero Convert(int v)
    {
        return SpireEnum<StatusNoZero>.From(v);
    }
}
