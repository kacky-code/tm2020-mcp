# Handoff: developing and testing the AngelScript plugin

Audience: Codex, or any agent working on `openplanet-plugin/`. Created 2026-08-28.

## The constraint everything else follows from

You cannot run this code. There is no Trackmania client in your sandbox, no AngelScript
compiler, and `dotnet` will not look at a `.as` file. A syntax error you introduce is invisible
until a human loads the plugin.

So the loop is: you write, a human installs and reloads, the log comes back. Design every change
so that loop runs **once**, not five times. A round trip costs a person switching to a game.

## What can and cannot be verified

| Side | Can you check it | How |
|---|---|---|
| `src/Tm2020Mcp/` (.NET) | yes, fully | `dotnet build`, `dotnet test --no-build`, TDD per `AGENTS.md` |
| `openplanet-plugin/**/*.as` | no | human reloads in Openplanet, reads `Openplanet.log` |

The rule that falls out: **keep AngelScript thin and push logic into .NET.** Parsing, validation,
formatting and anything with branches belongs on the side that has tests. `ManialinkValidator`
and `Tm2020ManialinkFacts` are the pattern. The plugin should mostly read engine state and hand
it over.

Never write "verified" about plugin behaviour you did not see in a log. Say "not verified, needs
a client" instead. That sentence is cheap and a wrong claim is not.

## Before writing any AngelScript

Check every unfamiliar member against the class reference rather than recalling it.

- URL shape: `https://next.openplanet.dev/<Engine>/<Class>`, and the engine matters.
  `CGameItemModel` is under `GameData`, and 404s under `Game`. So does `CGameCtnCollector`.
- `curl` works on those pages. `WebFetch`-style summarising drops exact signatures, so fetch and
  strip tags rather than asking a summariser what the members are.
- Look in `docs/openplanet/` first. If you add a page, keep it curated with its source URL, and
  do not paste whole pages in. `docs/openplanet/raw/` is gitignored on purpose.
- The reference carries a doc build date at the bottom. Quote it when you record a finding, so a
  later reader knows how stale the claim is.

## AngelScript rules that have already cost us

Every one of these came out of real code, not a style guide.

1. **No eval.** Scripts compile at plugin load. You cannot send a snippet over the bridge and get
   an answer. Anything the bridge can answer has to be a fixed endpoint written in advance.
2. **Reverse loops must use `int`.** `for (uint i = n - 1; i >= 0; i--)` never terminates, because
   a uint is never negative. Worse, when `n` is 0 the initialiser wraps to 4294967295 and the
   first index is far out of bounds.
3. **Handles.** Use `is` and `!is` for identity. `@a = b` rebinds the handle, `a = b` copies the
   value. Nod handles are reference counted, so storing one in your own array keeps that object
   alive after the engine drops it: the handle never goes null on its own, and the object keeps
   answering with stale-looking-fine values.
4. **Any `@` member can be null**, and a null dereference kills the plugin. From outside the game
   that failure looks identical to "bridge not running", which is a miserable thing to debug.
   Guard, and print when a guard fires so the guard earns its place or gets deleted.
5. **`cast<T>()` returns null rather than throwing.** Check the result, always.
6. **`yield()` inside long-running loops**, or the game hangs. See `Main()` in `TM2020Bridge`.
7. **A ternary's two branches can never be two mutually-convertible types.** This cost a reload
   on 2026-08-29: `Names += (Info is null ? "?" : Info.Name);` fails to compile with
   `Can't find unambiguous implicit conversion to make both expressions have the same type`,
   because `Info.Name` is a `wstring` and `"?"` is a `string`. The manual
   ([doc_expressions](https://www.angelcode.com/angelscript/sdk/docs/manual/doc_expressions.html))
   spells out why: the compiler converts "by following the principle of least cost", and
   *"if the conversion doesn't work, **or the conversion of either expression cost the same**,
   then the compiler will give an error."* string↔wstring converts equally well in both
   directions, so it is a tie, so it is an error. Note the asymmetry that makes this
   counter-intuitive: `cond ? SomeArray.Length : 4` (uint vs int literal) compiles fine, because
   *that* conversion has a cheaper direction. Assign into a typed local instead:
   ```angelscript
   string One = "?";
   if (Info !is null) One = Info.Name;   // conversion happens on assignment, no tie to break
   ```
   The same trap is waiting on any engine `Name`, `IdName` or other `wstring` member.
8. **A widget's label IS its ImGui id.** `UI::Button("Select###" + Name)` collides for every row
   whose `Name` is `""` — and names come back empty whenever an article model has not loaded, so
   two unresolved rows become one button and clicking either drives whichever ImGui saw first.
   The symptom is `2 visible items with conflicting id`. Two rules fall out, both learned from
   PuzzleMode on 2026-08-29:
   - Key ids on something guaranteed unique and stable, normally the row index, never a value
     read from the engine.
   - A label that *changes* silently changes the widget's identity too. `UI::Button(ButtonText)`
     where the caption flips between "Load challenge" and "Reload challenge" is a different
     widget each way; it needs an explicit `###` suffix to stay one.

   TrackmaniaBingo (~9,700 lines, the biggest local reference) puts `##` on essentially every
   widget for this reason — down to `"+##plus"` and `"-##minus"`, because a `+` button is
   precisely the thing you end up with two of.

## Logging is the only assertion surface

`Openplanet.log` is where a human reads your work, and they will grep it, not read it. So:

- Prefix every line with a stable tag: `print("E1: ...")`. `_W7Probe` uses `W7:` throughout and
  that is what made its output usable.
- One line per fact, `key=value` style, plus one summary line per run.
- Never print from a per-frame path. A log that scrolls is a log nobody can read.
- Print the counts, not just the failures. "blocks=812 nullBlockInfo=0" is a result;
  silence is not.

## Distinguish failure modes, never collapse them

"No client running", "client running but not in the editor", "editor open with no map", and
"editor open, map loaded, zero blocks" are four different answers. Returning an empty list for
all of them reads as success when nothing worked. W9 in `WAYFINDER-MAP.md` landed on this and it
applies to every diagnostic you add.

## How AngelScript actually gets tested here: the probe

`openplanet-plugin/_W7Probe/` is the pattern. Copy its shape.

1. You write `openplanet-plugin/_<name>Probe/` with an `info.toml` and a `Main.as`. The file
   header says which question it answers and that it is throwaway.
2. Keep `void Main() { }` empty and hang the work off a menu item, so nothing runs until asked.
3. You hand back the exact install command and the exact menu label. See below.
4. A human installs, enables Developer Mode, reloads plugins, clicks the item.
5. They grep the log for your prefix and paste it back.
6. You record the answer where it belongs, then delete the probe folder.

Probes are deleted, not merged. A probe that survives becomes an endpoint nobody maintains.

Install commands to reproduce in your handback, from `README.md`:

```powershell
# Windows
$PluginDir = "$env:USERPROFILE\OpenplanetNext\Plugins"
Remove-Item -Recurse -Force "$PluginDir\_XProbe" -ErrorAction SilentlyContinue
Copy-Item -Recurse ".\openplanet-plugin\_XProbe" "$PluginDir\_XProbe"
```

```bash
# macOS via CrossOver, bottle name varies
PLUGIN_DIR="$HOME/Library/Application Support/CrossOver/Bottles/<Bottle>/drive_c/users/crossover/OpenplanetNext/Plugins"
rm -rf "$PLUGIN_DIR/_XProbe" && cp -R openplanet-plugin/_XProbe "$PLUGIN_DIR/"
```

Keep plugins in the **user-local** `OpenplanetNext\Plugins` folder. The game-install
`Openplanet\Plugins` folder rejects local source plugins with signature errors.

## House style of the bridge plugin

Match `TM2020Bridge/Main.as` rather than inventing:

- JSON is built by hand, single-quoted literals, strings passed through `JsonEscape`.
- Endpoints are branches in the `if / else if` chain in `HandleClient`, each setting
  `responseBody` and `status` explicitly.
- Errors return `{"error":"..."}` with a 4xx. Do not return 200 with an empty body.
- State-changing endpoints `print` one line saying what happened.
- Endpoints stay localhost-only, and responses stay JSON unless the endpoint is explicitly XML.
- Adding an endpoint means adding the matching method in `src/Tm2020Mcp/EditorBridge/`, and an
  MCP tool only if an agent would really call it.

## What to hand back

Every AngelScript change ships with:

- the files you touched
- the install command, copy-pasteable, with the real folder name filled in
- the exact menu path to click
- the grep to run, for example `grep '^E1:' Openplanet.log`
- what each possible result means, written before the run, so the human is not interpreting
- an explicit line saying the plugin side is unverified until that log comes back

## Repo rules that still apply

From `AGENTS.md`: small reviewable changes, TDD for non-trivial .NET logic, `dotnet build` and
`dotnet test --no-build` before pushing, update `README`/docs when tools, endpoints or setup
change, no `bin/`, `obj/`, `TestResults/`. Trackmania 2020 ManiaLink is not the TMF or Maniaplanet
dialect: use `docs/manialink-tm2020.md` and the probes in `examples/`, never a general ManiaLink
reference.

## Prerequisites the human needs, once

Trackmania with **Club Access**, because the bridge is unsigned, unsigned plugins need Developer
Mode, and Developer Mode is Club-only. Enable it at F3 > Developer > Signature Mode. If that menu
is missing, the account does not have Club Access and no amount of copying files will help.

## What is actually open

Updated 2026-08-29, after a session that built `kacky-code/puzzlemode` against a live client.

**In this repo**

- `HANDOFF-editor-facts.md` E1 and E3. E2, E4 and E5 are answered. E3's underlying lazy-load
  behaviour is measured, but `GetCollectorNod()` itself was never called.
- `WAYFINDER-MAP.md` W2: is there any engine signal that a media URL failed to load, or must
  failure be inferred? Nine of the ten tickets are closed; this is the one left, and it is
  make-or-break for "the image at URL X never loaded".

**In `kacky-code/puzzlemode`**, which is where the live-client work now happens

- **The Simple editor resolves no block or item names.** Advanced works and reports
  `inventory loaded: 3757 blocks, 632 items`. Not a timing race: the load retries every frame and
  ran for seconds without recovering. `LogInventoryRootContents` is already in the plugin and
  prints article and Nadeo-authored counts per root. Load once in each mode and compare; that
  settles this and the next item together.
- **The inventory root index is not settled.** `C_RootBlocks = 0` came from logging
  `CurrentRootNode` by identity. Editor++ uses root 1 for blocks and names root 0 `CrashBlocks`,
  and the plugin originally used 1. Both readings can be true, because the root the editor has
  *selected* need not be the root holding the library. Every root reports an empty `NodeName`
  **and** an empty `Name`, so a wrong index never announces itself; it silently reads a different
  tree. Three separate bugs have come from this.
- **Rotated multi-cell base blocks are unverified end to end.** `BlockAnchorCoord` works and the
  anchor correction shows in the log, but nobody has placed a long block, exported a base row,
  rotated it, and confirmed it reports "has been turned" rather than "missing".
- **Free blocks cannot be part of a base.** Detected and refused rather than silently mismatched.
  Supporting them needs `Dev::GetOffsetVec3`, which is the one thing here that breaks on a game
  update.

Measured engine facts and the reference-plugin list are in `kacky-code/vault` under
`openplanet/`, marked measured / documented / open. Read that before re-deriving anything.
