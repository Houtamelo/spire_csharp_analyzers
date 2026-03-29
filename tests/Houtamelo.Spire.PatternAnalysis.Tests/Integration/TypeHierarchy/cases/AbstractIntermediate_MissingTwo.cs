//@ not_exhaustive
// Deep hierarchy missing two leaves
#nullable enable

[EnforceExhaustiveness]
public abstract class Event { }
public abstract class UIEvent : Event { }
public sealed class ClickEvent : UIEvent { }
public sealed class KeyEvent : UIEvent { }
public abstract class NetEvent : Event { }
public sealed class ConnectEvent : NetEvent { }
public sealed class DisconnectEvent : NetEvent { }

public class AbstractIntermediate_MissingTwo
{
    public int Test(Event e) => e switch
    {
        ClickEvent => 1,
        KeyEvent => 2,
        //~ ConnectEvent
        //~ DisconnectEvent
    };
}
