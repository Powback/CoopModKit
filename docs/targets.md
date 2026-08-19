# Co-op Target Dossier

Games without official co-op where couch co-op would be outstanding, ranked by
demand × feasibility with this kit's playbook. Engine claims verified where
marked; the rest are best-knowledge and get confirmed in each game's Phase 0.

## Tier S — Unity/Mono, playbook applies nearly verbatim

| Game | Why outstanding | Route |
|---|---|---|
| **Hollow Knight: Silksong** | The obvious #2: same studio DNA, same Unity 6 + BepInEx stack as HK (verified — mod scene already active on it), massive demand. Our HK anatomy knowledge transfers almost directly. | Clone-the-player, this kit |
| **Nine Sols** | Sekiro-like 2D metroidvania, HK-shaped camera and player model | Clone-the-player |
| **Tunic** | Isometric Zelda-like, perfect shared-screen game | Clone-the-player |
| **Death's Door** | Isometric action, already feels co-op-shaped | Clone-the-player |
| **Blasphemous 1/2** | 2D metroidvania, HK-adjacent architecture likely | Clone-the-player |

## Tier A — Unity/IL2CPP (same patterns, heavier plumbing)

| Game | Notes |
|---|---|
| **Ori and the Will of the Wisps** | Dream couch platformer; Moon's Unity is heavily customized — Phase 0 will say how heavily |

## Tier B — Unreal (try `CreatePlayer` before anything else)

UE ships native local-multiplayer machinery; attempt #1 is always UE4SS +
`CreatePlayer` + possess a pawn. The risk is never rendering — it's the game's
own quest/UI/camera blueprints assuming one player.

| Game | Why outstanding |
|---|---|
| **Hogwarts Legacy** (UE4, verified) | "Co-op Hogwarts" is one of the most-wished mods in existence; UE4SS is mature on it; HogWarp (online MP project) proves the community appetite and provides prior art |
| **Sifu** | Brawler — the genre couch co-op was born for |
| **Stray** | Two cats. Instant sell |
| **Little Nightmares I/II** | II already has an AI companion — co-op begs to exist |
| **Ender Lilies / Ender Magnolia** | 2D metroidvania on UE, HK-adjacent feel |
| **Jedi: Fallen Order / Survivor** | High demand, big-budget UE4 |
| **Lies of P** | Soulslike co-op demand is bottomless |

## Tier C — proprietary engines (research projects, not kit consumers)

| Game | Reality check |
|---|---|
| **Marvel's Spider-Man Remastered / 2** | Insomniac's proprietary engine; modding is model/suit-level today. Gameplay co-op = native reverse engineering from scratch. Highest wow-factor, highest cost |
| **Fallout 4 / Skyrim** | Creation Engine is single-player to the bone; the honest route is studying Skyrim Together Reborn's architecture (online sync, not couch). Major undertaking with prior art |
| **Hades** | Custom engine + Lua scripting — moddable but a different playbook entirely |

## Already solved elsewhere (don't build)

Elden Ring (Seamless Co-op mod), GTA V (FiveM + split-screen mods), Cult of
the Lamb (official local co-op added), Cuphead/Stardew/Terraria (official).

## Recommendation

1. **Silksong** proves the kit (game #2, maximal reuse) once HK is play-stable.
2. One **UE title** (Stray or Little Nightmares — small scope) validates the
   `CreatePlayer` playbook cheaply before attempting Hogwarts Legacy.
3. Tier C only as long-horizon passion projects.
