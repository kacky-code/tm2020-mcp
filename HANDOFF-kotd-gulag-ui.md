# Handoff: design the KOTD gulag screens by reading Nadeo's HUD

Status: **bridge side shipped and untested against a running client.** Created 2026-09-04.

You are picking this up on Windows, where Trackmania actually runs. Everything here needs a
live client, which is why it could not be finished on macOS.

## What this is for

KackyGG runs a Trackmania knockout mode, `KackyKOTD`, which is Nadeo's official Cup of the Day
knockout plus one addition we call the **gulag**: every N rounds the knocked-out players race
each other, the survivors are held out of the car, and the best knocked-out player gets their
place back.

The mechanic works. Verified on 2026-09-04 with a 12 player bot cup and again live with real
players. What is missing is the UI for it.

## What the players asked for

From the dev server on 2026-09-04, verbatim:

> **Fitz:** we still want the actual eliminated.. but also one showing after gulag round
> **Damel:** wdym, u dont eliminate on gulags
> **Fitz:** we need to change the UI to switch between two screens. players eliminated. and
> players in gulag. and then after gulag ended, we should show the players that got back in
> **Damel:** yy, which at least the ones that got in can be fully custom?

Three screens between rounds:

1. **Eliminated this round.** Already exists, stock Nadeo. Keep.
2. **Players in the gulag**, shown before a revival round. Missing.
3. **Players who got back in**, shown after it resolves. Missing, and the one worth real design
   effort. It is the payoff moment of the whole mechanic.

Damel's question, answered: yes, a fully custom screen is available. `Layers::Create` /
`Attach` / `Update` is standard, 774 uses across the ~1,900 script reference mirror in
`refs/` of this repo.

## The blocking question

The stock roll-call is Nadeo's `KnockedOutPlayers` UI module, driven from
`kacky-infra-ansible/gamemodes/KackyKOTD.Script.txt`:

```
UIModules_KnockedOutPlayers::DisplayContent(True);
UIModules_KnockedOutPlayers::DisplayEliminatedPlayer(accountIds, ranks);
MB_Sleep(computed from count, 4 players per page);
UIModules_KnockedOutPlayers::DisplayEliminatedPlayer([], []);
UIModules_KnockedOutPlayers::DisplayContent(False);
```

It takes an **arbitrary** list of account IDs and ranks. It is not hard-wired to eliminated
players, that is only what we pass it. So screens 2 and 3 could be that same block called again
with a different list, which would give us native paging, timing and styling for free.

**Unless the module renders a fixed header.** If it draws "KNOCKED OUT" in baked-in text, then
showing revived players under it reads exactly wrong and no amount of passing the right list
fixes it.

The module's source is unpublished, it ships inside the title pack. So this cannot be read from
source, only from what it renders.

**That is the question to answer: is the roll-call header fixed text, or driven by a variable
we can set?**

## What was built for you

`GET /layers` and `GET /layers/{index}` on the OpenPlanet bridge, plus MCP tools
`list_ui_layers` and `get_ui_layer_xml`. `CGameUILayer.ManialinkPageUtf8` is the XML a UI module
produces, live, which is the half of an unpublished module that matters.

Committed as `6db596b`. `dotnet build` clean, 135 tests pass.

**The AngelScript half has never run.** `dotnet` cannot validate it. If the plugin fails to load
after you copy it, that is the first suspect, and the change is confined to
`openplanet-plugin/TM2020Bridge/Main.as`: two routes near the `/manialink/events` route, and
four helper functions above `JsonEscape`.

## Do this

### 1. Install the updated bridge plugin

```powershell
$Repo = "<your clone of tm2020-mcp>"
$PluginDir = "$env:USERPROFILE\OpenplanetNext\Plugins"
Remove-Item -Recurse -Force "$PluginDir\TM2020Bridge" -ErrorAction SilentlyContinue
Copy-Item -Recurse "$Repo\openplanet-plugin\TM2020Bridge" "$PluginDir\TM2020Bridge"
```

Reload plugins in the Openplanet overlay. Check the Openplanet log for load errors before
going further.

### 2. Join the Kacky dev server

```
trackmania://#join=mU4GAOhbTJmJBTfzqIqSRg
```

That is `157.180.121.206`, server2, running KackyKOTD. HUD layers only exist in a playground:
in the menus the endpoint returns `"connected": false`, which is deliberately distinct from an
empty list.

If the join link has expired, the server is reachable over SSH as root with
`~/.ssh/kacky-deployment`, container `trackmania-2`.

### 3. Confirm the endpoint answers

```powershell
curl.exe -4 -sS --max-time 3 http://127.0.0.1:29100/layers
```

Expect `"connected":true` and roughly 29 layers. `attachId` is `Unassigned` on every one of them
in TM2020, so identify layers by the `tag` field, which is the `<manialink>` opening tag and
carries the id.

### 4. Capture the roll-call while it is on screen

This is the fiddly part: the roll-call only displays for a few seconds between rounds, and layer
indexes shift as layers come and go. Options, in order of preference:

- Sit in a cup until a round ends, then `list_ui_layers` and immediately `get_ui_layer_xml` on
  the layer whose tag looks like the knockout roll-call.
- Or dump every layer's XML once, in any state, and find the module by searching the XML for its
  labels. The layer may exist while hidden, which is enough to read its structure.

A revival round is the best moment: `S_GulagEveryNRounds` is 3, so rounds 3, 6, 9 are revival
rounds when more than `S_GulagStopBelowAlive` players are alive.

### 5. Answer the question and write it down

Look for whether the header text is a literal in the XML, or a `<label>` bound to a netwrite
variable the server sets.

- **Driven by a variable** → screens 2 and 3 reuse the stock module. Cheap, native, done in a
  few lines in `Match_EndRound`.
- **Fixed literal** → screens 2 and 3 need our own layer via `Layers::Create`. Use the captured
  XML as the template so ours matches Nadeo's fonts, sizes, colours and animation.

Either way, save the captured XML under `examples/` in this repo and record the finding in
`docs/openplanet/`, per this repo's rules on curated, source-linked notes.

## Then build the screens

Mode file: `kacky-infra-ansible/gamemodes/KackyKOTD.Script.txt`. The gulag code is marked, and
`docs/adr/0001-kotd-is-cotd-plus-gulag.md` records why the mode is stock-plus-gulag and nothing
else. Keep it that way: every departure from stock has cost us a bug.

Insertion points, both in `Match_EndRound`:

- **Gulag roster**, before a revival round runs. The racers are the knocked-out players;
  `RoundRacersCount(True)` counts them and `KnockedOutCount()` is the ledger figure.
- **Back in**, right after `RevivePlayers(S_GulagRevivals)` returns, which hands you the account
  IDs of everyone revived.

Test without humans. The bot harness drives real KOTD cups now:

```
cd <KackyGG>
docker compose -f compose.dev.yml -f .kott-harness/compose.override.yml --profile tm2020 up -d trackmania
docker logs -f kacky-dev-trackmania-1 | Select-String "KOTD"
```

12 bots on a straight map, a full cup in a couple of minutes, four revival rounds. Set
`TM_GAME_SETTINGS` to `kotdbots.txt` in `.kott-harness/compose.override.yml`.

Deploy to the dev server with no restart and nobody dropped:

```
cd <kacky-infra-ansible>
./gamemodes/reload-mode.sh 157.180.121.206 2 gamemodes/KackyKOTD.Script.txt
```

**A reload resets every mode setting to its default.** Re-apply them afterwards or the gulag
silently reverts to stock cadence. `dev-server/Maps/MatchSettings/kott-playtest.txt` in KackyGG
holds the intended values.

## Two traps that cost hours on 2026-09-04

**Do not style a stock UI element by hand.** `BigMessage` is a built-in the game renders. Passing
it text looks native. Adding formatting codes, caps and colours made it look foreign next to the
stock lines, and it had to be reverted. If you are styling, build your own layer; if you are
using Nadeo's, give it text only.

**Do not infer a Nadeo module's behaviour from its call sites.** That is exactly what this
handoff exists to stop. Read the XML.

## Repo rules that apply

`AGENTS.md` governs this repo. The ones that will bite:

- TDD for non-trivial logic, and `dotnet build` plus `dotnet test --no-build` before pushing.
- TM2020 ManiaLink is not the TMF or Maniaplanet dialect. Prefer `docs/manialink-tm2020.md` and
  `examples/` over any general ManiaLink reference found on the web, and never paste TMF snippets.
- Keep bridge endpoints localhost only, responses JSON unless explicitly XML.
- Update README and docs when endpoints or tools change.
