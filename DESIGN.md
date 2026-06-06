# TM2020 MCP Design

## Product Shape

`tm2020-mcp` is a local admin/developer workbench for Kacky and Trackmania 2020. Kontrol
already controls the server; this MCP should help admins and developers inspect, debug,
prototype, and validate things around the game client/editor that are awkward to reason
about from chat commands alone.

## Current Design Principles

- **Local first:** the bridge listens on localhost and targets the running Trackmania
  client/OpenPlanet plugin.
- **Nerd tooling, not player commands:** prefer diagnostics, previews, lab tools, and
  safe experiments over duplicating Kontrol actions.
- **Small surfaces:** expose narrow MCP tools with clear inputs and readable outputs.
- **Safe UI experiments:** distinguish map-editor ManiaLink preview from Interface
  Designer paste/import. Designer fragments are intentionally static and minimal.
- **Source-linked docs:** keep OpenPlanet and ManiaLink notes curated with URLs so agents
  can reason quickly and verify upstream when needed.

## Core Workflows

### ManiaLink / HUD Lab

Use the bridge to preview raw ManiaLink XML in the map editor and use local tools to
inspect or produce paste-safe Interface Designer fragments.

Useful outputs:

- static Designer fragments
- interactive-control reports
- crash-risk explanations
- small widget examples

### EmojiChat Lab

Use local analysis to debug Kacky chat rendering issues before trying them live.

Useful outputs:

- parsed emoji shortcodes
- unknown emoji warnings
- Trackmania format-code detection
- ManiaLink-safe text
- small label preview fragments

### ManiaLink Event Inspector

Use the bridge event buffer to store recent event payloads from probes or future
ManiaLink scripts. Use local XML inspection to identify controls that should produce
events.

Current event inspector is intentionally simple:

- `POST /manialink/events` records a payload
- `GET /manialink/events` returns recent payloads
- `POST /manialink/events/clear` clears the buffer

Future work can attach actual in-game UI probes that POST clicked control IDs/actions
into this buffer.

## Non-Goals

- Replacing Kontrol.
- Hosting public services.
- Committing large raw API documentation dumps.
- Assuming Interface Designer accepts the same XML as map-editor preview.
- Treating video/GPS UI as solved until static `<video>` playback and runtime click
  control are tested in the target Trackmania contexts.

## Validation Contract

Every behavior change should have tests where practical. Before pushing:

```bash
dotnet build
dotnet test --no-build
```

For AngelScript/OpenPlanet changes, also validate by reloading the plugin in OpenPlanet
when a running Trackmania client is available.
