# ManiaLink Elements

Source: https://doc.maniaplanet.com/manialink/getting-started
Last reviewed: 2026-06-06

## Core Structure

Full ManiaLink files commonly use:

```xml
<?xml version="1.0" encoding="utf-8" standalone="yes" ?>
<manialink version="3">
  <label text="Hello World!" />
</manialink>
```

For Interface Designer paste/import, do not assume the full document wrapper is accepted.
Paste-safe fragments should usually contain only the child controls needed by the
Designer.

## Relevant Tags

- `frame`: groups controls and lets child positions be relative to the frame.
- `framemodel`: reusable frame model.
- `frameinstance`: instance of a frame model.
- `quad`: image, background block, or built-in style/substyle element.
- `label`: text element with style/substyle and formatting support.
- `audio`: audio file element with play/loop controls.
- `music`: background music element; must be outside a `frame`.
- `video`: video file element. A useful probe shape is
  `<video data="file://Media/Videos/gps.webm" music="1" play="1" hidden="1" />`.
- `include`: include another XML file.

## Interface Designer Paste-Safe Rules

Use these rules for generated fragments meant for manual Designer paste:

- Omit XML declarations.
- Omit `<manialinks>` and `<manialink>` wrappers.
- Prefer static `frame`, `quad`, and `label` nodes.
- Escape raw XML attribute text like `<` as `&lt;`.
- Strip runtime/script attributes for static design work: `action`, `scriptevents`,
  `class`, `hidden`, and drag metadata.
- Avoid huge generated lists of `frameinstance` rows.
- Round generated `z-index` values to short stable numbers.

## Video/GPS Note

ManiaLink has a `video` tag shape worth testing for GPS-style flows. Trackmania also
exposes video-related managers in script contexts, so there are two separate test tracks:

- static XML playback with `<video ... />`
- runtime script/API control from a clicked `label` or `quad`

Do not assume Interface Designer, map-editor preview, and live server HUD layers all
accept the tag the same way. Test each context explicitly.
