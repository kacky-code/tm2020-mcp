# Game.CGameEditorPluginMap

Source: https://next.openplanet.dev/Game/CGameEditorPluginMap
Last reviewed: 2026-08-28

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

## Map Building Surface

Reached as `cast<CGameCtnEditorFree>(GetApp().Editor).PluginMapType`, which is declared as
`const CGameEditorPluginMapMapType@` and inherits this class.

```
CGameCtnBlockInfo@ GetBlockModelFromName(wstring BlockModelName)
CGameCtnBlockInfo@ GetTerrainBlockModelFromName(wstring TerrainBlockModelName)
bool CanPlaceBlock(CGameCtnBlockInfo@ BlockModel, int3 Coord, ECardinalDirections Dir, bool OnGround, uint VariantIndex)
bool PlaceBlock(CGameCtnBlockInfo@ BlockModel, int3 Coord, ECardinalDirections Dir)
bool RemoveBlock(int3 Coord)
CGameCtnBlock@ GetBlock(int3 Coord)
CGameCtnBlock@ GetStartLineBlock()
void SaveMap(wstring FileName)
void AutoSave()
void Validate()
bool SetMapType(wstring MapType)
const wstring MapName
const wstring MapFileName
const EValidationStatus ValidationStatus
```

`ECardinalDirections` is nested and must be written in full:
`CGameEditorPluginMap::ECardinalDirections::North` (also `East`, `South`, `West`).

Notes that cost time to learn:

- `GetBlockModelFromName` returns null for an unknown name. It does not throw, so a typo in a
  block name looks exactly like a placement that quietly did nothing. `POST /map/blocks`
  reports per-block failures for this reason.
- `PlaceBlock` returns false when the engine refuses the coordinate. The Y that counts as
  "ground" depends on the decoration, so the bridge scans upward with `CanPlaceBlock` when a
  request passes a negative Y instead of hardcoding a height.
- `SaveMap` returns void *and* completes asynchronously. Reading `MapFileName` straight
  after the call returns an empty string, so the bridge waits for it to fill in and reports
  `saved` from that rather than from the call returning.

Settled by a live run on 2026-08-28 (a start/straight/finish placed at x=24, z=24..22, then
saved):

- `RoadTechStart`, `RoadTechStraight` and `RoadTechFinish` are real TM2020 Stadium block
  names; all three placed first try. Note what that does *not* mean: `PlaceBlock` returning
  true says the block fit in the cell, never that the road connects. The first track placed
  this way was fully reversed and still reported three successes. Connectivity is settled in
  [tm2020-map-geometry.md](../tm2020-map-geometry.md), by parsing maps rather than by asking
  the editor.
- Ground level in a `48x48Screen155Day` map is **y=9**. The upward `CanPlaceBlock` scan found
  it on its own, which is why the bridge does not need the constant hardcoded.
- `SaveMap`'s file name is relative to the user Maps folder and creates missing
  subdirectories: `"MCP/dummy.Map.Gbx"` produced
  `Documents/Trackmania/Maps/MCP/dummy.Map.Gbx` (102 KB).
- The map's internal `MapName` stays `"Unnamed"`. The file name and the in-map name are
  separate, and nothing in this surface sets the latter.

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
- The block/save members above back the `create_map` MCP tool and the `/map/*` bridge
  endpoints. See [Game.CGameManiaTitleControlScriptAPI.md](Game.CGameManiaTitleControlScriptAPI.md)
  for the call that opens the editor in the first place.

## Known Risk

Interface Designer paste/import is stricter than map-editor preview. Full generated XML
with wrappers, model instances, raw unescaped symbols, and runtime script attributes may
preview in one context but crash or fail in the Designer context.
