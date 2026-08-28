# Handoff: settle five map-editor facts with the bridge

Status: not started. Created 2026-08-28. No owner.

## Why this exists

A plugin author (PuzzleMode, a challenge-quota tool for the map editor) was given five answers
on 2026-08-28 that we could not verify, only infer from the class reference at
`next.openplanet.dev` (doc build 2026-01-26). Each hedge is a fact a running client settles in
seconds. This handoff is the work to stop hedging.

The questions are all `CGameCtnChallenge` / `CGameCtnCollector` shaped. None of them are
ManiaLink questions, so MLHook has nothing to say about them and is not an alternative here.

## The five questions

| # | Question | Expression to evaluate | Reading the result |
|---|---|---|---|
| E1 | Does `ArticlePtr` reach the inventory article? | `cast<CGameCtnArticle>(block.BlockInfo.ArticlePtr)` | non-null means a placed block can be walked back to its article, which is what an "extras" list needs to show names and a Select button |
| E2 | Is `BlockInfo` ever null on a placed block? | `block.BlockInfo is null` over a full session including a map load | if it never fires, null guards on the hot scan path are dead weight |
| E3 | Does `GetCollectorNod()` work before the article loads? | `Article.IsLoaded` paired with `cast<CGameCtnCollector>(node.GetCollectorNod()) is null` | if null while unloaded, model handles must resolve lazily rather than in a constructor |
| E4 | Does the engine clear block units on delete? | `BlockUnitsE.Length` and `BlockUnits.Length` on a held handle after `RemoveBlock` | PuzzleMode currently uses `BlockUnitsE.Length == 0` as its deleted-test. Confirm or kill it |
| E5 | Does `GetBlock(coord)` identity flip after a delete? | hold handle, `RemoveBlock(coord)`, then `GetBlock(coord)` | null or a different handle confirms the O(1) liveness check we recommended |

E1 to E3 are read-only. **E4 and E5 place and delete blocks, so run them on a scratch map.**

Same asymmetry applies throughout: items have no coord lookup and no handle-based removal
(`RemoveItem` takes `CGameCtnEditorScriptSpecialProperty@`, not `CGameCtnAnchoredObject@`), so
E4 and E5 are block-only by construction.

## What the bridge does today

Nothing that touches the map. `openplanet-plugin/TM2020Bridge/Main.as` serves eight endpoints and
all of them are status, save, or ManiaLink. This is a new direction, not a config change. The
whole of `WAYFINDER-MAP.md` is ManiaLink readback and does not cover it.

The hard constraint: AngelScript compiles at plugin load, so there is no eval. An agent cannot
post a snippet and get an answer. Anything the bridge answers has to be a fixed endpoint written
ahead of time.

## Path A: throwaway probe (do this first)

`openplanet-plugin/_W7Probe/` is the pattern. A folder, an `info.toml`, a menu item, output to
`Openplanet.log`. No .NET work, no endpoint, no contract to maintain, and it answers all five.

Skeleton, unverified AngelScript, it has never been compiled:

```angelscript
// THROWAWAY probe for HANDOFF-editor-facts E1-E5. Delete once the answers are recorded.
// E4/E5 MUTATE THE MAP. Scratch maps only.

void Main() { }

CGameEditorPluginMap@ GetPmt()
{
    auto editor = cast<CGameCtnEditorFree>(GetApp().Editor);
    return editor is null ? null : editor.PluginMapType;
}

void DumpBlockFacts()
{
    auto pmt = GetPmt();
    if (pmt is null) { print("E: not in the map editor"); return; }
    auto map = pmt.Map;
    if (map is null) { print("E: no map"); return; }

    uint noInfo = 0, noArticle = 0, total = map.Blocks.Length;
    for (uint i = 0; i < total; i++) {
        auto bi = map.Blocks[i].BlockInfo;
        if (bi is null) { noInfo++; continue; }                     // E2
        if (cast<CGameCtnArticle>(bi.ArticlePtr) is null) noArticle++; // E1
    }
    print("E1/E2: blocks=" + total + " nullBlockInfo=" + noInfo + " unresolvedArticle=" + noArticle);
}
```

E3 walks `pmt.Inventory.RootNodes` and prints `IsLoaded` against whether `GetCollectorNod()`
casts. E4 and E5 need a place, hold, remove, re-read sequence on a known free coord.

## Path B: promote to an endpoint (only if the questions recur)

Do not build this to answer E1 to E5. Build it when a second round of map questions shows up.

Shape, following the existing bridge conventions (localhost only, JSON response, matching .NET
client method, MCP tool only if an agent would actually call it):

```
GET /editor/elements
-> { "inEditor": bool, "blocks": [ { "coord": [x,y,z], "model": string,
                                     "articleResolved": bool, "units": int } ], ... }
```

Distinguish the failure modes rather than returning an empty list: "no client", "not in the
editor", "no map open" and "editor open, zero blocks" are four different answers and merging
them into `[]` reads as success when nothing worked. W9 already learned this the hard way.

## Verification

Per `AGENTS.md`: `dotnet build` then `dotnet test --no-build` for anything on the .NET side. The
AngelScript cannot be checked by dotnet, so reload the plugin in Openplanet and read
`Openplanet.log`. If the live check was not possible, say so instead of implying it passed.

Reminder that the bridge is an unsigned plugin: it needs Developer Mode, which needs Club Access.
This is our verification tool, not something a plugin author installs to get an answer from us.

## When the answers land

Record them in this file with the date and the client version, then fold the confirmed ones into
`docs/openplanet/` with their source URLs. Delete the probe folder. E4 in particular should end
as a plain yes or no, because someone else's plugin currently depends on the guess.

## Out of scope

Nadeo's live ManiaLink (that is MLHook's job, declare it as a dependency rather than
reimplementing it), item removal, and plugin signing.
