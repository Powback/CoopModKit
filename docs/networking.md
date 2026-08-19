# The Generic-Multiplayer Question

Recurring idea: "serialize the whole scene, delta-sync it, get native
multiplayer on any Unity game." Decomposition into the five real problems:

1. **Bandwidth** — solved. UFOSimEngine's actual mechanism (read, not assumed:
   crates/net/delta.rs): causal delta batches — per-tick serialization of
   changed Causal components only, `(PersistentId u128, ComponentId,
   varint-len, bytes)`, with ReplicationClass labels deciding what replicates,
   identity.rs (persistent IDs), interest.rs (scoping), hash.rs (per-tick
   desync tripwire). The easy 10% — but see the note at the bottom: those
   crates are liftable as the GhostSync transport layer.
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

## The thin-client refinement (changes the verdict)

Host simulates EVERYTHING; clients forward input events and render a mirrored
scene from replicated *visible* state (transforms, animator states,
spawn/despawn, VFX/audio events — delta-encoded). This kills problems 3 and 5
(one simulation, no determinism needed) and reduces 2 to the snapshottable
subset. Latency = input RTT: negligible on LAN, cloud-gaming-feel over WAN.

Key asset: **the host side already exists — it is the couch mod.** Remote
players are clone-players driven by a virtual input device fed from the
network; the input layer does not care where button states come from.

Remaining hard parts, in order:
1. Puppet-izing the client world: run the game for assets/rendering, suppress
   all simulation (gameplay scripts, physics, FSMs) while keeping renderers,
   particles, audio, and the client's OWN live camera. HKMP's render-shell
   trick generalized from remote players to the whole scene. This is the
   project.
2. Event taps for one-shot VFX/audio (spawn hooks catch most).
3. Still single-scene: clients get independent cameras/screens (which Remote
   Play Together cannot do), but everyone inhabits the host's one loaded
   scene. Different map areas simultaneously stays blocked.

Versus Remote Play Together: wins are per-client cameras, bandwidth, render
quality; cost is months. Pipeline: couch mod → RPT for online → thin-client
GhostSync when per-client cameras justify it.

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

## UFOSimEngine, read properly

It achieves "sync anything seamlessly" because five properties are designed
in — replication classes on every component, persistent identity, bit-
deterministic sim with seeded RNG + state-hash verification, implicit-until-
observed state (most of the world has nothing to sync), interest management.
Those are the five problems above, solved at design time. It is the existence
proof that the dream works when you own the engine — and of why it cannot be
retrofitted onto a shipped Unity game (unlabeled components, unstable IDs,
nondeterministic sim, all state explicit).

Concrete reuse for thin-client GhostSync: lift the net crates as the
transport/wire substrate (delta-batch format, identity map, interest, hash
tripwire, WS/TCP/STDB transports) — a Rust relay speaking that format, the
Unity mod acting as emitter/applier of its curated visible-state set. The
per-game semantic layer stays hand-built.
