# TM2020 MCP Agent Guide

This file governs the entire `tm2020-mcp` repository.

## Purpose

This repo is a local MCP workbench for Trackmania 2020 admin/developer workflows. It
bridges AI agents to Trackmania through an OpenPlanet plugin and exposes focused tools for
editor state, ManiaLink preview/debugging, Interface Designer fragments, EmojiChat
experiments, and future Kacky admin diagnostics.

Do not duplicate Kontrol server commands here unless the feature is specifically about
local inspection, prototyping, validation, or debugging around the game client/editor.

## Working Rules

- Prefer small, reviewable changes.
- Preserve existing behavior unless the user explicitly asks to change it.
- Add or update tests for behavior changes.
- Use TDD for non-trivial logic: write or update a failing test first, then implement the
  minimum code to pass.
- Run `dotnet build` and `dotnet test --no-build` before pushing.
- Update README/docs when MCP tools, bridge endpoints, setup, or examples change.
- Keep OpenPlanet API notes under `docs/openplanet/` curated and source-linked; do not
  dump full raw websites into git.
- Keep generated/raw docs snapshots out of git. `docs/openplanet/raw/` is ignored.
- Do not commit build outputs (`bin/`, `obj/`, `TestResults/`).

## Architecture

- `openplanet-plugin/TM2020Bridge/` is the AngelScript OpenPlanet plugin. It owns the
  localhost HTTP bridge and all direct Trackmania/OpenPlanet interaction.
- `src/Tm2020Mcp/EditorBridge/` is the .NET HTTP client for the OpenPlanet bridge.
- `src/Tm2020Mcp/Tools/TrackmaniaTools.cs` exposes MCP tools.
- `src/Tm2020Mcp/EmojiChat/` contains local EmojiChat parsing/preview helpers.
- `src/Tm2020Mcp/Manialinks/` contains local ManiaLink inspection/sanitization helpers.
- `examples/` stores small reusable XML fragments.
- `docs/openplanet/` stores curated API notes with upstream URLs.

## OpenPlanet Bridge Rules

- Keep bridge endpoints localhost-only.
- Keep endpoint responses JSON unless the endpoint is explicitly returning XML.
- When adding an endpoint, add matching .NET client methods and MCP tools only if the
  tool is useful to agents.
- The AngelScript plugin cannot be validated by `dotnet`; document any OpenPlanet reload
  requirement and avoid syntax-risky changes when a .NET-side tool is enough.

## Interface Designer Rules

Designer paste/import is more fragile than map-editor `ManialinkText` preview.

Paste-safe fragments should usually:

- omit XML declarations
- omit `<manialinks>` and `<manialink>` wrappers
- use static `frame`, `quad`, and `label` nodes
- escape raw XML attribute text like `<` as `&lt;`
- avoid `framemodel`, `frameinstance`, `entry`, `scroll`, `action`, `scriptevents`,
  draggable classes, hidden handles, and huge generated z-index precision unless the
  feature is specifically testing those constructs

## Verification Before Push

Run:

```bash
dotnet build
dotnet test --no-build
```

If OpenPlanet plugin behavior changed, also reload the plugin in OpenPlanet and check
`Openplanet.log` when possible. If that live check is not possible, say so explicitly.
