// using UnityEngine;
//
// namespace Houtamelo.Spire.Showcase;
//
// static class UseCasesExamples {
//     static void Command(GameplayCommand command) {
//         if (command is { kind: GameplayCommand.Kind.ApplyEffect, applyEffect: var apply }) {
//             Debug.Log($"ApplyEffect: {apply.effect}");
//         }
//
//         if (command is { kind: GameplayCommand.Kind.Move }) {
//             var applyEffect = command.applyEffect; // SPIRE013
//         }
//
//         {
//             var applyEffect = command.applyEffect; // SPIRE014
//         }
//
//         // You can disable diagnostics:
// #pragma warning disable SPIRE014
//         {
//             var applyEffect = command.applyEffect;
//         }
// #pragma warning restore SPIRE014
//
//         // No diagnostics
//         if (command.IsMove) {
//             var move = command.move;
//         }
//
//         if (command.IsMove) {
//             var applyEffect = command.applyEffect; // SPIRE013
//         }
//
//         // No diagnostics
//         switch (command) {
//             case { kind: GameplayCommand.Kind.Idle }:
//                 throw new System.NotImplementedException();
//             case { kind: GameplayCommand.Kind.Teleport, teleport: var (target, duration) }:
//                 throw new System.NotImplementedException();
//             case { kind: GameplayCommand.Kind.Move, move: { destination: var destination, speed: var speed } }:
//                 throw new System.NotImplementedException();
//             case { kind: GameplayCommand.Kind.Speak, speak: { speech: var speech } }:
//                 throw new System.NotImplementedException();
//             case { kind: GameplayCommand.Kind.ApplyEffect, applyEffect: { effect: var effect } }:
//                 throw new System.NotImplementedException();
//         }
//
//         switch (command) {
//             case { kind: GameplayCommand.Kind.Idle, teleport: var teleport }: // SPIRE013
//                 throw new System.NotImplementedException();
//             case { kind: GameplayCommand.Kind.Teleport, teleport: { target: var target, duration: var duration } }:
//                 throw new System.NotImplementedException();
//             case { kind: GameplayCommand.Kind.Move, move: { destination: var destination, speed: var speed }, speak: var speak }: // SPIRE013
//                 throw new System.NotImplementedException();
//             case { kind: GameplayCommand.Kind.Speak, speak: { speech: var speech } }:
//                 throw new System.NotImplementedException();
//             case { kind: GameplayCommand.Kind.ApplyEffect, applyEffect: { effect: var effect } }:
//                 throw new System.NotImplementedException();
//         }
//
//         switch (command) {
//             case { kind: GameplayCommand.Kind.Idle }:
//                 throw new System.NotImplementedException();
//             case { kind: GameplayCommand.Kind.Teleport }: {
//                 var teleport = command.teleport; // No diagnostics
//                 var move = command.move; // SPIRE013
//             }
//             break;
//             case { kind: GameplayCommand.Kind.Move, move: { destination: var destination, speed: var speed } }:
//                 throw new System.NotImplementedException();
//             case { kind: GameplayCommand.Kind.Speak, speak: { speech: var speech } }:
//                 throw new System.NotImplementedException();
//             case { kind: GameplayCommand.Kind.ApplyEffect, applyEffect: { effect: Effect effect } }:
//                 throw new System.NotImplementedException();
//             case { kind: GameplayCommand.Kind.ApplyEffect, applyEffect: { effect: null } }:
//                 throw new System.NotImplementedException();
//         }
//
//         // No diagnostics
//         switch (command) {
//             case { IsIdle: true }:
//                 throw new System.NotImplementedException();
//             case { IsTeleport: true, teleport: var (target, duration) }:
//                 throw new System.NotImplementedException();
//             case { IsMove: true, move: var (destination, speed) }:
//                 throw new System.NotImplementedException();
//             case { IsSpeak: true, speak: var (speech) }:
//                 throw new System.NotImplementedException();
//             case { IsApplyEffect: true, applyEffect: var (effect) }:
//                 throw new System.NotImplementedException();
//         }
//
//         switch (command) {
//             case { kind: GameplayCommand.Kind.Idle }:
//                 throw new System.NotImplementedException();
//             case { kind: GameplayCommand.Kind.Teleport }:
//                 throw new System.NotImplementedException();
//             case { kind: GameplayCommand.Kind.Move, move: { destination: var destination, speed: var speed } }:
//                 throw new System.NotImplementedException();
//             case { kind: GameplayCommand.Kind.Speak, speak: { speech: var speech } }:
//                 throw new System.NotImplementedException();
//             case { kind: GameplayCommand.Kind.ApplyEffect, applyEffect: { effect: null } }:
//                 throw new System.NotImplementedException();
//             // Diagnostic: case `effect: not null` is not handled.
//         }
//     }
// }
