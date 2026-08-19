# CoopModKit

Base kit for building native-feeling local co-op mods for single-player games.
Born from HKCouchCoop (Hollow Knight, Unity/Mono).

- `docs/playbook.md` — the method: feasibility checklist, the patterns
  (clone-the-player, singleton masquerade, scoped field swap, session rules),
  engineering discipline, per-engine notes (Unity/Mono, IL2CPP, Unreal).
- `src/CoopKit/` — the proven library (netstandard2.0, HarmonyX-only dep):
  `Masquerade` (identity impersonation, Finalizer-safe), `PoolSwap`
  (per-player resources over shared state, reentrancy-aware), `Harness`
  (patch isolation, version gate, throttled log), `PacketBus` +
  `DeadReckoning` (ghost-tier primitives distilled from SilklessCoop).
  Tests: `docker compose run --rm test`.
- `templates/` — csproj + NuGet.config for BepInEx/HarmonyX builds against
  non-redistributable game refs.
- `scripts/` — containerized decompile + build (no host SDK needed).

Shared *code* library is deliberately deferred until a second game proves the
abstractions (see playbook, last section). Reference implementation:
`~/Projects/HollowKnightMPMod`.
