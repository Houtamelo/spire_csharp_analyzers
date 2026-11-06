# Firefighters shutdown allocation warning

Unity 6000.5.6f1 on Linux x64 reports this warning after the Firefighters Step 4
Play Mode smoke check completes:

```text
Leak Detected : Persistent allocates 3 individual allocations.
```

The scene creates actual bot entities, the check reports success, and Unity exits
with status zero. Native Burst and real-World transition checks also pass.
Evidence: `tmp/spire-demo-integration/dots-playmode-3.log` and the sibling's
`SpirePlayModeResults.json`. This is a warning, not a clean allocation audit.

The allocation sites have not been isolated. The authored Firefighters scripts
contain no explicit Persistent allocations; that alone does not identify the
owner or establish whether this was present in the original project. There is
no evidence attributing the warning to Spire's unmanaged BotState union.

Before making allocation claims, enable native leak stack traces, repeat the
same bootstrap/shutdown against the original pinned Firefighters sample, and
compare the allocation sites. Keep that investigation separate from the passing
state-transition and native Burst checks.
