//@ should_fail
// Abstract class switch where one arm has a when guard — that variant not fully covered — SPIRE009
#nullable enable
using Houtamelo.Spire;
namespace TestNs
{
    [EnforceExhaustiveness]
    public abstract class Notification { }
    public sealed class EmailNotification : Notification { public string Address { get; init; } = ""; }
    public sealed class SmsNotification : Notification { public string Phone { get; init; } = ""; }
    public sealed class PushNotification : Notification { public bool Silent { get; init; } }

    class Consumer
    {
        string Route(Notification n) => n switch //~ ERROR
        {
            EmailNotification e when e.Address.Contains("@") => $"email:{e.Address}",
            SmsNotification s => $"sms:{s.Phone}",
            PushNotification p => p.Silent ? "silent-push" : "push",
        };
    }
}
