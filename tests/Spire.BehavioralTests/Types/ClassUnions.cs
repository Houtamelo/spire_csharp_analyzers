using Spire;

[DiscriminatedUnion]
public abstract partial class CommandCls
{
    public sealed partial class Start : CommandCls { }
    public sealed partial class Stop : CommandCls
    {
        public string Reason { get; }
        public Stop(string reason) { Reason = reason; }
    }
}
