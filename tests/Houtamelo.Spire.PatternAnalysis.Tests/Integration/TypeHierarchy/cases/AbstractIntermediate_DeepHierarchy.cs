//@ exhaustive
// Deep hierarchy: base -> abstract -> concrete, all leaves covered
#nullable enable

[EnforceExhaustiveness]
public abstract class Msg2 { }
public abstract class Request : Msg2 { }
public sealed class GetRequest : Request { }
public sealed class PostRequest : Request { }
public abstract class Response : Msg2 { }
public sealed class OkResponse : Response { }
public sealed class ErrorResponse : Response { }

public class AbstractIntermediate_DeepHierarchy
{
    public int Test(Msg2 m) => m switch
    {
        GetRequest => 1,
        PostRequest => 2,
        OkResponse => 3,
        ErrorResponse => 4,
    };
}
