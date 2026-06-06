# Game.CGameScriptHandlerPlaygroundInterface

Source: https://next.openplanet.dev/Game/CGameScriptHandlerPlaygroundInterface
Last reviewed: 2026-06-06

## Role

`CGameScriptHandlerPlaygroundInterface` is the in-game ManiaLink script handler surface
for game-mode UI. It inherits from `CGameManialinkScriptHandler`.

## Relevant Methods

- `void ShowCurChallengeCard()`
- `void ShowModeHelp()`
- `void CopyServerLinkToClipBoard()`
- `void RequestSpectatorClient(bool Spectator)`
- `void SetSpectateTarget(string Player)`
- `void ShowProfile(string Player)`
- `void ShowInGameMenu()`
- `void CloseScoresTable()`
- `void PlayUiSound(CGameScriptHandlerPlaygroundInterface::EUISound Sound, int SoundVariant, float Volume)`
- `void Spectate(string Player)`

## Inherited ManiaLink Handler Methods

- `void Dbg_SetProcessed(CGameManialinkScriptEvent@ Event)`
- `bool IsKeyPressed(int KeyCode)`
- `void EnableMenuNavigation(...)`
- `void EnableMenuNavigation2(...)`
- `void OpenLink(string Url, CGameManialinkScriptHandler::ELinkType LinkType)`
- `void TriggerPageAction(string ActionString)`
- `void SendCustomEvent(wstring Type, MwFastBuffer<wstring>& Data)`
- `void PreloadImage(string ImageUrl)`
- `void PreloadAll()`

## Relevant Members

- `const CGamePlaygroundClientScriptAPI@ Playground`
- `const CGamePlaygroundUIConfig@ UI`
- `const CGamePlaygroundUIConfig@ ClientUI`
- `const CGameCtnChallenge@ Map`
- `string CurrentServerLogin`
- `wstring CurrentServerName`
- `wstring CurrentServerDesc`
- `string CurrentServerJoinLink`
- `wstring CurrentServerModeName`
- `const CGameManialinkPage@ Page`
- `const bool PageIsVisible`
- `const MwFastBuffer<CGameManialinkScriptEvent@> PendingEvents`
- `const CNetScriptHttpManager@ Http`
- `const CGameVideoScriptManager@ Video`
- `const CAudioScriptManager@ Audio`

## tm2020-mcp Implications

- This is the most relevant page for "button in a ManiaLink triggers something in-game".
- Button handling should probably flow through ManiaLink script events, custom events, or
  page actions.
- `PlayUiSound`, `ShowProfile`, `Spectate`, and server/map members are useful for Kacky
  admin UX experiments.
- The exposed `Video` manager makes a GPS-video experiment worth investigating, but it
  should be tested as script behavior rather than assumed to work in Interface Designer
  static XML.

## Practical GPS Button Shape

Possible flow to test:

1. Render a static `label` or `quad` with `scriptevents="1"`.
2. Handle the click in ManiaScript/OpenPlanet context.
3. Trigger a game-supported GPS flow, open a link, or show instructions.
4. Avoid trying to paste a video player as a static Interface Designer node until the
   runtime video API is proven.
