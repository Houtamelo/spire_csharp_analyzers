using System.Collections.Generic;
using UnityEngine;

namespace Houtamelo.Spire.Showcase {

public sealed class ShowcaseController: MonoBehaviour {
    static readonly  List<int>      InlinerScores = new() { 1, 2, 3 };
    [SerializeField] ShowcasePreset preset        = null!;

    int nextCommand;

    public ShowcasePreset Preset { get => preset; set => preset = value; }

    public int       Health      { get; private set; } = 100;
    public Vector2   Position    { get; private set; }
    public float     LastSpeed   { get; private set; }
    public string    LastMessage { get; private set; } = "";
    public ActorMode Mode        { get; private set; } = ActorMode.Idle;

    public void Execute(GameplayCommand command) {
        switch (command) {
            case { IsIdle: true }:
                Mode = ActorMode.Idle;
            break;
            case { IsTeleport: true, teleport: var (target, duration) }:
                Position = target;
                LastSpeed = duration;
                Mode = ActorMode.Acting;
            break;
            case { IsMove: true, move: var (destination, speed) }:
                Position = destination;
                LastSpeed = speed;
                Mode = ActorMode.Acting;
            break;
            case { IsSpeak: true, speak: var (speech) }:
                LastMessage = speech;
                Mode = ActorMode.Acting;
            break;
            case { IsApplyEffect: true, applyEffect: var (effect) }:
                if (effect is not null) {
                    Health = effect.Apply(Health);
                }

                Mode = ActorMode.Acting;
            break;
        }
    }

    public void ResetSession() {
        Health = 100;
        Position = Vector2.zero;
        LastSpeed = 0;
        LastMessage = "";
        Mode = ActorMode.Idle;
        nextCommand = 0;
    }

    void OnGUI() {
        if (!preset) return;

        GUILayout.BeginArea(new Rect(24, 24, Mathf.Min(Screen.width - 48, 850), Screen.height - 48), GUI.skin!.box!);
        GUILayout.Label("Houtamelo.Spire — Unity showcase", GUI.skin.box!);
        GUILayout.Label("Select ShowcasePreset in the Project window to edit variants and their fields.");
        GUILayout.Space(12);
        GUILayout.Label($"Selected command: {preset.selectedCommand.kind}");
        if (GUILayout.Button("Execute selected command")) Execute(preset.selectedCommand);
        if (preset.commands.Count > 0
            && GUILayout.Button($"Execute queue item {nextCommand + 1}/{preset.commands.Count}")) {
            Execute(preset.commands[nextCommand]);
            nextCommand = (nextCommand + 1) % preset.commands.Count;
        }

        if (GUILayout.Button("Reset runtime state")) ResetSession();

        GUILayout.Space(12);
        GUILayout.Label($"Mode: {Mode}   Health: {Health}   Position: {Position}   Command speed: {LastSpeed}");
        GUILayout.Label($"Speech: {LastMessage}");
        GUILayout.Label($"Move commands in queue (OfKind): {ShowcaseAlgorithms.CountMoves(preset.commands)}");
        GUILayout.Label($"Shape area (payload deconstruction): {ShowcaseAlgorithms.Area(preset.shape):0.##}");
        var result = preset.result;
        if (result is { IsSuccess: true, success: var (value) })
            GUILayout.Label($"Generic result: Success({value})");
        else if (result is { IsFailure: true, failure: var (error) })
            GUILayout.Label($"Generic result: Failure({error})");

        GUILayout.Label(
            $"Generated inliners: doubled sum of [1, 2, 3] = {ShowcaseAlgorithms.SumDoubledScores(InlinerScores)}");

        GUILayout.Space(12);
        GUILayout.Label("Managed references: queue items 4 and 5 share one DamageEffect; item 6 preserves null.");
        if (GUILayout.Button("Apply a HealingEffect (+15)"))
            Execute(GameplayCommand.ApplyEffect(new HealingEffect(15)));

        GUILayout.EndArea();
    }
}

}
