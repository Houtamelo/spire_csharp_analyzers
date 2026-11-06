# Boss Room AI union

Replace the connection-feedback union with the existing per-enemy AI state
hierarchy. The feedback union introduced a mapping layer over two already
separate UI callbacks; those callbacks return to their original implementation.

Committed as `27c0374` on BossRoomSpire's `with-spire` branch. Its `upstream` and
`without-spire` refs are unchanged. Review the AI conversion directly:

```sh
git -C examples/BossRoomSpire diff without-spire..with-spire -- Assets/Scripts/Gameplay/GameplayObjects/Character/AI
```

The original AI uses an `AIStateType` enum, a dictionary of behavior objects,
and abstract `AIState` methods implemented by `IdleAIState` and `AttackAIState`.
`ServerCharacter.Update` calls the brain while an enemy is alive and enabled.
Only the attack state owns a target, selected action, and configured action list.

Use `AIState.Idle()` and `AIState.Attack(foe, action, actions)`. Store the union
directly in `AIBrain`, replacing the enum, dictionary, and separate state objects.
An exhaustive switch dispatches updates. Keep idle scanning and attack logic in
separate partial `AIBrain` files. Deconstruct the readonly attack payload into
locals and save the updated values back into the union; the action list is retained
across ordinary attack ticks.

Preserve attack priority, delayed acquisition after an idle scan, initialization
on entry, nearest-target selection, retarget cancellation, chase/attack/stun
guards, and cooldown filtering. Losing the last live target clears actions;
the following tick returns to idle, as before. Do not serialize this runtime
state or change network payloads and Unity prefab contracts. Keep the existing
disconnect-reason parser fix and enum checks independently of the AI example.

Validation must exercise the actual brain, character assets/components, and
action player, covering both target acquisition and rejection, state persistence,
retargeting/loss, and action availability. Constructor or enum-shape checks alone
do not establish equivalent gameplay. Native execution uses the isolated audio
backends and Xvfb. Record results after running the checks.

## Validation

Unity 6000.5.6f1 Linux Editor Mono compiled the refactor. Native checks pass
**11/11 groups**: ten AI groups plus the existing connection numeric/wire-format
regression. The real Startup scene smoke check passes **1/1**. Source files were
formatted with scoped `dotnet format whitespace` calls.

The fixture uses real Imp/player prefabs, CharacterClass and action assets,
AIBrain, ServerActionPlayer, and ActionFactory. A cloned character configuration
uses the original MageBaseAttack skill with its real cooldown to exercise public
Idle-to-Attack updates safely in Edit Mode. Tests verify range and hatred, target
invalidation/retargeting, return to Idle, action-list identity, and absent skills.
Action-guard checks seed actual pooled chase, attack, and stun action data into
the real queue, then assert that the queue's count and head identity are retained.
A wrong-target chase must fall through to cooldown selection.

This covers the moved AI decisions and state persistence. It does not run combat
RPCs, a networked NPC encounter, player builds, or a performance benchmark. Seeded
guard actions do not execute OnStart; snapshots of static state and action IDs
are restored, and temporary objects/pools are cleaned up. No shared source assets
or project audio settings are retained from the checks.

Evidence is under `tmp/spire-demo-integration/`: the final
`boss-ai-validation-verified.log`, `boss-ai-playmode.log`, and the project's
SpireExampleResults.json and SpirePlayModeResults.json reports.
