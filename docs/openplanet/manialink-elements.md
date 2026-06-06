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

The basic ManiaLink element documentation does not describe a pasteable video element.
Trackmania exposes video-related managers in script contexts, so GPS/video behavior should
be explored as a runtime script/API feature rather than as a static Interface Designer
XML tag.
