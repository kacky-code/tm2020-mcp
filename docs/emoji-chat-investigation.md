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

Earlier generated local VP9 WEBM probes did not work, but those files were not encoded
with the Kacky-proven alpha recipe. Current Discord emote conversion uses VP9 with
`alpha_mode=1`; see the decision section below for the active recipe.

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
- Convert animated emotes server-side to VP9 WEBM with alpha.
- Host converted WEBMs on an HTTPS CDN/object store controlled by Kacky.
- Do not commit large binary emote catalogs to the Kontrol repo.

The Kacky Discord `EmotesZip/` library is the source of truth. Use
[`scripts/build-emote-cdn.mjs`](../scripts/build-emote-cdn.mjs) to re-convert GIFs to
VP9 alpha WEBM, generate transparent first-frame PNG fallbacks, normalize static PNGs,
write the Kontrol widget manifest, and dry-run the rclone upload to
`kacky-r2:kacky-cdn/emotes/`:

```bash
node scripts/build-emote-cdn.mjs
```

The script defaults to dry-run. Add `--execute` only when intentionally deploying to the
R2 bucket behind `cdn.kacky.gg`; rclone reads credentials from its local `kacky-r2`
remote config. After a real upload, purge the Cloudflare cache for `/emotes/*` because
older media objects were served as immutable. Current media uploads use
`Cache-Control: public, max-age=86400`; the manifest uses the same cache control.

The script writes:

- `var/kacky-discord-emotes/animated-webm/` with VP9 alpha WEBMs
- `var/kacky-discord-emotes/static/` with PNG fallbacks and static PNG emotes
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

Static entries use `"animated": false` and `"animatedUrl": null`.

Use the human emote name as the CDN filename. The manifest is hosted at
`https://cdn.kacky.gg/emotes/manifest.json`; the old `api.kacky.gg/emojis/manifest`
endpoint is no longer part of the flow.

Suggested conversion target:

```bash
ffmpeg -y -i input.gif \
  -c:v libvpx-vp9 \
  -crf 10 \
  -b:v 0 \
  -pix_fmt yuva420p \
  -auto-alt-ref 0 \
  output.webm
```

Open questions:

- Whether lower FPS or dimensions work better for many simultaneous chat emotes.
- Whether converted WEBMs loop long enough in chat; WebM has no real loop flag, so rely
  on the client looping the animated quad and verify this in-game.
