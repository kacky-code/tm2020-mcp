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

## Notes

- Keep the plugin in the user-local `OpenplanetNext/Plugins` folder. The game-install `Openplanet/Plugins` folder may reject local source plugins with signature errors.
- If a sandboxed process cannot reach `127.0.0.1:29100`, test with a normal terminal. Some agent sandboxes block localhost sockets even when the bridge is listening.
- The bridge listens only on localhost.
- Docker does not replace OpenPlanet. Trackmania and the OpenPlanet plugin still run on the host.
