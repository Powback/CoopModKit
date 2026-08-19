# CoopModKit

Base kit for building native-feeling local co-op mods for single-player games.
Born from HKCouchCoop (Hollow Knight, Unity/Mono).

- `docs/playbook.md` — the method: feasibility checklist, the patterns
  (clone-the-player, singleton masquerade, scoped field swap, session rules),
  engineering discipline, per-engine notes (Unity/Mono, IL2CPP, Unreal).
- `templates/` — csproj + NuGet.config for BepInEx/HarmonyX builds against
  non-redistributable game refs.
- `scripts/` — containerized decompile + build (no host SDK needed).

Shared *code* library is deliberately deferred until a second game proves the
abstractions (see playbook, last section). Reference implementation:
`~/Projects/HollowKnightMPMod`.
