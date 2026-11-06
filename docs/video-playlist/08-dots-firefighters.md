# 8. State-specific data in a DOTS firefighting simulation

Use [FirefightersSpire](../../examples/FirefightersSpire/README.md), adapted from
Unity's Entities 101 tutorial. The existing simulation provides a visible reason
for different states: bots fetch a bucket, fill it, pass it along a line, and
empty it over a fire.

The original Bot combines an enum state with a shared TargetPos field. The
sibling moves state-specific destinations into MoveToBucket and DouseFire
payloads. Shared line/team/bucket data remains ordinary ECS data.

1. Run the completed firefighting scene and identify the bot roles and transitions.
2. Trace the original enum and destination field through LineSystem and BotSystem.
3. Inspect the generated union's states and which ones carry a destination.
4. Show factories constructing transitions and the exhaustive system handler.
5. Inspect the actual unmanaged Bot component and the union's native storage.
6. Run transition checks in real Worlds, including ownership and heat/team effects.
7. Demonstrate native Burst execution rather than relying on a managed fallback.

Explain the intended fit: state-specific data inside an existing component,
without managed virtual dispatch or a structural component change per transition.
Do not imply that a union replaces ECS queries or always beats separate systems
and enableable components.

Show the storage tradeoff too: the sample retains the persistent fire target and
adds a destination inside the active-state payload. It improves the API's state
boundaries rather than reducing this component's size.

The runtime union deliberately has no Serializable attribute. If designers need
to author the same alternatives, manually declare a serializable authoring union
and convert or bake it. The [integration report](../reviews/2026-10-27-spire-demo-integration.md)
records the selected Unity version and executed checks.
