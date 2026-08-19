# Couch Co-op Mod Playbook

The method that turned Hollow Knight into native-feeling local co-op, written
to be repeated on other games. Engine-specific mechanics live in per-engine
sections; the investigation and the patterns are universal.

## Phase 0 — Feasibility (before writing any code)

Decompile (Unity/Mono: `scripts/decompile.sh`) and answer, with line numbers:

1. **How does the player entity read input?** Instance field → jackpot: a
   cloned player with its own input source inherits the whole vanilla moveset.
   Global/static reads → every input site needs routing; cost goes up 10x.
2. **Where are the singleton identity gates?** Grep the player class for
   `instance` comparisons. Few gates (HK had 2 in 5,700 lines) → clone
   architecture works. Gates inside the per-frame loop → puppet architecture
   (HKMP-style visual shells) instead.
3. **What state is shared vs must be per-player?** Health/resources per
   player; progression/wallet/loadout shared (one save = one team). List where
   each is READ — instance methods are scopeable, arbitrary readers (FSMs,
   coroutines, UI) are not.
4. **How is input hardware modeled?** Need per-device claim (InControl:
   `PlayerActionSet.Device`; Rewired: per-Player controller assignment; Unity
   Input System: `InputUser.PerformPairingWithDevice`). Keyboard usually can't
   be split — plan pads-only for joiners.
5. **What does the camera assume?** Find the follow target and any lock-zone /
   bounds system; you will need to stand down inside scripted camera regions.

## The patterns

- **Clone-the-player**: instantiate the real player entity; it runs vanilla
  code. Never build a puppet if the clone works — every reimplementation is a
  future desync.
- **Singleton masquerade**: point `Instance` at the clone strictly for the
  duration of a call that gates on identity; restore in finally/Finalizer so
  an exception can never leave the real player dethroned.
- **Scoped field swap**: per-player pools (health, mana) live in your struct;
  swap them into the shared save object around the player's own state-touching
  methods, write results back after. All vanilla math (charms, modifiers) runs
  against the right numbers without reimplementation.
- **Attribution masquerade**: when the game hardwires credit to "the player"
  (soul/xp on hit), resolve the true actor from the hit source and masquerade
  for that call.
- **Session dissolution rules**: define exactly what ends/suspends co-op —
  menu scenes (never let clones cross into another save), P1 death (P1 is the
  save file), device unplug, scripted control loss (freeze extras with the
  same switch the game uses on P1).
- **Native UI or no UI**: clone the game's own widgets (strip their drivers,
  drive them yourself); if the layout can't be found, show NOTHING and log a
  hierarchy dump. Custom-styled overlays read as hacks.
- **Downed states from the game's own grammar**: prefer the game's real death
  actors (HK: the actual Shade prefab + its HealthManager.OnDeath) over
  invented ghosts. Guard any save-adjacent side effects with
  neutralize-then-restore, and force-restore on EVERY teardown path — a save
  during the window must never persist neutralized values.

## Engineering discipline that paid off

- Version-gate on the exact game build; refuse loudly on mismatch.
- Patch class-by-class with per-class try/catch: a game update costs one
  feature, not the mod.
- Every masquerade/swap restores via Finalizer, never Postfix (skipped on
  exceptions).
- Throttle error logging in per-frame loops.
- Back up the save before the first run of any death/save-adjacent feature.
- Verify via log watcher: filtered tail of the mod loader's log during play
  sessions is the primary test harness.
- refs/ and decomp/ are gitignored and never shipped; users build against
  their own copy of the game.

## Per-engine notes

### Unity / Mono (BepInEx 5 + HarmonyX) — this kit's templates target this
Decompile with ilspycmd (pinned version in scripts/), reference game DLLs
Private=false, publish one plugin DLL. Proton needs
`WINEDLLOVERRIDES="winhttp=n,b" %command%` or the prefix-registry equivalent.

### Unity / IL2CPP
Same patterns, different plumbing: BepInEx IL2CPP + Il2CppInterop, dump
assemblies with Il2CppDumper. Reflection is proxy-based; field swaps go
through generated wrappers. Budget extra time.

### Unreal
Different playbook, often much shorter: UE ships native local multiplayer.
First attempt is always `CreatePlayer` (UE4SS console/Lua) + possess a pawn —
some games give split-screen nearly free. Only when the game's own Blueprint
logic assumes one player do you need the masquerade equivalents (UE4SS Lua
hooks / C++). No shared code with Unity — shared method only.

## When to extract the shared library
At game #2, not before. Candidates proven by HK: player registry/lifecycle,
input claiming, masquerade+swap helpers, group camera with lock standdown,
join-with-Start pause interception. Wrong abstractions enshrined from one
data point cost more than copy-paste.
