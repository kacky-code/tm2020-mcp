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
- Detect whether the map editor, Interface Designer, or module editor is active.

Interface Designer support is diagnostic-only for now. The bridge can detect `CGameEditorManialink`, but XML injection currently targets the map editor's `PluginMapType.ManialinkText` path.

## Requirements

- Trackmania 2020
- OpenPlanet with Developer Mode enabled
- .NET 10 SDK
- An MCP-compatible client

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

Example MCP config for a native install:

```json
{
  "mcpServers": {
    "tm2020": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/path/to/tm2020-mcp/src/Tm2020Mcp/Tm2020Mcp.csproj",
        "--no-build"
      ]
    }
  }
}
```

For Windows, use your local checkout path:

```json
{
  "mcpServers": {
    "tm2020": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\path\\to\\tm2020-mcp\\src\\Tm2020Mcp\\Tm2020Mcp.csproj",
        "--no-build"
      ]
    }
  }
}
```

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

Preview an XML file:

```bash
curl -4 -sS --max-time 5 \
  --data-binary @/path/to/widget.xml \
  http://127.0.0.1:29100/manialink/preview
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

## Notes

- Keep the plugin in the user-local `OpenplanetNext/Plugins` folder. The game-install `Openplanet/Plugins` folder may reject local source plugins with signature errors.
- If a sandboxed process cannot reach `127.0.0.1:29100`, test with a normal terminal. Some agent sandboxes block localhost sockets even when the bridge is listening.
- The bridge listens only on localhost.
- Docker does not replace OpenPlanet. Trackmania and the OpenPlanet plugin still run on the host.
