# TM2020 MCP

Local MCP tooling for Trackmania 2020 through an OpenPlanet plugin bridge.

The project has two pieces:

- `openplanet-plugin/TM2020Bridge` - an OpenPlanet plugin that exposes a tiny HTTP server on `127.0.0.1:29100`.
- `src/Tm2020Mcp` - a .NET MCP server that calls the bridge and exposes Trackmania tools to an AI agent.

## Current Capabilities

- Check whether the OpenPlanet bridge and TM2020 editor are active.
- Preview raw ManiaLink XML in the map editor with `PluginMapType.ManialinkText`.
- Preview ManiaLink XML from a local file.
- Clear the current ManiaLink preview.
- Trigger map-editor autosave.
- Create a new map in the editor, place and remove blocks, and save it to disk (`create_map`,
  `remove_map_block`). Needs the game at the main menu with no editor open.
- Read saved maps straight off disk with GBX.NET, with no running game: list a map's blocks
  (`inspect_map_gbx`), derive what a block direction means in world coordinates by counting
  neighbours across a corpus (`analyze_map_block_directions`), and walk a track from its start
  block to check the road actually connects (`verify_map_track`).
- Detect whether the map editor, Interface Designer, or module editor is active.
- Validate ManiaLink XML against Trackmania 2020 constraints before pushing it into the game (`validate_manialink_xml`, `validate_manialink_file`). Checks the v3 dialect, element names, media formats the client can actually decode, the 320x180 coordinate space, script-event wiring, duplicate ids, and Interface Designer paste-safety. Rules and their evidence: [`docs/manialink-tm2020.md`](docs/manialink-tm2020.md).
- Fetch every http(s) media URL in ManiaLink XML and report what the game will silently fail to render (`check_manialink_media`): dead URLs, non-200s, a web page served where a media file was expected, and animated WebP confirmed from its VP8X header. Needs no running game.

Interface Designer support is diagnostic-only for now. The bridge can detect `CGameEditorManialink`, but XML injection currently targets the map editor's `PluginMapType.ManialinkText` path.

## Requirements

- Trackmania 2020
- OpenPlanet with Developer Mode enabled
- .NET 10 SDK
- An MCP-compatible client

## Windows Quickstart

The whole path from nothing to a working loop. The game client half is Windows-native by
necessity: Openplanet hooks the running game process, so it cannot be containerised.

### 1. Prerequisites

- Trackmania 2020 with **Club Access**. This is a hard requirement, not a nicety: the bridge is an
  unsigned plugin, unsigned plugins load only under Openplanet's Developer Mode, and Developer Mode
  is unavailable on Starter and Standard editions. Nadeo restricts it to prevent piracy. See
  https://openplanet.dev/next/club
- Openplanet for Trackmania, from https://openplanet.dev/download
- .NET 10 SDK, from https://dotnet.microsoft.com/download (or Docker Desktop, see below)
- git

### 2. Clone

```powershell
cd $HOME
git clone https://github.com/kacky-code/tm2020-mcp.git
cd tm2020-mcp
```

**Use a normal PowerShell, not an elevated one.** Nothing here needs administrator rights, and an
elevated prompt starts in `C:\Windows\System32`, so any relative path silently resolves against
the wrong directory.

### 3. Install the bridge plugin

Set `$Repo` to wherever you cloned it. The commands then work from any directory, which avoids the
most common failure here: a relative path resolving against `C:\Windows\System32`.

```powershell
$Repo      = "$HOME\tm2020-mcp"
$PluginDir = "$env:USERPROFILE\OpenplanetNext\Plugins"

New-Item -ItemType Directory -Force -Path $PluginDir | Out-Null
Remove-Item -Recurse -Force "$PluginDir\TM2020Bridge" -ErrorAction SilentlyContinue
Copy-Item -Recurse "$Repo\openplanet-plugin\TM2020Bridge" "$PluginDir\TM2020Bridge"

Get-ChildItem $PluginDir
```

`Get-ChildItem` must list `TM2020Bridge`. If it does not, `$Repo` is wrong:

```powershell
Test-Path "$Repo\openplanet-plugin\TM2020Bridge"
```

### 4. Enable it in game

1. Launch Trackmania and press `F3` for the Openplanet overlay.
2. Enable **Developer Mode** under Developer > Signature Mode in the Openplanet overlay. Plugins
   installed as a plain folder are unsigned, so they will not load without it. If the option is
   missing or refuses to enable, the account does not have Club Access, and no local install of
   this plugin will work until it does.
3. Load or reload plugins from the Openplanet menu. The exact menu label moves between Openplanet
   versions; look for "Load plugin" or "Reload plugins" under the developer menu.

### 5. Verify the bridge is listening

```powershell
curl.exe -sS --max-time 3 http://127.0.0.1:29100/status
```

**Use `curl.exe`, not `curl`.** PowerShell aliases `curl` to `Invoke-WebRequest`, which takes
different arguments and will fail confusingly.

Expected:

```json
{"running":true,"editor_open":false,"map_editor":false,"interface_designer":false,"module_editor":false,"manialink_preview":false}
```

No response means the plugin is not loaded. Check the Openplanet log.

### 6. Build the MCP server

```powershell
dotnet build
```

### 7. Point your MCP client at it

**Claude Code needs no setup.** The repo ships a `.mcp.json` with a path relative to the checkout,
so the server is registered as soon as you start Claude Code from the repo root. Approve it when
prompted, and check it with `/mcp`.

For a client that takes JSON config, see [Run Native .NET](#run-native-net) below. The server
reads `TM2020_BRIDGE_URL` and defaults to `http://127.0.0.1:29100`, so it needs no configuration
when the game runs on the same machine.

If you register the server by hand instead, **substitute your own checkout path** — the paths in
this README are placeholders, not literals:

```powershell
claude mcp add tm2020 -- dotnet run --project $PWD\src\Tm2020Mcp\Tm2020Mcp.csproj
```

A registration pointing at a path that does not exist fails as `-32000` at connect time, which
reads like a server crash rather than a typo.

Adding `--no-build` skips the rebuild and starts faster, but then step 6 has to have run first,
and any later C# change is ignored until you rebuild. `.mcp.json` omits it for that reason.

### 8. Check it end to end

Ask the agent to call `get_tm2020_status`. It should report the same thing step 5 did.

`validate_manialink_xml` works with no game running at all, so it is the cheapest way to confirm
the MCP server itself is wired up correctly.

### 9. Optional: run the W7 readback probe

Throwaway probe answering wayfinder ticket W7 (see `WAYFINDER-MAP.md`): whether server-delivered
UI layers really expose a walkable page, and whether a failed image load is detectable.

```powershell
Remove-Item -Recurse -Force "$PluginDir\_W7Probe" -ErrorAction SilentlyContinue
Copy-Item -Recurse "$Repo\openplanet-plugin\_W7Probe" "$PluginDir\_W7Probe"
```

Reload plugins, **join a server** (the local docker dev-server is ideal, and a public server works
too since the probe only reads), then use **Plugins > W7 Readback Probe: dump UI layers**.

Output goes to the Openplanet log, reachable from the Openplanet overlay. It reports the layer
count, and per layer the attach id, visibility, whether its ManiaScript is running, the control
count, and an image-quad tally of `loaded / pending / failed` with a line per failure.

`ClientManiaAppPlayground is null` means you are not on a server yet.

Delete the folder when it has answered its question.

## Native .NET Or Docker?

Use native .NET when you can. It is the simplest setup because the MCP server and OpenPlanet bridge both run on the host, so `http://127.0.0.1:29100` works directly.

Use Docker when you want a reproducible MCP server runtime or do not want to install the .NET SDK on the machine running the MCP client. In Docker, `127.0.0.1` points inside the container, so the MCP server must use:

```text
http://host.docker.internal:29100
```

The OpenPlanet plugin still runs inside Trackmania on the host in both setups.

## Install The OpenPlanet Plugin

Copy `openplanet-plugin/TM2020Bridge` into your OpenPlanet user plugins folder.

On Windows, this path is commonly:

```powershell
$env:USERPROFILE\OpenplanetNext\Plugins\TM2020Bridge
```

Install from PowerShell:

```powershell
$PluginDir = "$env:USERPROFILE\OpenplanetNext\Plugins"
Remove-Item -Recurse -Force "$PluginDir\TM2020Bridge" -ErrorAction SilentlyContinue
Copy-Item -Recurse ".\openplanet-plugin\TM2020Bridge" "$PluginDir\TM2020Bridge"
```

On macOS with CrossOver, the user plugin folder is inside the bottle. The exact bottle name can vary:

```bash
~/Library/Application\ Support/CrossOver/Bottles/<Bottle Name>/drive_c/users/crossover/OpenplanetNext/Plugins/TM2020Bridge
```

Install from bash:

```bash
PLUGIN_DIR="$HOME/Library/Application Support/CrossOver/Bottles/<Bottle Name>/drive_c/users/crossover/OpenplanetNext/Plugins"
rm -rf "$PLUGIN_DIR/TM2020Bridge"
cp -R openplanet-plugin/TM2020Bridge "$PLUGIN_DIR/"
```

Then in Trackmania:

1. Open the OpenPlanet overlay with `F3`.
2. Enable Developer Mode if it is not already enabled.
3. Use `F3 -> Load plugins`.

Verify from a normal terminal:

```bash
curl -4 -sS --max-time 3 http://127.0.0.1:29100/status
```

Example response:

```json
{"running":true,"editor_open":true,"map_editor":true,"interface_designer":false,"module_editor":false,"manialink_preview":false}
```

## Run Native .NET

Build once:

```bash
dotnet build
```

Run manually:

```bash
dotnet run --project src/Tm2020Mcp/Tm2020Mcp.csproj
```

Run with a custom bridge URL:

```bash
TM2020_BRIDGE_URL=http://127.0.0.1:29100 \
dotnet run --project src/Tm2020Mcp/Tm2020Mcp.csproj
```

Example MCP config for a native install. Replace `/path/to/tm2020-mcp` with your actual checkout
path — a config pointing at a nonexistent project fails at connect time with a generic transport
error (`-32000` in Claude Code) that looks like a server crash:

```json
{
  "mcpServers": {
    "tm2020": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/path/to/tm2020-mcp/src/Tm2020Mcp/Tm2020Mcp.csproj"
      ]
    }
  }
}
```

On Windows the path needs escaped backslashes, again substituting your own checkout:

```json
{
  "mcpServers": {
    "tm2020": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\Users\\you\\code\\tm2020-mcp\\src\\Tm2020Mcp\\Tm2020Mcp.csproj"
      ]
    }
  }
}
```

Clients that launch the server with the repo as its working directory can use the relative path
`src/Tm2020Mcp/Tm2020Mcp.csproj` and skip the substitution entirely, as `.mcp.json` does.

## Run With Docker

Build the image:

```bash
docker build -t tm2020-mcp .
```

Run it manually:

```bash
docker run --rm -i \
  -e TM2020_BRIDGE_URL=http://host.docker.internal:29100 \
  tm2020-mcp
```

Example MCP config for Docker:

```json
{
  "mcpServers": {
    "tm2020": {
      "command": "docker",
      "args": [
        "run",
        "--rm",
        "-i",
        "-e",
        "TM2020_BRIDGE_URL=http://host.docker.internal:29100",
        "tm2020-mcp"
      ]
    }
  }
}
```

On Linux, `host.docker.internal` may require host-gateway mapping:

```json
{
  "mcpServers": {
    "tm2020": {
      "command": "docker",
      "args": [
        "run",
        "--rm",
        "-i",
        "--add-host=host.docker.internal:host-gateway",
        "-e",
        "TM2020_BRIDGE_URL=http://host.docker.internal:29100",
        "tm2020-mcp"
      ]
    }
  }
}
```

## MCP Tools

- `set_openplanet_bridge_url(url)` - override the bridge base URL. Defaults to `http://127.0.0.1:29100`.
- `get_tm2020_status()` - show bridge/editor state.
- `preview_manialink_xml(xml)` - push raw ManiaLink XML to the map editor.
- `preview_manialink_file(path)` - read an XML file from disk and preview it.
- `clear_manialink_preview()` - clear the current preview.
- `autosave_map_editor()` - trigger map-editor autosave.
- `remove_map_block(x, z, y?, probe?)` - remove one grid block from the open editor, reporting
  what was there. `probe` additionally reads engine state off the deleted block's handle; see
  below.
- `create_map(saveAs?, withTrack?, straightCount?, direction?, originX?, originZ?,
  environment?, decoration?, mapType?, waitSeconds?)` - open the editor on a brand new map,
  optionally lay a track, optionally save it. Pass `routeLength` to place a generated turning
  route instead of the straight three-block one. Refuses to run while any
  editor is open so it cannot discard unsaved work. `saveAs` is relative to the user Maps
  folder and may name a subfolder: `"MCP/dummy.Map.Gbx"` writes
  `Documents/Trackmania/Maps/MCP/dummy.Map.Gbx`. The map's internal name stays `Unnamed`.
- `inspect_map_gbx(path, nameFilter?, limit?)` - list the blocks of a `.Map.Gbx` with
  coordinates and directions. Free blocks are marked; they carry a rotation, not a grid
  direction.
- `analyze_map_block_directions(path, nameFilter?, minimumSamples?)` - point at a map or a
  directory and get, per block and direction, which neighbouring cell holds a connected
  block. This is how the direction table in `docs/tm2020-map-geometry.md` was derived.
- `verify_map_track(path)` - walk a saved map from its start block and report whether the
  road connects to the finish.
- `generate_track_plan(seed?, length?, turnChance?, originX?, originZ?, direction?, style?)` -
  generate a connected, turning route from block shapes learned out of real maps, verified
  before it touches the game. `style: "tricks"` mixes in turbo, no-engine, reset and
  ice/bump/water/dirt surfaces; `"plain"` stays on tech road.
- `learn_map_block_connections(path, outputPath?, minimumSamples?, namePrefix?, variant?)` -
  relearn the block-shape model from a corpus of maps.
- `learn_map_motif(path, anchor, radius?, threshold?, outputPath?, excludePrefixes?)` - measure
  a multi-block structure, such as a loop or a reset-gate run, from the maps around it.
- `stamp_map_motif(motifPath, x, z, y?, direction?, minimumSupport?, dryRun?)` - place a learned
  motif into the open editor, rotated, after checking the whole footprint for collisions.
- `write_free_blocks(sourcePath, outputPath, blocksJson, cells?)` - write free blocks into a
  copy of a map at explicit world positions and rotations, with no game running. This places
  geometry the editor plugin API cannot express at all.
- `get_recent_manialink_events()` - show recent ManiaLink event payloads recorded by the bridge.
- `record_manialink_event(body)` - manually record an event payload in the bridge buffer.
- `clear_manialink_events()` - clear the bridge event buffer.
- `inspect_manialink_interactions(xml)` - list interactive `label`/`quad` controls from XML.
- `analyze_emoji_chat_message(message, knownEmojiNames?)` - parse EmojiChat shortcodes,
  Trackmania format codes, unknown emoji, and ManiaLink-safe text.
- `build_emoji_chat_preview_xml(message, knownEmojiNames?)` - generate a small paste-safe
  XML fragment for one EmojiChat line.
- `build_manialink_video_probe_xml(data, music?, play?, hidden?)` - generate a small
  ManiaLink document with a `<video>` tag for GPS/video experiments.

## Probing Engine Behaviour

`remove_map_block(..., probe: true)` exists to settle questions the class reference cannot
answer. It holds the block handle, removes the block, and then reports what the engine left
behind:

```text
"probe": {
  "get_block_null": true,     GetBlock at that coordinate afterwards
  "same_handle": false,       whether it is the handle we held
  "held_units_e": 0,          BlockUnitsE.Length on the deleted handle
  "held_units": 0,            BlockUnits.Length on the deleted handle
  "held_info_null": false     BlockInfo on the deleted handle
}
```

That answers E2, E4 and E5 of [`HANDOFF-editor-facts.md`](HANDOFF-editor-facts.md) in one call.
**Reading members off a handle the engine has just freed is the question being asked, and it is
also the thing that could take the client down**, so the probe is opt-in per request and off by
default. Run it on a scratch map, which `create_map` will make for you.

## Map Analysis

The map tools read `.Map.Gbx` files directly through [GBX.NET](https://github.com/BigBang1112/gbx-net)
and need neither the game nor the bridge. They exist because the editor API cannot answer the
question that matters: `PlaceBlock` reports whether a block *fits in a cell*, never whether the
road *connects*. A track laid in the wrong direction places cleanly and reports nothing but
successes.

```text
verify_map_track("C:/Users/you/Documents/Trackmania/Maps/MCP/dummy.Map.Gbx")

NOT connected: RoadTechStart <24, 9, 24> dir=North points +Z but (24, 9, 25) is empty.
A start block facing away from its own track looks exactly like this.
```

Generation runs off the same evidence. `BlockConnectionModel` learns each block's *shape* -
one open side for a start, two opposite for a straight, two perpendicular for a curve - by
counting neighbours, and `generate_track_plan` chains blocks whose shapes fit. A generated
route is verified before anything is placed:

```text
generate_track_plan(seed: 183, length: 60)
62 blocks, finish=True, verified=True
```

That route placed 62/62 through the bridge and verified connected when parsed back off disk.
It connects and ends properly; it is not a good track, and it is not a Kacky map. Those are
jump gauntlets, and judging a jump needs a car model this repo does not have. See the corpus
numbers in the geometry doc.

`analyze_map_block_directions` is the tool that settles conventions rather than guessing them.
Pointed at a directory of maps, it counts which neighbouring cell holds a connected block for
every (block, direction) pair. A start or finish has one road exit, so it yields a forward
vector with its sign; a symmetric straight yields only an axis and is reported without a
verdict. Results and method: [`docs/tm2020-map-geometry.md`](docs/tm2020-map-geometry.md).

The editor plugin API is grid-only: every placement method takes an `int3` coordinate and a
cardinal direction. Roughly half the blocks in a map like Deep Dip are placed off that grid at
arbitrary angles, so the bridge can never build one. `write_free_blocks` goes around that by
editing the `.Map.Gbx` directly - positions in world units, rotations in degrees, no grid
involved. Note that parsing a written file back is not proof the game accepts it; open the map
to confirm.

Some things in Trackmania are a shape, not a block. A loop is a five-wide wall of
`PlatformTechLoopStart` over a base row; a reset is a run of two or three `GateSpecialReset`.
`learn_map_motif` measures those structures from real maps and `stamp_map_motif` places them
whole, rotated, refusing to half-build one. Note that the engine's own `DecoWall*` and
`Structure*` blocks cannot be stamped back - they are generated, not authored - so motifs
exclude them by default.

Local map corpora are gitignored. Keep them outside the repo or in an ignored folder.

## Interface Designer Fragments

Interface Designer paste/import is more fragile than the map-editor ManiaLink preview path.
Use static fragments without an XML declaration, `<manialinks>` wrapper, or `<manialink>`
wrapper.

Example fragments live in:

```text
examples/interface-designer/
```

These examples intentionally avoid runtime/script-only attributes and nodes such as
`action`, `scriptevents`, `class`, `hidden`, `scroll`, `framemodel`, `frameinstance`, and
`entry`.

## EmojiChat And Event Debugging

The EmojiChat media investigation is recorded in
[`docs/emoji-chat-investigation.md`](docs/emoji-chat-investigation.md). Short version:
static 7TV WebP works in ManiaLinks, animated 7TV WebP/AVIF/GIF does not, and converted
remote VP9 WEBM with alpha works for animated emotes.

To build the Kacky emote CDN payload from the full EmotesZip library:

```bash
node scripts/build-emote-cdn.mjs
```

The script reads top-level `.gif` and `.png` files from `EmotesZip/` by default,
re-encodes animated GIFs to VP9 alpha WEBM, writes transparent first-frame PNG fallbacks
to `var/kacky-discord-emotes/static/`, normalizes static PNG emotes into the same static
directory, writes `var/kacky-discord-emotes/manifest.json`, and runs a dry-run rclone
upload plan to `kacky-r2:kacky-cdn/emotes/`. If a `.gif` and `.png` share the same
basename, the animated GIF wins and the standalone static PNG is skipped. If the source
directory is absent, the script skips re-conversion and keeps the existing WEBMs. The
manifest is hosted on the CDN at:

On case-insensitive local filesystems, case-only PNG output pairs such as `HUH`/`huh`
cannot both exist in the same `static/` directory. The script stages those exact-case
objects under `var/kacky-discord-emotes/static-case-collisions/` and emits targeted
`rclone copyto` commands so the case-sensitive CDN keys are still uploaded.

```text
https://cdn.kacky.gg/emotes/manifest.json
```

The default run never uploads; rclone runs with `--dry-run`. To deploy the generated
media and manifest with the existing local `kacky-r2` rclone remote, run:

```bash
node scripts/build-emote-cdn.mjs --execute
```

After a real upload, purge the Cloudflare cache for `/emotes/*`; old media objects were
served with `cache-control: public, max-age=31536000, immutable`, so overwriting them will
not refresh existing edge cache entries. Media and manifest uploads use
`Cache-Control: public, max-age=86400`.

The rclone remote and bucket/path are configurable when needed:

```bash
node scripts/build-emote-cdn.mjs \
  --rclone-remote kacky-r2 \
  --rclone-path kacky-cdn/emotes
```

`EMOTE_SOURCE_DIR`, `CDN_BASE_URL`, `RCLONE_REMOTE`, and `RCLONE_PATH` are optional
environment variables. `CDN_BASE_URL` defaults to `https://cdn.kacky.gg`.

The bridge includes a small rolling ManiaLink event buffer:

```bash
curl -4 -sS --max-time 3 http://127.0.0.1:29100/manialink/events
curl -4 -sS --max-time 3 \
  -H 'Content-Type: application/json' \
  --data-binary '{"id":"gps","action":"open"}' \
  http://127.0.0.1:29100/manialink/events
curl -4 -sS --max-time 3 -X POST http://127.0.0.1:29100/manialink/events/clear
```

This is currently a debug buffer for probes and future ManiaLink scripts. It does not yet
automatically capture every in-game click.

## Video / GPS Probe

ManiaLink can include a video element, for example:

```xml
<video data="file://Media/Videos/gps.webm" music="1" play="1" hidden="1" />
```

Use `build_manialink_video_probe_xml` to generate a small test document around that tag.
The open question is not whether the tag exists; it is which Trackmania 2020 contexts
accept it and whether click-to-play behavior can be wired cleanly from a visible
ManiaLink control.

## How To Interact From An Agent

Typical prompt flow:

```text
Use the tm2020 MCP. First call get_tm2020_status. If map_editor is true, preview this ManiaLink XML: <manialink version="3">...</manialink>
```

For file-based widget preview:

```text
Use preview_manialink_file with C:\path\to\widget.xml, then call get_tm2020_status.
```

For Docker setups, either set `TM2020_BRIDGE_URL=http://host.docker.internal:29100` in the MCP config or call:

```text
set_openplanet_bridge_url("http://host.docker.internal:29100")
```

## Direct HTTP Commands

Show status:

```bash
curl -4 -sS --max-time 3 http://127.0.0.1:29100/status
```

Create a new map, then give it a track and a file name:

```bash
curl -4 -sS --max-time 10 \
  -H 'Content-Type: application/json' \
  --data '{"environment":"Stadium","decoration":"48x48Screen155Day","map_type":"TrackMania\\TM_Race"}' \
  http://127.0.0.1:29100/map/new

# once /status reports map_editor=true. A negative y means "find the ground for me".
curl -4 -sS --max-time 10 \
  -H 'Content-Type: application/json' \
  --data '{"blocks":[{"name":"RoadTechStart","x":24,"y":-1,"z":24,"dir":"North"}]}' \
  http://127.0.0.1:29100/map/blocks

curl -4 -sS --max-time 10 \
  -H 'Content-Type: application/json' \
  --data '{"file_name":"MCP/dummy.Map.Gbx"}' \
  http://127.0.0.1:29100/map/save
```

Preview an XML file:

```bash
curl -4 -sS --max-time 5 \
  --data-binary @/path/to/widget.xml \
  http://127.0.0.1:29100/manialink/preview
```

Remove a block, and ask what the engine did to the handle:

```bash
curl -4 -sS --max-time 10 \
  -H 'Content-Type: application/json' \
  --data '{"blocks":[{"x":24,"y":-1,"z":24}],"probe":true}' \
  http://127.0.0.1:29100/map/blocks/remove
```

Clear preview:

```bash
curl -4 -sS --max-time 5 \
  -X POST \
  http://127.0.0.1:29100/manialink/clear
```

Get current preview XML:

```bash
curl -4 -sS --max-time 3 http://127.0.0.1:29100/manialink/current
```

## Kontrol Widget Preview Flow

From any project that can export ManiaLink XML:

```bash
cd /path/to/your/widget-project
# Run your project's XML export command.
```

Then preview a generated XML file:

```bash
curl -4 -sS --max-time 5 \
  --data-binary @/path/to/widget.xml \
  http://127.0.0.1:29100/manialink/preview
```

Windows PowerShell equivalent:

```powershell
curl.exe -4 -sS --max-time 5 `
  --data-binary "@C:\path\to\widget.xml" `
  http://127.0.0.1:29100/manialink/preview
```

## Development

Run checks:

```bash
dotnet build
dotnet test --no-restore
```

The OpenPlanet AngelScript plugin cannot be compiled by `dotnet`; verify it by reloading plugins in OpenPlanet and checking `Openplanet.log`.

## Local Reference Docs

Curated Openplanet and ManiaLink notes live in:

```text
docs/openplanet/
```

These files are intentionally small, agent-readable summaries with source URLs. They are
not a full mirror of the Openplanet docs.

[`docs/tm2020-map-geometry.md`](docs/tm2020-map-geometry.md) is the matching reference for the
block grid: what a block `direction` means in world coordinates, where ground level is, and
which claims came from parsing 450 real maps with GBX.NET rather than from the editor API.

## Notes

- Keep the plugin in the user-local `OpenplanetNext/Plugins` folder. The game-install `Openplanet/Plugins` folder may reject local source plugins with signature errors.
- If a sandboxed process cannot reach `127.0.0.1:29100`, test with a normal terminal. Some agent sandboxes block localhost sockets even when the bridge is listening.
- The bridge listens only on localhost.
- Docker does not replace OpenPlanet. Trackmania and the OpenPlanet plugin still run on the host.
