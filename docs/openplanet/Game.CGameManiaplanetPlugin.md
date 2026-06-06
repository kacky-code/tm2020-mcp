# Game.CGameManiaplanetPlugin

Source: https://next.openplanet.dev/Game/CGameManiaplanetPlugin
Last reviewed: 2026-06-06

## Role

`CGameManiaplanetPlugin` is a ManiaPlanet plugin context. It inherits from
`CGameManiaApp` and exposes menu/server/map/player state plus helper methods useful for
admin tooling.

## Relevant Methods

- `void ClipboardSet(wstring ClipboardText)`
- `void QuitGameAndOpenLink(string Url)`
- `void ShowCurMapCard()`
- `CNetScriptHttpRequest@ CreatePostImage(...)`
- `void SetLocalUserClubLink(string ClubLink)`
- `void SetLocalUserNickName(wstring NickName)`
- `void FlashWindow()`
- `void PlaySound(CGameManiaplanetPlugin::EUISound Sound, uint SoundVariant)`
- `void ServerChatLog_Copy()`
- `void CustomEvent(wstring Type, MwFastBuffer<wstring>& Data)`
- `void SendExternalCustomEvent(wstring Type, MwFastBuffer<wstring>& Data)`

## Relevant Members

- `string CurrentServerLogin`
- `wstring CurrentServerName`
- `wstring CurrentServerModeName`
- `string CurrentServerJoinLink`
- `const MwFastBuffer<CGamePlayerInfo@> CurrentServerPlayers`
- `const CGameCtnChallenge@ CurrentMap`
- `const CGameManialinkBrowser@ ManialinkBrowser`
- `const CGameManiaTitleControlScriptAPI@ TitleControl`
- `const CGamePlaygroundClientScriptAPI@ Playground`
- `const string ServerChatLog`
- `float MusicVolume`
- `int PluginZOrder`

## Inherited ManiaApp Surface

- `UILayerCreate`
- `UILayerDestroy`
- `UILayerDestroyAll`
- `LayerCustomEvent`
- `OpenLink`
- `Video`
- `Audio`
- `Http`
- `ScoreMgr`

## tm2020-mcp Implications

- This is a strong candidate context for Kacky admin utilities because it exposes current
  server, map, players, chat log, browser, and title-control state.
- `ClipboardSet` could support safe Interface Designer fragment copy flows.
- `CurrentServerPlayers`, `CurrentMap`, and server metadata are obvious next targets for
  status and diagnostics endpoints.
- `PluginZOrder` and `UILayerCreate` matter if the bridge starts drawing admin UI layers
  directly.
