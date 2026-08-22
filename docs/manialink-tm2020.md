# ManiaLink in Trackmania 2020

The rules `validate_manialink_xml` enforces, and where each one comes from.

Trackmania 2020 ManiaLink is not the same language as TrackMania Forever ManiaLink, and the
general references on the web mostly describe the older Maniaplanet or TMF dialects. Several
of the constraints below contradict those references, because they were established by
probing an actual TM2020 client. Where that happened, the probe is named.

## Dialect

TM2020 serves ManiaLink v3:

```xml
<manialink version="3" id="widget" name="widget" layer="normal">
```

A wrapper with no `version`, or `version="1"`/`version="2"`, is TMF or Maniaplanet-era XML.
It parses without complaint and then does nothing useful, which is why the validator treats
it as an error rather than a warning. TMF uses `<manialink id="1">` with no version at all,
so a copied TMF snippet is the most common way to hit this.

## Coordinate space

320 x 180, centred on the origin: `x` in `-160..160`, `y` in `-90..90`. Positions inside a
`frame`, `framemodel` or `frameinstance` are relative to that container, so only un-framed
elements can be bounds-checked from the XML alone. The validator checks only those.

## Media

This is where TM2020 diverges most from what the general references say.

| Element | Attribute | Accepted | Notes |
|---|---|---|---|
| `quad` | `image`, `imagefocus` | `.png` `.jpg` `.jpeg` `.dds` `.webm` | `.webm` is not a typo, see below |
| `video` | `data` | `.webm` | no mp4, no HLS/DASH manifest, no web page URL |
| `audio`, `music` | `data` | `.ogg` `.wav` `.mux` | |

**A WebM set as a `quad image` is how animated content works.** This is the single most
useful TM2020 fact in this repo and no general ManiaLink reference states it. The client
plays a remote VP9 WebM as a video-backed quad, which is what the Kacky emote pipeline
relies on: animated emotes are converted to VP9 with alpha and served from a CDN, then set
on quads. Proven by `examples/manialink-media/dashmap-webm-static-probe.manialink.xml` and
`kacky-cdn-emote-probe.manialink.xml`.

**Animated image payloads do not decode.** `.gif` and `.avif` fail: the client attempts them
and leaves the backing box, sometimes in a corrupted decode state. Mirror the asset as VP9
WebM instead. Proven by `7tv-animated-static-probe.manialink.xml` and
`7tv-catjam-format-matrix.manialink.xml`.

**`.webp` is ambiguous in the markup, but decidable from the file.** Static WebP loads; animated
WebP does not, and the URL never says which it is, so `validate_manialink_xml` can only warn.
`check_manialink_media` settles it by fetching the first bytes and reading the RIFF header: an
extended-format `VP8X` chunk carries an `ANIM` flag (`0x02`) in its flags byte at offset 20.

That check was validated against the in-game record in
[`emoji-chat-investigation.md`](emoji-chat-investigation.md): the two 7TV WebPs observed to load
classify as static, and the three catJAM WebPs observed to fail classify as animated. Seven of
seven, including a WebM that worked and a deliberately dead URL.

Proven by `7tv-cdn-static-probe.manialink.xml`.

**`file://` paths that point at real media did not load** in map-editor ManiaLink preview,
for relative and absolute forms alike. Host over http(s). Proven by
`local-image-path-matrix.manialink.xml`.

**Built-in engine resources are the exception.** Extensionless `file://` paths such as
`file://ZoneFlags/Path/World` resolve inside the client and are used by the real scoretable
HUD. The validator recognises the extensionless shape and leaves them alone.

**The video element needs a direct media file.** A clip page URL, a Cloudflare Stream iframe
URL, an HLS `.m3u8` or DASH `.mpd` manifest, and a signed MP4 all fail. If the canonical
asset is MP4, produce a separate WebM rendition and point ManiaLink at that.

## Elements

Known: `manialinks` `manialink` `frame` `framemodel` `frameinstance` `quad` `label` `entry`
`fileentry` `textedit` `audio` `music` `video` `graph` `gauge` `include` `script`
`stylesheet`. Anything else is silently ignored by the client, so the validator warns.

`music` must sit outside any `frame`.

## Script events

`scriptevents="1"` on an element with no `<script>` block in the document means the events go
nowhere. Duplicate `id` values are a related trap: ManiaScript `GetFirstChild` resolves only
one of them, so a repeated id silently wires up the wrong element.

## Interface Designer fragments

Paste into the in-game Interface Designer is stricter than map-editor preview, and the two
are easy to confuse. Validate with `target: "designer"` for fragments meant to be pasted.
Those must omit the XML declaration and the `<manialink>`/`<manialinks>` wrapper, stick to
static `frame`, `quad` and `label` nodes, and carry no runtime attributes (`action`,
`scriptaction`, `scriptevents`, `class`, `hidden`, `url`, `manialink`).

## What the game will not tell you

A running client cannot report whether an image loaded. `CGameManialinkQuad` exposes
`DownloadInProgress` and `CPlugBitmap@ Image`, which look like they should answer it, and do not:
probed against a real client, **2,338 image quads reported `Image` as null and `DownloadInProgress`
as false, including Nadeo menu chrome and Ubisoft CDN images that were visibly on screen**. The
game logs nothing about image loading either.

So reachability is answered from outside the game, by `check_manialink_media` fetching the URLs
directly. That needs no client, no Openplanet and no Club Access. What a client *can* report is
structure: the control tree, computed positions, sizes, visibility, and whether a layer's
ManiaScript is running.

## Sources

- Probe evidence and the emote decision record: [`emoji-chat-investigation.md`](emoji-chat-investigation.md)
- Probe fragments: `examples/manialink-media/`, `examples/interface-designer/`
- Element and attribute reference: [`openplanet/manialink-elements.md`](openplanet/manialink-elements.md)
- OpenPlanet script API notes: [`openplanet/INDEX.md`](openplanet/INDEX.md)
- ManiaLink reference: https://maniaplanet-community.gitbook.io/maniascript/manialink/manialinks
- ManiaScript docs: https://maniascript.boss-bravo.fr/
