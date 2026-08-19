# The Generic-Multiplayer Question

Recurring idea: "serialize the whole scene, delta-sync it, get native
multiplayer on any Unity game." Decomposition into the five real problems:

1. **Bandwidth** — solved (delta encoding, e.g. UFOSimEngine's). The easy 10%.
2. **What is the state?** Visible state (transforms/animators) snapshots fine.
   Behavioral state does not: private fields, mid-yield coroutines (compiler
   state machines w/ locals+closures), FSM internals, statics, RNG streams.
   You can replicate where an enemy is, not what it was thinking.
3. **Who simulates?** Host-authoritative replicas give the remote player
   round-trip latency on their own inputs; fixing that means client-side
   prediction + reconciliation, which is game-specific by nature. Core
   unsolved genericity problem of netcode.
4. **Identity** — instance IDs differ per process; needs deterministic spawn
   ID injection.
5. **Determinism** — RimWorld Multiplayer is NOT state sync; it is command
   lockstep over a sim its authors spent years making deterministic (seeded
   RNG streams per call site, UI/sim randomness separation, per-tick world
   hashes, resync machinery). Arbitrary Unity games are nondeterministic at
   the physics layer across CPUs; retrofitting determinism is harder than
   hand-syncing.

## What is real

- **GhostSync tier (future kit module):** HKMP-shaped model — each client owns
  its world, remote players are mirrored representations, curated entities are
  host-authoritative. Generic infra (~60%): deterministic spawn IDs,
  transform/animator delta streams, spawn/despawn replication, RPC bus,
  interest management. Per-game (~40%, always hand-built): the sync allowlist,
  enemy ownership, hit reconciliation. Build when a game demands online.
- **Available today at zero cost:** couch mods + Steam Remote Play Together =
  online multiplayer with no networking code. Ship couch first; RPT is the
  online story until GhostSync earns its existence.
