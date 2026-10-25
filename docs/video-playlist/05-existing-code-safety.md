# 5. Protecting existing code with a few attributes

Show that Spire can improve an existing Unity codebase without converting its
architecture to discriminated unions.

## Example and feature grouping

Boss Room declares ConnectStatus and ReconnectMessage in
[ConnectionManager.cs](https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop/blob/1299ba4fc97d83b634e7d32a86c438ba668d7f9c/Assets/Scripts/ConnectionManagement/ConnectionManager.cs).
Its [connection-status UI](https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop/blob/1299ba4fc97d83b634e7d32a86c438ba668d7f9c/Assets/Scripts/Gameplay/UI/ConnectionStatusMessageUIManager.cs)
uses a default arm to warn at runtime when a new status lacks a message.

Group `[EnforceExhaustiveness]`, explicit handling of intentionally silent enum
members, `SpireEnum<TEnum>` checked conversion, and `[EnforceInitialization]` on
a struct whose default state the application actually rejects. ReconnectMessage
is a candidate for the last demonstration, subject to reviewing its producers.

## Demonstration sequence

1. Show existing connection feedback and its enum-based UI handler.
2. Add the exhaustiveness attribute and replace the catch-all policy with explicit
   handling. An unguarded default arm opts out; the attribute alone is insufficient.
3. Add a plausible connection outcome and show the compiler identifying the UI
   handler that must be updated. Apply the fix and trigger the new feedback.
4. At a real integer input boundary, compare an unchecked enum cast with
   `SpireEnum<TEnum>.TryFrom`; handle invalid input explicitly. Omit this segment
   if the chosen subsystem has no such boundary.
5. If an existing struct must be constructed explicitly, annotate it and show an
   accidental default or uninitialized array being diagnosed, then repair it.

## Preparation and recording evidence

Use local connection scenarios and keep the Netcode protocol intact. Review all
uses before annotating a shared type; default values may be intentional. Do not
change external enums merely to demonstrate an attribute.

Verify a valid member, an invalid integer, the new connection feedback, and any
initialization repair included in the video. These analyzers identify supported
source patterns; they do not validate arbitrary constructor arguments or prevent
Unity/native code from producing default values at runtime.

See [exhaustiveness](../exhaustiveness.md), [SPIRE001](../rules/SPIRE001.md), and
the existing [showcase algorithms](../../examples/UnityShowcase/Assets/Scripts/ShowcaseAlgorithms.cs).
