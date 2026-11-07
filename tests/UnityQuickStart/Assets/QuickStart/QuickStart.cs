using System;
using UnityEngine;

namespace Houtamelo.Spire.QuickStart;

[Serializable]
[DiscriminatedUnion]
public partial struct Command
{
    [Variant] public static partial Command Idle();
    [Variant] public static partial Command Move(Vector2 destination, float speed);
    [Variant] public static partial Command Speak(string text);
}

public sealed class QuickStart : MonoBehaviour
{
    [SerializeField] private Command command = Command.Move(new Vector2(2, 3), 4);

    [ContextMenu("Describe command")]
    private void DescribeCommand() => Debug.Log(Describe(command), this);

    public static string Describe(Command value) => value switch
    {
        { IsIdle: true } => "Idle",
        { IsMove: true } => "Move at speed " + value.move.speed,
        { IsSpeak: true } => value.speak.text,
        _ => throw new InvalidOperationException("Unknown command kind."),
    };
}
