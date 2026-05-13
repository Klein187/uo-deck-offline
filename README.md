# UO Deck Offline

One-stop installer for an offline Ultima Online experience on a Steam Deck — server, client, and a roster of wandering player-bots — all on your own machine, all single-player. Choose **Pre-T2A**, **T2A**, or **both side-by-side** at install time.

This repository **does not contain Ultima Online client files.** You must supply those yourself from a legitimate UO install (any retail or trial install with the `.mul` / `.uop` data files will do). The installer copies them into a private working directory; nothing leaves your Deck.

## Pick your era

| Option | Expansion | Description |
|---|---|---|
| **Pre-T2A** | `None` | Britannia only — no Lost Lands, no T2A monsters. Closest to UO's September 1997 launch. |
| **T2A** | `T2A` | Adds the Lost Lands, ophidians, terathans, T2A dungeon bosses. June 1998 era. |
| **Both** | both | Side-by-side install. Pick at launch. Independent characters and saves. |

You're prompted at install time. "Both" roughly doubles disk footprint (~6 GB total instead of ~4 GB) and gives each server its own port (`2593` for pre-T2A, `2594` for T2A) so they don't collide.

## What's in the box

- **ServUO Publish 57.4.1**, pinned, configured with the chosen expansion flag.
- **ClassicUO** native Linux client, pre-configured, update checks disabled.
- **PlayerBot** scripts: **~300 NPC "players"** populating the world:
  - **Skill tiers** — every bot rolls one of five tiers at spawn: Novice, Apprentice, Adept, Expert, Grandmaster. Bell-curve distributed (10/22/36/22/10%) so most bots are mid-tier but you'll occasionally find a GM. Tier affects skill ranges, equipment quality, pack contents, and loot. A GM miner carries way more ore including rare colored types; a GM tamer usually has a dragon; a GM mage has a complete spellbook; a GM PK drops magic loot. A novice in any role has basic gear and small drops.
  - **Combat bots** (warriors, mages, archers, healers) — wander and fight monsters, accept follow/dismiss orders. ~10% of the combat slot is given to a **combat tamer** instead, who wanders with a tier-appropriate fighting pet (wolves and bears at low tiers, dragons and wyrms at GM).
  - **Civilian bots** (crafters, miners, fishermen, lumberjacks, peaceful tamers) — each role gets a *random count per reseed*. They hang at role-appropriate locations and perform ambient activities. Miners 80% of the time have a pack mule with mixed ore (composition scales with miner's tier — GMs have a chance of valorite, novices only iron).
  - **AFK macroers** — bank-only by default: stand at a city bank looping a skill macro (hiding, magery, healing, meditation, anatomy, musicianship, or taming). Tamer macroers spawn wild creatures next to themselves and "tame" them on a cadence.
  - **Player killers** — red names, fully random gear and class, skill tier affects damage, HP, and loot quality. GM PKs are genuinely dangerous and drop real loot.
  - Civilian respawn keeps the world from emptying out when PKs do their work.
  - Bots throttle their AI when no real player is nearby so the Deck doesn't melt.
- A **launcher** that picks an expansion (if multiple are installed), boots the matching server, waits for it, launches the client, and tears the server down cleanly on exit.
- A **`.desktop`** entry you can add to Steam as a non-Steam game.

## Offline guarantee

After `install.sh` completes successfully, **everything needed to play is on disk**.

- .NET 8 installed locally to `~/uo-offline/dotnet` (no system-wide install, no sudo).
- ServUO at a pinned commit, NuGet packages pre-restored to `~/uo-offline/nuget-cache`, built with `--no-restore` so future rebuilds don't need the network.
- ClassicUO configured with `"check_updates": false`.
- Phone-home URLs stripped from `Server.cfg`.
- Server bound to `127.0.0.1` only — no LAN, no outbound.

Run `~/uo-offline/verify-offline.sh` to confirm. Then disable Wi-Fi and you should be playing fully offline.

## Requirements

- A Steam Deck (or any Linux box) with ~4 GB free for one expansion, ~6 GB for both.
- An existing UO client install you can point the installer at.
- An internet connection during install (for ServUO, ClassicUO, and .NET 8).
- `curl`, `git`, `unzip`, `rsync` on PATH. SteamOS has all of these by default.

## Install

In desktop mode, open Konsole:

```bash
git clone https://github.com/Klein187/uo-deck-offline.git
cd uo-deck-offline
./install.sh
```

The installer will ask which version(s) you want, find your UO data files, and do the rest. Total time: ~5 minutes for one expansion, ~8 for both.

## Play

**Desktop mode:** launch "UO Offline" from your app menu.

**Gaming Mode:** add `~/uo-offline/start-uo.sh` as a non-Steam game.

If you installed only one expansion, it launches directly. If you installed both, you'll get a quick numbered prompt asking which to play. To skip the prompt, pass `--expansion=pretoa` or `--expansion=t2a`:

```bash
~/uo-offline/start-uo.sh --expansion=t2a
```

When you close ClassicUO, the matching server saves and shuts down.

## PlayerBot commands

In-game, as the admin:

| Command | Access | Description |
|---|---|---|
| `[addbot warrior` / `mage` / `archer` / `healer` | GM | Spawn a combat bot |
| `[addbot crafter` / `miner` / `fisherman` / `lumberjack` / `tamercivilian` | GM | Spawn a civilian |
| `[addbot tamercombat` | GM | Spawn an adventuring tamer with a combat pet |
| `[addbot macroer` | GM | Spawn an AFK macroer (picks a random skill to "train") |
| `[addbot pk` | GM | Spawn a solo PK (use multiple times for a gang) |
| `[removebot` | GM | Remove nearest PlayerBot within 20 tiles |
| `[botcount` | GM | Total + breakdown by category |
| `[botstats` | GM | Distribution by role and region |
| `[botbudget <n>` | Admin | Set total target population |
| `[pkmode players` / `bots` / `both` | Admin | Change what PKs hunt (live, no reseed needed) |
| `[pkdensity <0-50>` | Admin | Set PKs as percent of population (needs `[reseedbots`) |
| `[reseedbots` | Admin | Wipe and reseed to current target |
| `[clearbots` | Admin | Remove all PlayerBots without reseeding |

### Speech triggers (combat bots only)

- `<botname> follow me` — they follow you.
- `<botname> stop` — halt.
- `<botname> dismiss` — back to wandering.

Civilians and macroers don't take orders. Macroers don't respond to speech at all — they're "AFK," that's the joke.

### Notes on the population

- First boot takes 30-90 seconds while seeding. You'll see progress in the server log.
- Default mix per 300 bots: ~200 combat, ~75 civilian (random per-role weights so the mix varies — could be 30 miners and 8 fishermen one reseed, the reverse the next), ~15 PKs split between dungeon campers and wilderness wanderers. Macroers are ~20% of civilians (~15 bots) and concentrate at banks.
- Civilians spawn at role-appropriate locations 75% of the time. Macroers prefer banks (80%) with the rest in wilderness as bait. PKs camp dungeon entrances based on a per-dungeon roll (~55% chance each), with the rest wandering the wilderness on rotating waypoints.
- Civilian respawn runs every 10 minutes — bots killed by PKs are gradually replaced.
- All bots throttle their AI when no real player is nearby (PKs excluded — they hunt regardless).
- Settings file at `~/uo-offline/servers/<exp>/Scripts/Custom/PlayerBots/Settings.txt` controls everything. Edit + `[reseedbots` to apply.
- Delete `~/uo-offline/servers/<exp>/Saves/playerbots_seeded.flag` to force a re-seed on next boot.

## File layout after install

```
~/uo-offline/
├── dotnet/                   # user-local .NET 8
├── nuget-cache/              # shared NuGet packages (for offline rebuilds)
├── uodata/                   # shared UO data files (lowercased)
├── ClassicUO/                # shared client
├── servers/
│   ├── pretoa/               # only present if pre-T2A was selected
│   │   └── ServUO source + Saves/ + Config/
│   └── t2a/                  # only present if T2A was selected
│       └── ServUO source + Saves/ + Config/
├── installed.list            # manifest read by the launcher
├── start-uo.sh
├── stop-uo.sh
├── verify-offline.sh
└── install.log
```

## Uninstall

```bash
./uninstall.sh
```

Removes `~/uo-offline/` and the `.desktop` entry. Doesn't touch your original UO client install.

## Troubleshooting

**Launcher picker doesn't appear** — only one expansion is installed. Pass `--expansion=...` to be explicit, or check `~/uo-offline/installed.list`.

**Bots spawned in T2A but I want them in pre-T2A locations only** — delete `~/uo-offline/servers/<exp>/Saves/playerbots_seeded.flag` and restart that server.

**"Cannot locate built ServUO binary"** — build failed silently. Check `~/uo-offline/install.log`.

**"Server did not start within 60s"** — corrupted save. Stop the server, delete `~/uo-offline/servers/<exp>/Saves/`, restart (you lose characters).

**ClassicUO black screen** — `clientversion` in the launcher's generated `settings.json` doesn't match your data files. Edit `start-uo.sh` and change the `clientversion` value (try `7.0.45.65` or your actual client version).

## Legal

Ultima Online and its data files are © Electronic Arts. This project does not include or distribute any EA-owned content. You must supply your own client files. ServUO is a community emulator distributed separately under its own license; we pull it from upstream at install time.

## Credits

- [ServUO](https://github.com/ServUO/ServUO) — the server emulator.
- [ClassicUO](https://github.com/ClassicUO/ClassicUO) — the cross-platform client.
- PlayerBot scripts in `assets/playerbots/` are original to this repo, MIT-licensed.
