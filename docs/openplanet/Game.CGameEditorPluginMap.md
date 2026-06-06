# Game.CGameEditorPluginMap

Source: https://next.openplanet.dev/Game/CGameEditorPluginMap
Last reviewed: 2026-06-06

## Role

`CGameEditorPluginMap` is the map editor plugin API surface. It inherits from
`CGameManiaApp` and exposes editor state, map/editor members, and ManiaLink preview
members.

## Relevant Members

- `string ManialinkText`
- `const CGameManialinkPage@ ManialinkPage`
- `const bool IsTesting`
- `const bool IsValidating`
- `const CGameEditorPluginCameraAPI@ Camera`
- `const CGameEditorPluginCursorAPI@ Cursor`
- `const MwFastBuffer<CGameCtnBlock@> Blocks`
- `const MwNodPool<CGameCtnEditorScriptAnchoredObject@> Items`
- `const MwFastBuffer<CGameEditorMapScriptClipList@> FixedClipLists`
- `const MwFastBuffer<CGameEditorMapScriptClipList@> FrameClipLists`

## Inherited ManiaApp Surface

Because this inherits `CGameManiaApp`, it also exposes UI layer helpers and managers such
as:

- `UILayerCreate`
- `UILayerDestroy`
- `LayerCustomEvent`
- `OpenLink`
- `Video`
- `Audio`
- `Http`
- `Xml`

## tm2020-mcp Implications

- The current OpenPlanet bridge uses the map-editor ManiaLink preview path. Keep this
  distinction clear: it is not the same as Interface Designer paste/import.
- `ManialinkText` is the important member for map-editor preview.
- `ManialinkPage` could become useful for inspecting currently rendered preview controls.
- Editor state members can support future admin/map validation tools.

## Known Risk

Interface Designer paste/import is stricter than map-editor preview. Full generated XML
with wrappers, model instances, raw unescaped symbols, and runtime script attributes may
preview in one context but crash or fail in the Designer context.
