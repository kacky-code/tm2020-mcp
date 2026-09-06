# Driving a mode's own UI layers

How a server-side mode puts its own ManiaLink on screen, and the choices that turn out
to matter. Written after building the KOTD gulag screens and then reading a shipped mode
that had solved the same problem.

The evidence is `refs/Revive-KO` (pinned `4fa6c0c`), a knockout mode with a revive
mechanic that has run multiple streamed cups with real players. It is worth more than most
of the mirror because it is **TM2020-era and shipped**: much of `refs/` is Maniaplanet
2017-2019 code whose dialect no longer applies, which is why `docs/manialink-tm2020.md`
exists at all.

Our side of each comparison is `kacky-infra-ansible/gamemodes/KackyKOTD.Script.txt`.

## Two ways to drive a layer, and only one scales

`main()` in a ManiaLink page runs **once, when the page is created**. Everything below
follows from that.

Revive-KO bakes the data into the XML and re-creates the layer for every event. The player
list is string-interpolated into the page source at generation time and split apart inside
it:

```
// UI/Modules/ReviveWidget.Script.txt:81
declare Text[] RevivePlayers = TL::Split(",", "{{{replayers}}}");
```

with `Layers::Create` / `SetType` / `Attach` per showing and `Detach` to hide
(`UI/UI.Script.txt:181-193`). Given data baked into the source, re-creation is not a style
choice, it is the only way to make `main()` run again.

Ours creates the layer once at load and pushes netwrite variables at it
(`KackyKOTD.Script.txt:2542` and `:2555`), with a `Net_KOTT_Gulag_Update` counter the page
watches so identical pushes still redraw.

**Use the second one.** Re-creating pushes the entire page to every client on every event;
netvars push a few hundred bytes. At a 100 player cup with a screen every round that
difference is the whole cost of the feature. The counter is load-bearing: without it, two
identical pushes in the same millisecond are indistinguishable from no change.

## Getting a list into the layer

Three transports appear in the mirror, in increasing order of correctness:

1. **String-interpolate and `TL::Split`** — Revive-KO, above. Ties you to re-creating the
   layer, and breaks on any value containing the separator.
2. **`tojson()` / `fromjson()` through a `Text` netvar** — ours today.
3. **Array netvars.** `netwrite`/`netread` take arrays directly:

   ```
   // refs/Revive-KO/.../UI/Modules/ScoreTable.Script.txt:110
   declare netread Text[] Net_TS_ReviveQueue for Teams[i];
   ```

   Nadeo does the same, including the parallel-arrays shape we use for names and ranks:

   ```
   // refs/game-modes/Common/Scripts/Libs/Nadeo/ChannelProgression.Script.txt:1413-1414
   declare netwrite Text[] Net_LibChanPro_RankingNames for Teams[0];
   declare netwrite Integer[] Net_LibChanPro_RankingScores for Teams[0];
   ```

   Array netvars appear roughly 1,165 times across `refs/`, on `Teams[0]` and on `_Score`
   alike.

**Open item:** our gulag screens JSON-encode `Text[]` and `Integer[]` into `Text` netvars
and decode them in the page. Nothing found so far says that is necessary; dropping it
removes `tojson`/`fromjson` from both ends. Not changed yet, because that code is running
in live cups and the swap deserves its own test round rather than a drive-by.

## Per-entity state

`declare <Type> <Name> for <Entity>` attaches a variable to a player, a score, or the mode
itself. It is how both codebases carry per-thing state without a global map:

- `for Player` — per car, per round. Gone when the car leaves the map.
- `for Score` — per participant, survives respawn and spectate. Revive-KO keeps team
  number here; our KOTD keeps `Knockout_GulagShotSpent` here.
- `for This` — per mode. The only way a parent mode's state is reachable from a subclass,
  or from another label block.

Two rules that cost time when broken. A variable declared in one label block is **not**
visible from another, so shared state has to be declared in `Match_InitMap`; the compiler
says "The member or variable does not exist" and never mentions scope. And every
declaration site must repeat the type and default, so a typo in one site silently creates a
second variable instead of failing.

## Entrance and exit choreography

Queue both directions in one pass, staggered by index, and let the animation manager run
them. Revive-KO's row entrance (`UI/Modules/ReviveWidget.Script.txt:73-76`):

```
AnimMgr.Flush(frame);
AnimMgr.Add(frame, "<elem pos=\""^x^" 0\" />",   Now + 1500 + i*250, 2500, ElasticOut);
AnimMgr.Add(frame, "<elem pos=\""^x^" -21\" />", Now + 5500 + i*250, 2500, ElasticIn);
```

`Flush` first, or a re-show inherits the previous animation's queue. The `i*250` stagger is
what makes a list of names read as an event rather than as a table appearing.

The same trick per character gives a title reveal (`UI/Modules/Knockout.Script.txt:97-111`):
one `<label>` per letter, `Now + i*100` in, and `Now + 6500 + (8-i)*100` out so it unwinds
from the far end instead of mirroring itself.

This is the gap in our screens. Ours match Nadeo's `DisplayEliminations` timings for the
panel as a block; nothing staggers per name.

## Resolving a player from an account id, client side

A page receives account ids and needs names. Both codebases scan `Scores`:

```
// UI/Modules/ReviveWidget.Script.txt:41
CSmScore GetScoreFromUuid(Text Uuid) {
    foreach(Score in Scores) {
        if (Score.User.WebServicesUserId == Uuid) return Score;
    }
    return Null;
}
```

Resolving in the page rather than on the server keeps display names current across a
rename. The cost is that a player whose score is gone resolves to `Null`, and Revive-KO
renders nothing for them (`:49`, early return). Ours resolves server-side and substitutes
`"?"`. Neither is wrong, but the failure has to be handled somewhere, and a silent gap in a
roll-call is the worse of the two.

## Queue position as a status token

The most reusable idea in the repo, and it is four lines
(`UI/Modules/ScoreTable.Script.txt:62-77`). Against each eliminated player the scoretable
prints their **position in the revive queue** — `KO1`, `KO2`, `KO3` — alongside `DC`, `DNF`
and a tick for the living.

That answers "am I getting back in, and when" continuously, on a screen the player opens
themselves, rather than in a popup they have to catch. For a one-team gulag the queue is
global, and the token reads as a place in line.

## Fixed slots, and what they cost

Both knockout widgets pre-declare `<frameinstance>` slots inside a `framemodel`, hide them
all, then show and fill the first N. Cheap and stable — the page never rebuilds its tree.

The cost is a hard cap. `ML::Clamp(KnockoutPlayers.count, 0, 8)`
(`UI/Modules/Knockout.Script.txt:92`) shows eight names and says nothing about the rest, so
a 20 player round silently under-reports. If the count varies, either say how many were not
shown or measure and lay out rather than slotting: our gulag screen caps at
`C_GulagMaxNames`, appends "and N more", and sizes the panel from `ComputeWidth`.

## A layer can be attached to one player

`Layers::Attach` takes an optional player, and Beu's debug mode uses both forms
(`Libs/Beu/ModeLibs/TM_DebugMode.Script.txt:49` and `:54`, `:262`):

```
Layers::Attach(C_DebugMode_MainUI);          // everyone
Layers::Attach(C_DebugMode_MainUI, Player);  // that player only
```

Worth knowing before reaching for netvars. Our gulag screens push to `Teams[0]` and every
client decides what to draw, which is right for a roll-call everyone should see. It is the
wrong shape for anything addressed to a subset — a roster shown only to the players in the
gulag, or an admin panel — where a per-player attach avoids broadcasting state that most
clients exist only to ignore.

## Layer types, and the stadium screen

`Layers::SetType` picks where a layer lives:

- `Normal` — the HUD. Almost everything.
- `ScoresTable` — bound to the scoreboard key. Revive-KO puts both its scoretable and its
  team picker here (`UI/UI.Script.txt:149`, `:161`), so "hold TAB" gets you either
  depending on match phase.
- `ScreenIn3d` — a surface inside the map.

`ScreenIn3d` pairs with `AttachId`, which names the surface
(`UI/UI.Script.txt:210-217`):

```
Layers::SetType(C_Layer_WinnerWidget, CUILayer::EUILayerType::ScreenIn3d);
Layers::Get(C_Layer_WinnerWidget).AttachId = "16x9_StadiumSmall";
```

so the winner screen plays on the stadium's big screen during the podium, and falls back to
a `Normal` layer with `AttachId = ""` when the map has no podium.

This qualifies a note in `HANDOFF-kotd-gulag-ui.md`, which observed that `attachId` reads
back `Unassigned` on every layer in TM2020. That was measured on Nadeo's UI modules, which
are all `Normal`. The field is writable and meaningful — it is just empty on layers that
are not attached to a 3D surface.

## What the animation string accepts

Wider than `pos`. Across the widgets: `pos`, `scale`, `rot`, `opacity`, and `textcolor`.
The element name in the string tracks the target — `<elem>`, `<frame>`, `<label>`:

```
// UI/Modules/321Go.Script.txt: opacity + scale + rot together
AnimMgr.Add(ctrl, "<elem opacity=\"0.7\" scale=\"2\" rot=\"0\" />", Now+1+(x*500), dur, QuadOut);
// UI/Modules/LiveRanking.Script.txt:154: animating a text colour
AnimMgr.Add(label, "<label opacity=\"1\" textcolor=\""^Color^"\" />", Now, 50, QuadOut);
```

There are two arities. The four-argument form takes a start time; the three-argument form
omits it and starts immediately (`LiveRanking.Script.txt:161`) — which is what a live
ranking wants, since rows re-sort on every checkpoint and there is nothing to schedule.

Sliding a row to its new rank is one call against the row frame. That is the whole of a
live ranking's motion.

## Sound, without a mode round trip

A page can hold an audio source and play the game's own sounds by path
(`UI/Modules/CheckpointSound.Script.txt`):

```
declare CAudioSource Snd = Audio.CreateSound(
    "file://Media/Manialinks/Nadeo/Trackmania/Ingame/Sound/RaceCheckPoint_Experimental.wav",
    0.0, False, False, False);
```

driven by a `while(True) yield` loop watching `GUIPlayer.RaceWaypointTimes.count`. Entirely
client-side: no netvar, no server tick, and it stays correct while spectating because
`GUIPlayer` follows the camera. The same loop shape drives the 3-2-1 (watching
`GUIPlayer.SpawnStatus`) and the winner banner's sine pulse.

`ModeUtils::PlaySound` from the mode is the other route, and the one our gulag screen uses
for its warning. Prefer the page-side loop when the trigger is something the client can
already see.

## Open question: `.tga` images

`LiveRanking.Script.txt:13-14` draws its screen-edge alert with a painter stencil:

```
image="file://Media/Painter/Stencils/13-SquareGradiant2/Brush.TGA" modulatecolor="f00"
```

`.tga` is **not** in the extension set `Tm2020ManialinkFacts.ImageExtensions` accepts, so
`validate_manialink_xml` rejects it today.

Do not change the validator on this evidence. `.tga` appears 52 times across `refs/`, but
only twice in anything TM2020-era, and both are these two quads — which start at
`opacity="0"` and are animated in, so a silent load failure would look identical to the
intended resting state. That is exactly the shape of a Maniaplanet habit carried forward
untested.

It is cheap to settle: put both quads at `opacity="1"` and `preview_manialink_xml` it
against a live client. Either the validator gains an extension with evidence, or this gets
recorded as a dialect trap.

The technique is worth having either way. A wide gradient stencil, `modulatecolor` to the
alert colour, one at each screen edge, faded in by `AnimMgr` — that is a screen-edge alert
frame in two quads, and our gulag round has one.

## The in-game inspector, and where to get it

Revive-KO extends Beu's `TM_DebugMode`, a mode that wraps yours and adds a tabbed,
minimisable in-game panel: bot management, live speed/steer control, a variable inspector,
a custom-event builder, and a timestamped log pane.

**Take it from `refs/TM2020-Gamemodes/TM_DebugMode.Script.txt`, not from Revive-KO.**
Revive-KO vendors `2023-09-01`; Beu's own repo has `2025-11-03`, and the newer one is the
portable one:

| | Revive-KO copy | TM2020-Gamemodes |
|---|---|---|
| version | 2023-09-01 | 2025-11-03 |
| extends | `Libs/Zerax/.../ModeBase` | `Modes/TrackMania/TM_Rounds_Online` |
| labels | `LogVersions`, `LoadHud`, `InitMap`, `Yield` | `Match_LogVersions`, `Match_AfterLoadHud`, `Match_InitMap`, `Match_Yield` |
| includes | `TL`, `ML`, `TiL` | `DebugMode_TL`, `DebugMode_ML`, `DebugMode_TiL` |

The panel's command set is identical between them — the difference is entirely that the
newer one targets Nadeo's Rounds base with standard label names and namespaced includes, so
it composes with a mode that already uses `TL`/`ML` of its own.

Its usage model is inversion: you do not include it, you point its `#Extends` at your mode
and run *it* as the server's mode. The same shape as our `KackyKOTDDevBots.Script.txt`, and
it stacks on top of one.

Two engine facts worth having on their own:

- **`Dbg_DumpDeclareForVariables(Entity, False)`** dumps every `declare ... for` variable
  attached to an entity — `Teams[0]`, `This`, `UIManager.UIAll`, a `Player`, a `Score`, a
  `CUIConfig`. Netvars and per-entity state are otherwise invisible without a log line per
  variable. Nadeo's own `Libs/Nadeo/CMGame/Utils/Log.Script.txt` uses it, so it is a real
  library function rather than something Beu built.
- **`+++LabelName+++`** injects a label block at that point, and `***LabelName***` defines
  one. That is how a 1,000 line mode keeps one copy of its timestamp-prefix code and its
  player-lookup and pastes them into eight call sites.

`ModeUtils::GetPlayerFromAccountId` is also worth stealing on its own; the debug mode pairs
it with a length check to accept an account id (36 chars), a login (22), or a display name
in one field (`DebugMode_FindPlayer`).

## Not taken

- **Team colouring and car skins.** Revive-KO's author recommends removing them for a
  teamless cup, and the team system is load-bearing there in a way it is not here:
  `Player_CanSpawn` refuses to spawn a player with no team
  (`Modes/Trackmania/ReviveKO.Script.txt:98-107`).
- **The knockout and revive widgets as files.** The animation vocabulary is worth having;
  the widgets themselves cap at 8 and drop departed players.
- **`LiveRanking.Script.txt`.** CotD already ranks live, and ADR 0001 keeps KOTD
  stock-plus-gulag.

## Licensing of the sources quoted here

This repo is public, so it matters what came from where.

- **`Revive-KO`** has **no LICENSE file**. Its author has said verbally that the code is
  his and offered it for use, which is not the same as a licence. Short quotes for
  commentary, as above, are ordinary technical writing; copying files out of it is not
  settled until he puts a licence on it.
- **`TM2020-Gamemodes`** (Beu) is **GPL-3.0**. Quoting is fine. Vendoring `TM_DebugMode`
  into another repo makes that repo a derivative work, which is why KackyGG generates it
  from the mirror at `dev-server/make-debug-mode.py` and gitignores the output instead of
  committing it. Private today is not an argument — it is a latent obligation nobody will
  remember when the repo stops being private.
- Nadeo's `Libs/` are quoted from the Maniaplanet-era open mirrors in `refs/`; the TM2020
  ones ship inside the title pack and are published nowhere, which is the reason this
  mirror exists.

## Where the evidence lives

`refs/Revive-KO` was already in `refs/MANIFEST.md`, at the same commit as the copy that
prompted this note. The mirror was never the problem; knowing what is in it is. Add a
section here rather than re-reading 3,400 lines to rediscover the same six patterns.
