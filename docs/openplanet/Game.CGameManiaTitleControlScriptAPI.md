# Game.CGameManiaTitleControlScriptAPI

Source: https://next.openplanet.dev/Game/CGameManiaTitleControlScriptAPI
Last reviewed: 2026-08-28

## Role

The title-layer control API. It is what launches the map editor, so it is the only way for a
plugin to create a map that does not exist yet. Reached from the app object, which must be
cast first:

```angelscript
auto app = cast<CGameManiaPlanet>(GetApp());
auto titleApi = app.ManiaTitleControlScriptAPI;
```

`ManiaTitleControlScriptAPI` is declared on `CGameManiaPlanet` as
`const CGameManiaTitleControlScriptAPI@` and is inherited by `CTrackMania`.

## Map creation members

```
void EditNewMap1(string Environment, string Decoration, wstring ModNameOrUrl, wstring PlayerModel, wstring MapType, wstring EditorPluginScript, string EditorPluginArgument)
void EditNewMap2(string Environment, string Decoration, wstring ModNameOrUrl, wstring PlayerModel, wstring MapType, bool UseSimpleEditor, wstring EditorPluginScript, string EditorPluginArgument)
void EditNewMap3(string Environment, string Decoration, wstring ModNameOrUrl, wstring PlayerModel, wstring MapType, bool UseSimpleEditor, MwFastBuffer<wstring>& EditorPluginsScripts, MwFastBuffer<wstring>& EditorPluginsArguments)
void EditNewMap4(string Environment, string Decoration, wstring ModNameOrUrl, wstring PlayerModel, wstring MapType, bool UseSimpleEditor, MwFastBuffer<wstring>& EditorPluginsScripts, MwFastBuffer<wstring>& EditorPluginsArguments, bool OnlyUseForcedPlugins)
void EditNewMapFromBaseMap(wstring BaseMapName, wstring ModNameOrUrl, wstring PlayerModel, wstring MapType, wstring EditorPluginScript, string EditorPluginArgument)
void EditNewMapFromBaseMap2(wstring BaseMapName, string Decoration, wstring ModNameOrUrl, wstring PlayerModel, wstring MapType, wstring EditorPluginScript, string EditorPluginArgument)
void EditNewMapFromBaseMap3(wstring BaseMapName, string Decoration, wstring ModNameOrUrl, wstring PlayerModel, wstring MapType, MwFastBuffer<wstring>& EditorPluginsScripts, MwFastBuffer<wstring>& EditorPluginsArguments, bool OnlyUseForcedPlugins)
```

Existing maps are opened with the `EditMap` / `EditMap2` … `EditMap5` family, same shape but
taking a map file instead of an environment.

## tm2020-mcp Implications

- The bridge's `POST /map/new` calls `EditNewMap2`. It is the lowest arity overload that still
  lets us say "not the simple editor", and it avoids `MwFastBuffer` arguments, which are
  awkward to construct from a plugin.
- **All of it is void.** None of these report whether the editor actually opened, so the
  bridge waits for `cast<CGameCtnEditorFree>(GetApp().Editor)` to become non-null instead of
  trusting the call, and the MCP tool polls `/status` after that.
- Loading is asynchronous and can take several seconds, which is longer than the .NET client's
  5s HTTP timeout. That is why creation and block placement are separate endpoints rather than
  one long request.
- The string arguments are engine names, and a wrong one fails silently: the call returns, no
  editor opens, and the only symptom is the wait timing out. Defaults used by the bridge are
  `Environment = "Stadium"`, `Decoration = "48x48Screen155Day"`,
  `MapType = "TrackMania\TM_Race"`, all overridable per request.

## Verified live

Run against a real client on 2026-08-28, from the main menu with no editor open:

```
POST /map/new {}
-> {"created":true,"map_editor":true,"environment":"Stadium",
    "decoration":"48x48Screen155Day","map_type":"TrackMania\\TM_Race","waited_frames":19}
```

So the three default strings are correct for TM2020, and the editor was up 19 frames
(roughly a third of a second) after the call. The frame wait still matters: the call itself
returns before the editor exists, and a wrong decoration would look identical here apart
from `map_editor` staying false.
