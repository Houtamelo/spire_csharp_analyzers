using Spire;

[DiscriminatedUnion(Json = JsonLibrary.SystemTextJson)]
public abstract partial class JsonCmdStj
{
    public sealed partial class Start : JsonCmdStj { }
    public sealed partial class Stop : JsonCmdStj
    {
        public string Reason { get; }
        public Stop(string Reason) { this.Reason = Reason; }
    }
}

[DiscriminatedUnion(Json = JsonLibrary.NewtonsoftJson)]
public abstract partial class JsonCmdNsj
{
    public sealed partial class Start : JsonCmdNsj { }
    public sealed partial class Stop : JsonCmdNsj
    {
        public string Reason { get; }
        public Stop(string Reason) { this.Reason = Reason; }
    }
}
