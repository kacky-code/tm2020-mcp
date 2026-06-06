# Openplanet Docs Cache

Last reviewed: 2026-06-06

This directory is a curated local cache for agent-readable Openplanet and ManiaLink
reference notes. It is not a full mirror of `next.openplanet.dev`.

Use this cache for fast local orientation, then verify against the source URL before
implementing unfamiliar or risky API usage.

## Cached Pages

| File | Source | Why it matters |
| --- | --- | --- |
| [Game.CGameManiaApp.md](Game.CGameManiaApp.md) | https://next.openplanet.dev/Game/CGameManiaApp | Base ManiaApp API for UI layers, links, script managers, and video/audio manager access. |
| [Game.CGameScriptHandlerPlaygroundInterface.md](Game.CGameScriptHandlerPlaygroundInterface.md) | https://next.openplanet.dev/Game/CGameScriptHandlerPlaygroundInterface | In-game ManiaLink handler surface for events, profile/spectate actions, UI sounds, and video/audio manager access. |
| [Game.CGameScriptHandlerBrowser.md](Game.CGameScriptHandlerBrowser.md) | https://next.openplanet.dev/Game/CGameScriptHandlerBrowser | Manialink browser handler surface; useful for browser navigation and inherited ManiaLink event APIs. |
| [Game.CGameEditorPluginMap.md](Game.CGameEditorPluginMap.md) | https://next.openplanet.dev/Game/CGameEditorPluginMap | Map editor plugin surface; includes `ManialinkText`, `ManialinkPage`, editor state, and inherited ManiaApp APIs. |
| [Game.CGameManiaplanetPlugin.md](Game.CGameManiaplanetPlugin.md) | https://next.openplanet.dev/Game/CGameManiaplanetPlugin | OpenPlanet plugin context surface with current server/map/player state and inherited ManiaApp APIs. |
| [manialink-elements.md](manialink-elements.md) | https://doc.maniaplanet.com/manialink/getting-started | Practical ManiaLink XML tag notes for frames, quads, labels, audio/music, and paste-safe Interface Designer fragments. |

## Update Policy

- Keep pages small and task-focused.
- Prefer class names, methods, members, and practical implications over full raw HTML.
- Add new pages only when the repo uses or experiments with that API surface.
- Update `Last reviewed` when checking against upstream docs.
- Do not paste entire Openplanet pages into this repo.

## Upstream Repository Leads

Openplanet's GitHub organization is https://github.com/openplanet-nl.

Verified public repos that may be useful:

- `openplanet-nl/nadeoapi-docs`: source for the Trackmania web services docs at
  `webservices.openplanet.dev`, not the same thing as the `next.openplanet.dev` engine
  class reference.
- `openplanet-nl/opdev-config-tracking`: listed by GitHub as "API configuration history".
  This may be related to generated engine API docs; verify before depending on it.

## Fetch Helper

The allowlisted source URLs are also stored in [sources.txt](sources.txt). To refresh raw
HTML snapshots for manual review, run:

```bash
scripts/cache-openplanet-docs.sh
```

Raw snapshots are written under `docs/openplanet/raw/` and ignored by git.
