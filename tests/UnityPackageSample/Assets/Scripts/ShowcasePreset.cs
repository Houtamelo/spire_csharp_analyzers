using System.Collections.Generic;
using UnityEngine;

namespace Houtamelo.Spire.Showcase {

[CreateAssetMenu(menuName = "Spire/Showcase Preset")]
public sealed class ShowcasePreset: ScriptableObject {
    [Header("Command authoring")]
    public GameplayCommand selectedCommand = GameplayCommand.Idle();
    public List<GameplayCommand> commands = new();

    [Header("Property patterns and generic results")]
    public Shape shape = Shape.Point();
    public OperationResult<int> result = OperationResult<int>.Success(0);

    public void ResetExamples() {
        var sharedDamage = new DamageEffect(10);
        selectedCommand = GameplayCommand.Move(new Vector2(3, 4), 2);
        commands = new List<GameplayCommand> {
            GameplayCommand.Idle(),
            selectedCommand,
            GameplayCommand.Speak("Hello from Spire"),
            GameplayCommand.ApplyEffect(sharedDamage),
            GameplayCommand.ApplyEffect(sharedDamage),
            GameplayCommand.ApplyEffect(null),
        };

        shape = Shape.Rectangle(3, 4);
        result = OperationResult<int>.Success(42);
    }
}

}
