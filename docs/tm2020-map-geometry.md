# TM2020 Map Geometry

What the block grid actually means, measured from real maps rather than inferred from the
API. This is the single place these conventions are encoded; `src/Tm2020Mcp/Maps/DummyTrackBuilder.cs`
applies them and cites this file.

Last reviewed: 2026-08-28

## Why measured

The editor API cannot answer these questions. `PlaceBlock` returns whether a block *fits* in
a cell, never whether the road *connects*, so a fully reversed track reports four successful
placements and looks like success in every log. Three placement attempts driven by reasoning
about "North" produced three wrong tracks; parsing maps that humans built settled it in one
pass.

## Method

[GBX.NET](https://github.com/BigBang1112/gbx-net) 2.4.4 (`Gbx.ParseNode<CGameCtnChallenge>`,
with `Gbx.LZO = new Lzo()` — the body is LZO-compressed and parsing fails without it).

Corpus:

- 450 Kacky Reloaded maps (KR1-KR6), grid-built, 1,349 `RoadTechStraight` and 256
  `RoadTechStart`/`RoadTechFinish` blocks.
- 50 official campaign maps (Spring 2026, Summer 2026). **These turned out not to answer the
  question**: their racing lines are built from *free blocks* (92 of 96 road-ish blocks in one
  sampled map), which carry an absolute position and rotation instead of a grid coord and a
  cardinal direction. Useful for terrain and deco vocabulary, not for grid conventions.

For each block the analysis asks which neighbouring cell holds another road block, grouped by
the block's stored `Direction`. A start or finish has exactly one road exit, so its neighbour
reveals the forward vector including sign; a straight is symmetric and reveals only the axis.

## Direction to world vector

`CGameEditorPluginMap::ECardinalDirections` is a rotation index, not a compass bearing. For
the `RoadTech` family, forward is:

| `dir` | Forward | Start: road sits at | Finish: road arrives from |
| --- | --- | --- | --- |
| `North` | **+Z** | +Z (15 cases, 0 at -Z) | -Z (10) |
| `South` | **-Z** | -Z (17, 0 at +Z) | +Z (12) |
| `East` | **-X** | -X (37, 0 at +X) | +X (17) |
| `West` | **+X** | +X (19, 0 at -X) | -X (9) |

Straight-block chaining agrees on the axis and is far better sampled: `North` n=305 chains
161/159 along ±Z with **zero** along ±X; `East` n=329 chains 163/167 along ±X.

Two traps in that table:

- `North` is `+Z`. The intuitive reading — north as "up the map", decreasing Z — is backwards.
- `East` is `-X` and `West` is `+X`, mirrored from what the names suggest. Getting the axis
  right and the sign wrong yields a start block facing away from its own track, which is
  what a wrong dummy map looks like.

## Rotation is per model, not global

Two block models with the same `dir` do not necessarily point the same way. Measured on the
campaign corpus:

| Block | `North`/`South` chain along | `East`/`West` chain along |
| --- | --- | --- |
| `DecoWallSlope2Straight` | Z | X |
| `PlatformTechWallStraight` | Z | X |
| `WaterGrassZoneStraight` | Z | X |
| `SnowRoadPillarStraight` | Z | X |
| `StandStraight` | **X** | **Z** |
| `DecoHillSlope2Straight` | **X** | **Z** |

`StandStraight` (grandstands) and `DecoHillSlope2Straight` are rotated 90 degrees from the
road-aligned families. So the table above is a fact about `RoadTech*`, not a global rule:
verify any new block family against the corpus before assuming it follows.

## Other measured facts

- **Ground level is y=9** in a `48x48Screen155Day` map. The bridge does not hardcode it: a
  negative `y` in `POST /map/blocks` triggers an upward `CanPlaceBlock` scan, which found 9
  on its own and will keep working if another decoration differs.
- `RoadTechStart`, `RoadTechStraight` and `RoadTechFinish` are valid TM2020 Stadium block
  names, placed at `variant=0 sub=0`.
- `SaveMap("MCP/dummy.Map.Gbx")` writes `Documents/Trackmania/Maps/MCP/dummy.Map.Gbx` and
  creates missing subdirectories. The map's internal `MapName` stays `Unnamed`; file name and
  map name are separate.

## What the corpus says about Kacky maps

Measured over the same 450 maps, because "generate a Kacky map" turns on these numbers:

| | |
| --- | --- |
| Grid blocks per map | min 0, median 1,290, max 45,534 |
| Free blocks per map | min 0, median 94, max 8,702 |
| Decoration | `48x48Day` (245), `48x48Screen155Day` (66), `48x48Sunrise` (24), `48x48Sunset` (15) |
| Map size | `<48, 40, 48>`, with a handful at `<48, 255, 48>` and one `<128, 128, 128>` |
| Road-block height | y from 4 to 246, median 19 |
| Maps with a grid start block | 174 of 450; the other 276 place the start as a free block |

The vocabulary is not road at all. The most-used blocks are `PlatformBase` (28,738),
`DecoPlatformBase` (24,014), `DecoPlatformIceBase` (15,305) and
`PlatformPlasticWallStraight4` (12,276). `RoadTechStraight` appears 1,476 times, in 102 maps.
A Kacky map is a platform gauntlet, not a track.

### Why an adjacency walk cannot validate one

Of the 174 maps with a grid start, the connection-model walk completes **0**. The dominant
failure is "next cell empty", 144 times. That is not a bug in the walker: **the gaps are the
design**. A Kacky route jumps between platforms, so grid adjacency stops describing it as
soon as the car leaves the ground. Validating one needs a physics model of the car, which
nothing in this repo has.

So the honest split:

- Generated grid tracks: verifiable here, end to end, without opening the game.
- Hand-built Kacky maps: not verifiable here. Reading their blocks, vocabulary and
  statistics works fine; judging whether a jump lands does not.

## Learned connection model

`BlockConnectionModel` counts, per (block name, direction), which neighbouring cells hold
another grid block, and keeps offsets that clear a share of that block's sightings. It
recovers block *shape* without anyone declaring it: one offset for a start or finish, two
opposite for a straight, two perpendicular for a curve.

Run against the corpus at variant 0, restricted to the road family, it independently
reproduced every value in the direction table above, and added the curve shapes nobody had
measured:

```
RoadTechStart    North -> +Z        RoadTechCurve1 North -> -X +Z
RoadTechStart    East  -> -X        RoadTechCurve1 East  -> -X -Z
RoadTechFinish   North -> -Z        RoadTechCurve1 South -> +X -Z
RoadTechStraight North -> +Z -Z     RoadTechCurve1 West  -> +X +Z
```

Note `RoadTechFinish North -> -Z` is the side the road *arrives from*, the reverse of the
forward vector. Confusing the two puts the finish in backwards, which is a mistake this repo
has already made once.

The model shipped at `src/Tm2020Mcp/Maps/block-connections.json` (64 entries, ~4 KB) was
learned this way. Regenerate it with `learn_map_block_connections`. Two settings matter:

- **Variant 0 only.** `PlaceBlock` places variant 0, and a curve's variant changes its shape,
  so averaging across variants produces a model that does not describe what the bridge builds.
- **`keepRatio` around 0.35.** Kacky maps run routes alongside each other; a lower threshold
  invents connections through walls, a higher one loses curve exits.

## Generating a route

`RouteBuilder` chains blocks whose learned shapes fit: at each step it takes the blocks that
connect back the way the route arrived, prefers a straight or a curve by `TurnChance`, and
follows the chosen block's other exit. It stays in bounds, never reuses a cell, and unwinds
its own tail when there is no room left for a finish.

Measured end to end on 2026-08-28: 20 of 20 seeds produced routes the verifier accepts, and
seed 183 (62 blocks) placed 62/62 through the bridge, saved, and verified connected when
parsed back off disk.

What it does not do: model a car. A generated route connects, fits, and ends properly. It is
not fun, not hard, and not Kacky.

## Trick blocks, and what is still out of reach

The road family carries more than plain tech road, and the learned model reads their shapes
cleanly. Everything below has at least one direction whose shape is unambiguous, which is
what `RoutePalette.Tricks` is built from:

| Shape | Blocks |
| --- | --- |
| Straight | `RoadTechStraight`, `RoadBumpStraight`, `RoadWaterStraight`, `RoadDirtStraight`, `RoadIceWithWallStraight`, `RoadTechSpecialTurbo`, `RoadTechSpecialTurbo2`, `RoadTechSpecialNoEngine`, `RoadTechSpecialReset` |
| Curve | `RoadTechCurve1`, `RoadBumpCurve1`, `RoadIceCurve1`, `RoadWaterCurve1` |

Placed live on 2026-08-29: a 20-block trick route with 12 distinct blocks - no-engine, turbo,
ice, water, dirt and bump surfaces - placed 20/20 and verified connected off disk.

### Flips and loops: learned and stamped

A loop is not one block, it is a motif. Probing the 3D neighbourhood of the 68 North-facing
`PlatformTechLoopStart` blocks in KR1-KR3:

```
80%  <-1, 0,  0>  PlatformTechLoopStart     five-wide wall of loop-start blocks
80%  <+1, 0,  0>  PlatformTechLoopStart
70%  <+-2, 0, 0>  PlatformTechLoopStart
73%  < 0, 0, -1>  PlatformTechBase          base row in front of it
52%  < 0,-1, -1>  StructureStraight         support underneath
```

`GateSpecialReset` behaves the same way: runs of two or three along the travel axis (70% at
+1, 50% at +2), a curtain rather than a single block.

`MotifLearner` measures exactly this, `BlockMotif` rotates it, and `MotifStamper` places it
after checking the whole footprint. Stamped live on 2026-08-29: the loop motif placed
**10/10** into the editor.

Three things had to be learned the hard way, each from the bridge's per-block errors:

1. **Support is counted per (offset, block), then the dominant direction is chosen.** Keying
   on direction directly splits a row that is always present but laid in mirrored
   orientations, and drops it below the threshold. That is what made the base row vanish
   from the first version of the loop motif.
2. **`Structure*` and `DecoWall*` cannot be stamped back onto the grid.** `DecoWall*` returns
   "unknown block model" and `Structure*` is refused at every height tried, ground level and
   elevated alike, so both are excluded from motifs by default. Note what this does *not*
   mean: Deep Dip 2 contains 416 author-placed `StructureSupportDeadend` blocks. They are
   placed as **free blocks**, which the grid API cannot express. The exclusion is a fact
   about `PlaceBlock`, not about what a mapper can build.
3. **A motif learned from elevated structures needs lifting.** Loops sit high in real maps, so
   their support layer is below the anchor. Stamped at ground level that layer lands
   underground and the engine refuses it, leaving a half-built structure. The stamper lifts
   the whole motif so its lowest block rests on ground level, and says so.

What still is not solved: a loop stamped beside a route is scenery. Routing the driving line
*through* it means knowing the loop's drivable topology - where the car enters, where it comes
out, at what height - and that is not in the adjacency data. A motif is geometry, not a line.

## Free blocks, and the ceiling they expose

`MapBlock` now carries `Position` (world units) and `Rotation` (yaw, pitch, roll in radians)
for free blocks. A free block's grid `Coord` is reported as `<-1, 0, -1>`, so without this
everything about a modern map is invisible.

Measured on Deep Dip 1 and 2 (4,000 and 4,144 free blocks):

| | Deep Dip | Deep Dip 2 |
| --- | --- | --- |
| Free block height | -22 to 1,947 world units (levels -3 to 243) | -10 to 1,895 (levels -1 to 237) |
| Tilted (pitch or roll off zero) | 2,204 of 4,000 (**55%**) | 2,164 of 4,144 (**52%**) |

The rotation vocabulary is not arbitrary. Yaw clusters on multiples of 45 degrees - 0, +-45,
+-90, +-135, +-180 account for the overwhelming majority - with pitch used sparingly for
ramps (Deep Dip 2 has 121 blocks at pitch 15). Placement is free, but the *angles* are a small
discrete set.

Vertical density comes in bands: dense platform clusters separated by sparse climbing
sections, not a uniform spiral.

### What this means for building

One grid cell is 32 world units across and 8 tall, and half of a Deep Dip is placed off that
grid at angles the grid cannot express. The Openplanet editor plugin API has **no free-block
placement method at all** - every placement call on `CGameEditorPluginMapMapType` takes an
`int3 Coord` and an `ECardinalDirections`:

```
PlaceBlock / PlaceBlock_NoDestruction / PlaceGhostBlock   int3 + ECardinalDirections
PlaceRoadBlocks / PlaceTerrainBlocks                      int3 start + int3 end
PlaceMacroblock / PlaceMacroblock_AirMode / ...           int3 + ECardinalDirections
```

So the bridge has a hard ceiling: it can build grid tracks and stamp grid motifs, and it can
never build what Deep Dip is made of. Two ways past it, neither yet built:

1. **Macroblocks.** `PlaceMacroblock` takes a `CGameCtnMacroBlockInfo`, and a macroblock can
   contain free-placed geometry. Authoring macroblocks and placing them is the in-editor
   route.
2. **Write the .Map.Gbx directly.** `MapGbxWriter` does this: it opens a map, appends free
   blocks at explicit world positions and rotations, and saves a copy. The `write_free_blocks`
   tool exposes it, and it needs no running game.

   What the file needs per free block, all established by reading real maps:

   | Field | Value |
   | --- | --- |
   | `BlockModel` | `Ident(name, "", "")` - collection and author are empty on every TM2020 block ident in the corpus, so **no donor block is needed**; the model can be named outright |
   | `Flags` | `0x20000000`, observed on every free block in Deep Dip 1 and 2 |
   | `Coord` | `<-1, 0, -1>`, the placeholder the engine writes for a free block |
   | `AbsolutePositionInMap` | world units - 32 per cell across, 8 per level |
   | `YawPitchRoll` | radians; the writer wraps degrees into (-180, 180] first, the range real maps use |

   Verified on 2026-08-29: a twelve-block spiral of `PlatformIceWallStraight`, yawed in 45
   degree steps with alternating -90 degree roll, climbing half a cell per step, positions
   fractional (`835.88`) and deliberately off-grid. Written, re-parsed, every block intact.
   `Maps/MCP/spiral.Map.Gbx`.

   **The game accepts it.** Confirmed on 2026-08-29 by the strongest check available: the
   bridge's `POST /map/open` loaded `MCP/spiral.Map.Gbx` into the editor (`map_file_name`
   came back as `MCP\spiral.Map.Gbx`, so it really was that file), and then the *editor* saved
   it back out. Parsing the game's own save against the written original:

   | | written by GBX.NET | saved back by Trackmania |
   | --- | --- | --- |
   | blocks | 16, 12 free, 6 tilted | 16, 12 free, 6 tilted |
   | first block | `<864, 72, 768>` yaw 0 roll -90 | `<864, 72, 768>` yaw 0 roll -90 |
   | off-grid position | `<835.88, 76, 835.88>` yaw 45 | `<835.88, 76, 835.88>` yaw 45 |

   Fractional positions, 45 degree yaw steps and -90 degree rolls all survived the engine's
   own loader and writer unchanged. The file grew from 102,502 to 113,775 bytes with no change
   in block count - the engine adds its own data, not geometry.

   So the grid-only ceiling on the plugin API is not a ceiling on this project: anything Deep
   Dip does with free blocks can be written from outside the game and opened inside it.

### Deep Dip: the vertical corpus

`Deep Dip` and `Deep Dip 2` are `<48, 255, 48>` maps of 16,205 and 18,217 blocks, with grid
blocks from y=8 to y=254. They are the right corpus for vertical motifs, and they confirm the
finding above at a different scale: their most common blocks are `DecoWallBasePillar`,
`StructureSupportDeadend`, `StructureBase` and `StructurePillar` - all engine-generated.

Their most common free blocks are `StructureSupportDeadend`, `StructureBase` and
`StructureSupportCurve0Out`, and 29-78% of each of those families is tilted: the scaffolding
of the tower is itself angled geometry, not stacked boxes.

### Length cannot be measured from this corpus

**Zero of 450** KR maps carry an `AuthorTime`; Kacky maps ship unvalidated. So a target like
"max 10 seconds" cannot be checked against the corpus, and nothing here simulates a car. Route
length is a block count, not a time, and any claim about how long a generated map takes to
drive is a guess until someone drives it.

## Verifying a generated track

Read the saved `.Map.Gbx` back and check each consecutive pair against the table, which is
what confirmed `dummy2.Map.Gbx`:

```
RoadTechStart    <24, 9, 24> dir=North   -> forward +Z -> <24,9,25> holds a straight
RoadTechStraight <24, 9, 25> dir=North
RoadTechStraight <24, 9, 26> dir=North
RoadTechFinish   <24, 9, 27> dir=North   -> arrives from -Z = <24,9,26>
```

This is a real check the bridge cannot perform on its own, and it is cheaper than a human
looking at the editor.
