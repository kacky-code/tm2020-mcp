# EmojiChat Media Investigation

Date: 2026-06-06

## Question

Can Kacky/Kontrol revive EmojiChat with 7TV-style animated emotes in Trackmania 2020
ManiaLinks?

The old Kontrol EmojiChat code did not use local files. It fetched a manifest from
`https://api.kacky.gg/emojis/manifest`, built remote URLs from `baseUrl`, and set them on
placeholder quads with `CMlQuad.ChangeImageUrl(...)`.

## Evidence

### Local Files

Local `file://` probes failed in the map-editor ManiaLink preview context:

- `file://Media/Images/.../*.png` stayed as the backing box.
- `file://Media/Images/.../*.webm` stayed as the backing box.
- Absolute local file URI variants also stayed as backing boxes.

Conclusion: do not depend on local Kontrol repo files or local Trackmania media files for
player-facing EmojiChat assets.

### 7TV Native Formats

Remote static 7TV WebP worked:

- `https://cdn.7tv.app/emote/63071bb9464de28875c52531/3x.webp`
- `https://cdn.7tv.app/emote/63071bb9464de28875c52531/4x.webp`

Both loaded after a short delay in static `image="..."` and in `ChangeImageUrl(...)`
probes.

7TV AVIF did not work:

- `https://cdn.7tv.app/emote/01F5VW2TKR0003RCV2Z6JBHCST/4x.avif`

Animated 7TV catJAM did not work directly:

- `https://cdn.7tv.app/emote/01F6MQ33FG000FFJ97ZB8MWV52/2x.webp`
- `https://cdn.7tv.app/emote/01F6MQ33FG000FFJ97ZB8MWV52/3x.webp`
- `https://cdn.7tv.app/emote/01F6MQ33FG000FFJ97ZB8MWV52/4x.webp`
- `https://cdn.7tv.app/emote/01F6MQ33FG000FFJ97ZB8MWV52/3x.gif`

Observed behavior: the colored backing boxes remained or shifted into a corrupted-looking
decode state. Treat this as "Trackmania tries but cannot use the animated payload."

### Remote WEBM

Known Trackmania-cached remote WEBMs worked in both static `image="..."` and
`CMlQuad.ChangeImageUrl(...)` probes:

- `https://download.dashmap.live/tmsigns/Cats-catBonk1x1.webm`
- `https://download.dashmap.live/tmsigns/Cats-glorpRave1x1.webm`

The cached Trackmania files had this profile:

```text
codec: vp8
pixel format: yuv420p
size: 128x128
fps: 60
duration: about 1s
delivery: remote HTTPS
```

Generated local VP9 WEBM probes did not work. The useful conversion target is therefore
VP8 WEBM, not VP9 WEBM.

## Probe Files

Relevant probe files live under:

```text
examples/manialink-media/
examples/interface-designer/
```

Important probes:

- `7tv-cdn-static-probe.manialink.xml`
- `7tv-cdn-changeimage-probe.manialink.xml`
- `7tv-animated-static-probe.manialink.xml`
- `7tv-animated-changeimage-probe.manialink.xml`
- `7tv-catjam-format-matrix.manialink.xml`
- `dashmap-webm-static-probe.manialink.xml`
- `dashmap-webm-changeimage-probe.manialink.xml`

Run a probe from a normal terminal while the map editor preview bridge is available:

```bash
curl -4 -sS --max-time 3 \
  --data-binary @/Users/dkThoLue/personalProjects/tm2020-mcp/examples/manialink-media/dashmap-webm-changeimage-probe.manialink.xml \
  http://127.0.0.1:29100/manialink/preview
```

## Decision

For Kontrol production:

- Store the emote allowlist/manifest in code or config.
- Use 7TV CDN `3x.webp` directly for static emotes.
- Convert animated 7TV emotes server-side to VP8 WEBM.
- Host converted WEBMs on an HTTPS CDN/object store controlled by Kacky.
- Do not commit large binary emote catalogs to the Kontrol repo.

The current Kacky Discord archive is already converted to VP8 WEBM under
`var/kacky-discord-emotes/animated-webm/`. Use
[`scripts/build-emote-cdn.mjs`](../scripts/build-emote-cdn.mjs) to generate static PNG
fallbacks, write the Kontrol widget manifest, and dry-run the rclone upload to
`kacky-r2:kacky-cdn/emotes/`:

```bash
node scripts/build-emote-cdn.mjs
```

The script defaults to dry-run. Add `--execute` only when intentionally deploying to the
R2 bucket behind `cdn.kacky.gg`; rclone reads credentials from its local `kacky-r2`
remote config.

The script writes:

- `var/kacky-discord-emotes/static/` with PNG fallbacks
- `var/kacky-discord-emotes/manifest.json` for the CDN-hosted Kontrol manifest
- `var/kacky-discord-emotes/logs/convert-failures.txt` for non-fatal fallback failures

Manifest entry shape:

```json
{
  "name": "peepoRun",
  "aliases": [],
  "animated": true,
  "staticUrl": "https://cdn.kacky.gg/emotes/peepoRun.png",
  "animatedUrl": "https://cdn.kacky.gg/emotes/peepoRun.webm",
  "width": 128,
  "height": 128
}
```

Use the human emote name as the CDN filename. The manifest is hosted at
`https://cdn.kacky.gg/emotes/manifest.json`; the old `api.kacky.gg/emojis/manifest`
endpoint is no longer part of the flow.

Suggested conversion target:

```bash
ffmpeg -i input \
  -vf "scale=128:128:force_original_aspect_ratio=decrease,pad=128:128:(ow-iw)/2:(oh-ih)/2:color=black" \
  -c:v libvpx \
  -pix_fmt yuv420p \
  -r 60 \
  -an \
  output.webm
```

Open questions:

- Whether alpha transparency is needed and whether Trackmania accepts VP8 alpha reliably.
- Whether lower FPS or dimensions work better for many simultaneous chat emotes.
- Whether converted WEBMs loop long enough in chat; if not, bake repeated frames into the
  output or refresh `ChangeImageUrl(...)` on updates.
