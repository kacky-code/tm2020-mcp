# Wayfinder Map: ManiaLink render-readback round trip

**Destination:** a decision-complete spec for a loop where the MCP pushes ManiaLink XML into
Trackmania 2020, observes what the client actually did with it, and feeds that back to an agent
so a HUD can be iterated on without a human watching the screen. **Implementation is OUT OF
SCOPE.** No code ships from this map.

**Store:** local (`WAYFINDER-MAP.md`)

## Givens (settled before charting, do not re-litigate)

- Windows-only is acceptable, provided setup is easy. Stated by the human.
- The payoff target is kontrol HUD development, not just this repo's own lab work.
- `kacky-code/tm2020-mcp` is already a public repo.
- The bridge can already read live game state: `GetInterfaceDesignerSelectionJson` pulls ~25
  properties off `CGameEditorManialink`. Readback is proven in principle.
- `PluginMapType.ManialinkPage` exists and `docs/openplanet/Game.CGameEditorPluginMap.md:44`
  already flags it as the likely hook. Nobody followed up.
- **The KackyGG docker dev-server does not make this redundant.** `evoesports/trackmania` is the
  headless dedicated-server image: it can exercise mode logic, callbacks and XML-RPC, but it
  contains no renderer and can never report that a quad drew as a grey box. The two test disjoint
  things. This is the scoping question that had to be answered before the map was worth working,
  and it is settled.
- ManiaScript gamemodes render their own HUDs (`KackyKOTD.Script.txt` carries 5 ManiaLink/UI
  references), and the visual half of KOTT is currently unverified. `KackyKOTTGulagShot.Script.txt`
  is a whole staging mode built by hand just to produce a screenshot. That manual loop is prior
  art for what this feature would automate.

## Out of scope

- Anything kontrol already does. The MCP does not duplicate server commands.
- Replacing the validator shipped in `545a66e`. This map is about the *live* half.
- Hosting or any non-local service.

## Tickets

- [x] W1 · RESEARCH · Does `CGameManialinkPage` expose a walkable control tree in OpenPlanet's
      AngelScript API, with per-control computed position, size and visibility? · blocks: W5, W7
- [x] W2 · RESEARCH · Is there any engine signal that a media URL failed to load, or must
      failure be inferred? Make-or-break for "the image at URL X never loaded". · blocks: W5, W6
- [x] W3 · RESEARCH · Which of the three render contexts can the bridge observe, and do they
      agree? Map-editor preview, Interface Designer, and a client connected to a server.
      DESIGN.md already warns they differ. If editor preview does not predict server HUD,
      the round trip validates the wrong thing and both the kontrol and gamemode payoffs
      evaporate. · blocks: W6, W10
- [x] W4 · RESEARCH · What is the cheapest path from a kontrol TSX component to an XML string
      this MCP can consume: an existing render entry point, a test hook, or a new export? ·
      blocks: W6
- [x] W5 · DECISION · What does "observe" mean concretely: a control-tree dump, a diff against
      the submitted XML, a list of anomalies, or a screenshot? · blocked-by: W1, W2
- [x] W6 · DECISION · Who is this for, and does it target editor preview or server HUD? Decides
      whether this is a local lab tool or a kontrol development loop. · blocked-by: W2, W3, W4
- [ ] W7 · PROTOTYPE · Throwaway AngelScript that walks a rendered page and dumps the tree
      against a real client. Turns W1's paper answer into a real one. · blocked-by: W1
- [~] W8 · DECISION · What "easy setup" means for a Windows-only bridge on a public repo:
      release zip, installer, or OpenPlanet plugin-manager listing. · blocked-by: W6
- [x] W9 · DECISION · How an agent drives the loop unattended: validate, push, read back,
      correct, stop. Including the failure mode when no client is running. · blocked-by: W5, W7

- [x] W10 · RESEARCH · Do ManiaScript gamemode HUDs (KOTT gulag countdown, elimination, revival
      banner) render through a path the bridge can observe on a client connected to the local
      docker dev-server? This is the gamemode payoff and it subsumes the kontrol one, since both
      are server-delivered HUD layers rather than editor previews. · blocked-by: W3 · blocks: W6

## Resolutions

### W1 — RESOLVED YES. The rendered page is fully walkable.

`CGameManialinkPage` exposes:

- `const CGameManialinkFrame@ MainFrame` — the root control
- `const MwFastBuffer<CGameManialinkControl@> ControlsCache` — a **flat cache of every control**,
  so no recursive descent is needed
- `CGameManialinkControl@ GetFirstChild(string ControlId)` — lookup by id
- `void GetClassChildren(string Class, CGameManialinkFrame@ Frame, bool Recursive)` with results in
  `GetClassChildren_Result`
- `const CGameManialinkControl@ FocusedControl`, `const string Url`

Per control, `CGameManialinkControl` gives everything a readback needs:

- identity: `const string ControlId`, `const MwSArray<string> ControlClasses`
- geometry: `const vec2 AbsolutePosition_V3`, `const vec3 AbsolutePosition`, `vec3 RelativePosition`,
  `vec2 Size`, `float ZIndex`, `float Scale`, `const float AbsoluteScale`
- state: `bool Visible`, `const bool IsFocused`

The page does **not** expose its own XML, so a readback compares the live tree against the XML the
MCP submitted rather than against anything the client hands back.

Source: https://next.openplanet.dev/Game/CGameManialinkPage,
https://next.openplanet.dev/Game/CGameManialinkControl

### W2 — RESOLVED YES, by inference rather than an error flag.

`CGameManialinkQuad` has **no** load-failure or error member. It does have:

- `const bool DownloadInProgress`
- `CPlugBitmap@ Image`
- `string ImageUrl`, `string ImageUrlFocus`, `string AlphaMaskUrl`, `void ChangeImageUrl(string)`

Those two combine into a deterministic tri-state per quad:

| `DownloadInProgress` | `Image` | meaning |
|---|---|---|
| true | any | still loading, poll again |
| false | not null | loaded |
| false | null | **failed to load** |

This is the headline capability of the whole feature and it is real: no timing heuristic and no
pixel inspection required, only polling until `DownloadInProgress` clears. It does mean the
readback is asynchronous by nature, which W5 and W9 have to account for.

Source: https://next.openplanet.dev/Game/CGameManialinkQuad


### W3 + W10 — RESOLVED TOGETHER, and they flip the design.

Server-delivered HUD layers are observable, through the same mechanism as W1 and with **more**
information available than the map-editor path. The chain is:

`CGameManiaAppPlayground.UILayers` (`const MwFastBuffer<CGameUILayer@>`, inherited from
`CGameManiaApp`) -> per layer `const CGameManialinkPage@ LocalPage` -> the W1 tree walk.

`CGameUILayer` also gives, per layer:

- `wstring ManialinkPage` / `string ManialinkPageUtf8` — **the layer's XML source**. The page
  object itself does not expose its XML (W1), so on this path a readback can diff submitted
  against actual XML directly, which the editor path cannot do.
- `bool IsVisible`, `const bool AnimInProgress`
- `const bool IsLocalPageScriptRunning` — whether the layer's ManiaScript is actually running.
  This is the live counterpart to the validator's static `script.events-without-script` check.
- `CGameUILayer::EUILayerType Type`, `string AttachId` — identify which layer is whose.

**Consequence for the spec:** build the readback on `UILayers`, not on
`PluginMapType.ManialinkPage`. The editor path was the obvious hook (and the one the cached docs
flagged) but it is the weaker of the two: it serves only this repo's own lab work, while the
playground path serves kontrol HUDs and ManiaScript gamemode HUDs, which are the stated payoff
targets. The editor path stays useful for previewing XML that is not attached to any server.

Both payoffs collapse into one mechanism, so W6's "editor preview or server HUD" question is
largely settled: server HUD, with editor preview as a secondary convenience.

**Still to confirm in W7:** that ManiaScript gamemode HUDs (created server-side via
`UIManager.UILayerCreate`) do surface in the client's `UILayers` buffer with a usable `AttachId`.
Consistent with the model, but unverified against a real client.

Source: https://next.openplanet.dev/Game/CGameManiaAppPlayground,
https://next.openplanet.dev/Game/CGameUILayer

### W4 — RESOLVED. No new export needed on the kontrol side.

`Manialink.render()` in `kontrol/core/ui/manialink.ts:58` is already a public
`async render(recipientOverride?: string): Promise<string>` returning the rendered XML. Its only
couplings are:

- `UIContext.instance` for settings, themes, colours, fonts, positions
- `tmc.players.getPlayer(targetRecipient)` — but that whole branch is guarded by
  `if (targetRecipient)`, so it is skipped entirely when rendering without a recipient

kontrol already establishes the harness pattern: `core/uimanager.test.ts` builds a
`createMockTmc()` with `players.getPlayer` stubbed. A headless "render this component to XML"
helper is a small harness over existing public API, not a new export or a refactor.

That makes the kontrol integration cheap: render to XML in kontrol, hand the string to this MCP,
validate it statically, push it to a client, read the layer back.

## Open tickets after this pass

All five RESEARCH tickets are closed (W1, W2, W3, W4, W10) and every one came back positive.
Remaining: four DECISIONs for the human (W5, W6, W8, W9) and one PROTOTYPE (W7), which needs a
Windows machine with Trackmania and OpenPlanet running.


### W5 — RESOLVED. Anomaly report by default, full tree on request.

The readback's default output is what looks **wrong**, not everything that exists:

- quads where `DownloadInProgress` is false and `Image` is null (the W2 tri-state), so the image failed
- controls with `Visible == false` that the XML expected to show
- controls with zero `Size`, or `AbsolutePosition_V3` outside the 320 x 180 space
- layers where `IsLocalPageScriptRunning` is false but the XML declares `scriptevents="1"`

**The key design property: it reuses the same finding vocabulary as the static validator shipped in
`545a66e`.** `media.image-format` is the static guess ("this .webp may be animated, verify
in-game"); the live readback either kills it or confirms it with the same code. Static and live
become two passes over one rule set rather than two unrelated tools, and the ambiguous cases the
validator can only warn about become decidable.

A full control-tree dump stays available as a separate tool, because an empty anomaly list on a
HUD that still looks wrong is exactly when you need the raw tree. It is opt-in because a real
kontrol widget is hundreds of controls and dumping it every iteration would burn context for
nothing.

Consequence for W9: the readback is asynchronous. `DownloadInProgress` and `AnimInProgress` both
have to clear before the anomaly list means anything, so the tool polls rather than answering
immediately.

### W6 — RESOLVED. kontrol contributors, on the server-HUD path.

**Context** was settled by W3/W10: build on `CGameManiaAppPlayground.UILayers`, with map-editor
preview kept as a secondary convenience for XML not attached to any server.

**Audience** is kontrol contributors: the human plus anyone building kontrol plugins or ManiaScript
gamemode HUDs. Not a general TM2020 plugin-dev tool yet, because that would mean supporting
strangers' setups before the feature has been proven once against a real client. The repo is
already public, so wider adoption can happen on its own later.

What choosing this audience commits us to:

- kontrol's `documentation/devs/` references the tool, so it is discoverable by someone who did not
  build it
- setup has to work for someone who did not build it (see W8)
- anomaly output has to be readable by someone who is not the author, which the shared
  finding-code vocabulary from W5 already helps with

**Constraint to carry into the spec:** reading `UILayers` observes the operator's own client while
it is connected to a server. Against the local docker dev-server that is clean. Pointed at a
production Kacky server it means watching a live event. This is a development-loop tool aimed at
dev servers, and the spec should say so in the tool description itself, not only in prose.

### W8 — RESOLVED. Tagged GitHub release carrying a `.op` bundle.

A release ships the plugin as a `.op` file (OpenPlanet's standard distribution format, a renamed
zip) alongside the MCP client config snippet, linked from kontrol's `documentation/devs/`. That is
one release workflow and it works for someone who does not have this repo checked out.

Deliberately **not** submitting to the OpenPlanet plugin manager yet. That is a public commitment
and a review process, and W7 has not yet proven the feature against a real client. It stays open as
the natural next step if the tool earns a wider audience.

### W9 — RESOLVED. One composite tool over primitives that mostly already exist.

The agent is pointed at a single tool that does the whole loop: validate the XML statically (the
`545a66e` validator), push it, poll until `DownloadInProgress` and `AnimInProgress` have cleared,
then return the W5 anomaly list. `preview_manialink_xml` and `clear_manialink_preview` already
exist as primitives; the layer readback becomes a new one; the composite is a thin wrapper.

The settling logic is the reason this is composite rather than three tools the agent strings
together. An agent that does not know it must wait for the download to finish will read a page
mid-load and report a false failure, and that mistake would be made once per agent forever.

**Failure modes must be distinguishable, not merged into one empty result.** "No client running",
"client running but not connected to a server", "layer not found", "settled with no anomalies" and
"settled with anomalies" are five different answers. Returning an empty anomaly list when the
bridge is simply dead is the single worst outcome available, because it reads as success.

### Note on Docker, attached to W8

Asked directly: can all of it run in Docker? No, and the boundary is inherent rather than a gap
to close.

| Piece | Docker | Why |
|---|---|---|
| MCP server (.NET) | yes | the Dockerfile already does this, reaching the bridge at `host.docker.internal:29100` |
| Dedicated server for mode logic | yes, already | `evoesports/trackmania`, headless |
| **Game client + OpenPlanet** | **no** | needs a GPU, a display and an Ubisoft/Epic login; OpenPlanet hooks the live process; ManiaLink rendering *is* the renderer |

**This is the feature's premise, not an obstacle to it.** A HUD only exists on a client. If it could
be observed headlessly the docker dev-server would already cover it and this whole map would be
pointless.

Not physically impossible: TM2020 runs under Proton/Wine and GPU passthrough into containers
exists. It would need passthrough, an X/Wayland display, an Ubisoft login inside the container and
OpenPlanet working under Wine. For a dev-loop tool aimed at kontrol contributors that is a large
amount of fragility for no gain over running the game on the Windows box that already exists.

Docker would not reduce the setup burden anyway: the burden is one folder copy into
`OpenplanetNext\Plugins`, which is exactly what W8's `.op` release turns into a double-click.

### W8 — REOPENED. The `.op` release does not clear the real barrier.

Corrected by the human after the decision was recorded. **Openplanet Developer Mode, which is what
loads an unsigned plugin, effectively requires Trackmania Club Access.** On Starter and Standard
editions only plugins shipped with Openplanet or signed-and-approved ones load. This is a Nadeo
anti-piracy requirement, not an Openplanet policy.

Sources: https://openplanet.dev/next/club, https://openplanet.dev/docs/tutorials/writing-plugins

Why the original answer was wrong: a `.op` bundle downloaded from a GitHub release is still an
**unsigned local plugin**. It needs Developer Mode exactly like the folder install does. The
release changes the install from "copy a folder" to "download a file", which was never the real
friction. The real friction is that every user needs Club Access.

The research gap that caused it: W8 asked what good packaging looks like and never asked what
*installing an unsigned plugin requires*. Distribution format and load eligibility are different
questions and only the second one gates adoption.

**Consequence for W6.** The audience decision was kontrol contributors. If every contributor needs
Club Access, that audience is materially smaller than it looked, and the tool would help only the
subset who already pay for Club. Submitting to the Openplanet plugin manager is no longer the
premature option I judged it to be; it is the only route that makes the tool usable by a kontrol
contributor on Standard access.

**Re-decide between:**

1. Submit the bridge to the Openplanet plugin manager and get it signed. Removes the Club barrier
   for everyone, at the cost of a public review process and a real support commitment.
2. Keep it unsigned and accept the tool is Club-Access-only, documenting that as a prerequisite.
   Honest and free, but shrinks the audience to whoever already has Club.
3. Stay unsigned for now, ship the value that needs no game client at all (the static validator
   already works with nothing running), and revisit signing once W7 has proven the readback.

**Immediate effect:** W7 cannot run at all without Club Access on the machine running the probe.
That is now the gate on the whole prototype.