# Reference mirrors

Local clones of the ManiaScript repos this workbench is checked against. Nothing here is
ours. Do not edit anything under `refs/`, only read it.

## Why it exists

Trackmania 2020's `Libs/Nadeo/*` are published nowhere. The mode scripts that ship with the
game are inside the title pack, so the only readable sources for how Nadeo builds a mode are
other people's work: the ManiaPlanet-era repos where Nadeo did publish the libraries, and
community modes that call the TM2020 ones.

Several of these repos are dormant, which is the other reason to mirror rather than rely on
them staying up.

## Use it

```bash
./refresh.sh          # clone or update every repo, then regenerate MANIFEST.md
```

`MANIFEST.md` records the pinned commit and last upstream commit per repo, so a claim can be
traced to an exact revision.

The clones are gitignored. `MANIFEST.md` and `refresh.sh` are tracked, which is enough to
reproduce the corpus exactly. Committing 224 MB of third-party code, under licences we do not
control, would be wrong on both counts.

## What it is good for

Searching it beats guessing at an API. For example, whether a negative `S_FinishTimeout` means
"compute from the map" was settled by finding `GetFinishTimeout()` in a community knockout,
after the official docs turned out to say nothing about round timing.

Not a substitute for reading what the game actually renders. For that use `list_ui_layers` and
`get_ui_layer_xml`, which return the ManiaLink a UI module produces live.
