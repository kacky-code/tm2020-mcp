# Kacky Map Blueprint

Four sheets planning a Kacky-style map, built from what parsing the corpus established:

| Sheet | Contents |
| --- | --- |
| `Main.dc.html` | 48x48 top-down route plan, sections A-G, loop footprint and reset run at real cell sizes |
| `HeightProfile.dc.html` | Deep Dip 1 and 2 free blocks per 8-level band, and why the route plan is flat |
| `Palette.dc.html` | What Kacky maps are made of, against what the generator can place |
| `Rules.dc.html` | Direction table, learned block shapes, loop and reset motifs |

One rule runs through all four: **cyan is measured** - parsed out of 450 Kacky Reloaded maps
and Deep Dip 1/2 with GBX.NET, with sample sizes shown - and **amber is proposed or
unverified**. Sections A-G are a proposal. The numbers behind the sheets are in
[../tm2020-map-geometry.md](../tm2020-map-geometry.md).

## Re-seeding

The published canvas is assembled from these files by the `design` skill's helper. The
assembled `kacky-map-blueprint.html` is about 2 MB of editor payload and is **not committed** -
it is generated. To change a sheet, edit its `.dc.html` here, re-run the helper, and republish
to the same artifact URL so the link keeps working.
