// Diagnostic exercises; uncomment one block at a time.
// using UnityEngine;
//
// namespace Houtamelo.Spire.Showcase;
//
// static class UseCasesExamples {
//     static void Command(GameplayCommand command) {
//         if (command is { IsApplyEffect: true, applyEffect: var (effect) }) {
//             Debug.Log($"ApplyEffect: {effect}");
//         }
//
//         if (command is { IsMove: true }) {
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
//             case { IsIdle: true, teleport: var teleport }: // SPIRE013
//                 throw new System.NotImplementedException();
//             case { IsTeleport: true, teleport: var (target, duration) }:
//                 throw new System.NotImplementedException();
//             case { IsMove: true, move: var (destination, speed), speak: var speak }: // SPIRE013
//                 throw new System.NotImplementedException();
//             case { IsSpeak: true, speak: var (speech) }:
//                 throw new System.NotImplementedException();
//             case { IsApplyEffect: true, applyEffect: var (effect) }:
//                 throw new System.NotImplementedException();
//         }
//
//         switch (command) {
//             case { IsIdle: true }:
//                 throw new System.NotImplementedException();
//             case { IsTeleport: true }: {
//                 var teleport = command.teleport; // No diagnostics
//                 var move = command.move; // SPIRE013
//             }
//             break;
//             case { IsMove: true, move: var (destination, speed) }:
//                 throw new System.NotImplementedException();
//             case { IsSpeak: true, speak: var (speech) }:
//                 throw new System.NotImplementedException();
//             case { IsApplyEffect: true, applyEffect: { effect: Effect effect } }:
//                 throw new System.NotImplementedException();
//             case { IsApplyEffect: true, applyEffect: { effect: null } }:
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
//             case { IsIdle: true }:
//                 throw new System.NotImplementedException();
//             case { IsTeleport: true }:
//                 throw new System.NotImplementedException();
//             case { IsMove: true, move: var (destination, speed) }:
//                 throw new System.NotImplementedException();
//             case { IsSpeak: true, speak: var (speech) }:
//                 throw new System.NotImplementedException();
//             case { IsApplyEffect: true, applyEffect: { effect: null } }:
//                 throw new System.NotImplementedException();
//             // Diagnostic: case `effect: not null` is not handled.
//         }
//     }
// }
