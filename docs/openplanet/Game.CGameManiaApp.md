# Game.CGameManiaApp

Source: https://next.openplanet.dev/Game/CGameManiaApp
Last reviewed: 2026-06-06

## Role

`CGameManiaApp` is the base API surface for ManiaPlanet client scripts. Several useful
contexts inherit from it, including map editor plugins, browser apps, playground apps,
and OpenPlanet/ManiaPlanet plugin surfaces.

## Relevant Methods

- `CGameUILayer@ UILayerCreate()`
- `void UILayerDestroy(CGameUILayer@ Layer)`
- `void UILayerDestroyAll()`
- `void LayerCustomEvent(CGameUILayer@ Layer, wstring Type, MwFastBuffer<wstring>& Data)`
- `void OpenLink(string Url, CGameManiaApp::ELinkType LinkType)`
- `bool OpenFileInExplorer(wstring FileName)`
- `void Dialog_Message(wstring Message)`
- `wstring Dbg_DumpDeclareForVariables(CMwNod@ Nod, bool StatsOnly)`

## Relevant Members

- `uint LayersDefaultManialinkVersion`
- `const MwFastBuffer<CGameUILayer@> UILayers`
- `const CXmlScriptParsingManager@ Xml`
- `const CNetScriptHttpManager@ Http`
- `const CGameVideoScriptManager@ Video`
- `const CAudioScriptManager@ Audio`
- `const CInputScriptManager@ Input`
- `const CGameDataFileManagerScript@ DataFileMgr`
- `const CGameScoreAndLeaderBoardManagerScript@ ScoreMgr`
- `const CGameUserManagerScript@ UserMgr`
- `const CSystemPlatformScript@ System`
- `const CGameManiaPlanetScriptAPI@ ManiaPlanet`

## tm2020-mcp Implications

- UI layer experiments should usually start from a context that inherits this class.
- `UILayerCreate` and `LayerCustomEvent` are candidates for richer ManiaLink preview
  work than the current map-editor `PluginMapType.ManialinkText` path.
- The presence of a `Video` manager does not mean Interface Designer has a pasteable
  video XML element. Treat video as a script/API investigation, not a static XML fragment
  feature.
- `OpenLink` could be useful for admin buttons that open external pages or ManiaLink
  browser pages.

## Open Questions

- Which runtime contexts in Trackmania 2020 expose enough permission to control video
  playback safely?
- Can an OpenPlanet plugin create and own a temporary UI layer while the player is on a
  live server without conflicting with server-provided ManiaLinks?
