# Game.CGameScriptHandlerBrowser

Source: https://next.openplanet.dev/Game/CGameScriptHandlerBrowser
Last reviewed: 2026-06-06

## Role

`CGameScriptHandlerBrowser` is the ManiaLink browser script handler. It inherits from
`CGameManialinkScriptHandler`.

## Relevant Methods

- `void ShowCurMapCard()`
- `void BrowserBack()`
- `void BrowserQuit()`
- `void BrowserHome()`
- `void BrowserReload()`

## Relevant Members

- `const CGameManiaAppBrowser@ ParentApp`
- `const CGameCtnChallenge@ CurMap`
- `const bool IsInBrowser`
- `wstring BrowserFocusedFrameId`

## Inherited ManiaLink Handler Surface

The browser handler inherits the same core event/action helpers that are relevant for
interactive ManiaLinks:

- `OpenLink`
- `TriggerPageAction`
- `SendCustomEvent`
- `PreloadImage`
- `PreloadAll`
- `PendingEvents`
- `Http`
- `Video`
- `Audio`

## tm2020-mcp Implications

- Useful when an admin control should open a ManiaLink browser page rather than render in
  the in-game HUD layer.
- Browser navigation methods are specific to browser context and should not be assumed to
  work in map editor or playground UI context.
- If a video/GPS concept becomes a hosted page instead of an in-game HUD widget, this
  handler is a likely place to investigate.
