# Prior Art: SilklessCoop (Silksong, nek5s)

Source read 2026-08-19 (github.com/nek5s/SilklessCoop, ~2k lines).
Companion repos: SilklessLib (transport), Echoserver (standalone relay).

## Architecture: the ghost model, executed cleanly

Pure visual sync — each player runs their own full game and save; remote
players are render-shells (GameObject + tk2dSprite + tk2dSpriteAnimator)
driven by two packet types: position {scene, x, y, scaleX, vx, vy} and
animation {crestName, clipName}. No gameplay interaction, no shared world,
enemies unsynced. The polar opposite of our one-world clone model — and the
complementary half: their strengths (online, cross-scene freedom, zero save
risk, tiny code) are exactly our model's gaps, and vice versa (they cannot
have real co-op interaction; we can).

## Techniques worth stealing

1. **Staleness fade + dead reckoning** (SimpleInterpolator): ghosts advance by
   last-known velocity each frame and fade opacity toward zero until the next
   packet refreshes both. Elegant packet-loss UX in ~25 lines. (Also the
   correct render-side answer to "distant entities jump between positions" in
   any tick-replicated view.)
2. **Typed packet bus**: `SilklessAPI.AddHandler<TPacket>(fn)` — transport-
   agnostic (SteamP2P or echo server behind it). The right shape for a future
   GhostSync net API.
3. **Modular Sync components**: each concern (player visual, map compass,
   player count, audio) is a self-contained component with
   OnEnable/OnDisable/OnPlayerJoin/OnPlayerLeave. Clean extension seam.
4. **tk2d sprite collection caching by GUID** (Resources.FindObjectsOfTypeAll
   at startup): how to render a remote player's animations across spritesheet
   switches (Silksong crests). Directly reusable for any tk2d ghost rendering.
5. **Scene gating**: ghost SetActive(packet.Scene == activeScene) — in the
   ghost model, "different areas simultaneously" is trivially "you don't see
   them right now".
6. Lazy, self-healing hero discovery (GameObject.Find each Update until
   cached) — crude, resilient, works across scene reloads.

## Strategic implication

For Silksong as game #2, the endgame combination is: our clone model locally
(couch, real interaction) + their ghost model remotely (online friends as
ghosts via SilklessLib-style transport). The two models compose — a couch
pair could host ghosts, ghosts could watch a couch pair.
