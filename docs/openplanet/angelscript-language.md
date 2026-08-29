# AngelScript Language Notes

Sources:

- https://www.angelcode.com/angelscript/sdk/docs/manual/doc_script.html (language manual index)
- https://www.angelcode.com/angelscript/sdk/docs/manual/doc_script_handle.html (object handles)
- https://www.angelcode.com/angelscript/sdk/docs/manual/doc_script_func_ref.html (parameter references)
- https://openplanet.dev/docs/tutorials/angelscript-overview (Openplanet's own overview)

Last reviewed: 2026-08-28

## Why this page exists

`openplanet-plugin/TM2020Bridge/Main.as` is the one part of this repo `dotnet build` cannot
check. Every mistake in it costs a game restart or at least a plugin reload to find, so the
language rules that bite are worth having locally. AngelCode owns the *language*; Openplanet
owns the *bindings*. Keep the two straight: a method that exists in the AngelScript standard
library is not automatically present in Openplanet, and vice versa.

## Manual sections at angelcode.com

All under `https://www.angelcode.com/angelscript/sdk/docs/manual/`:

| Page | Covers |
| --- | --- |
| `doc_script_global.html` | Global entities: functions, variables, enums, typedefs |
| `doc_script_statements.html` | Statements, control flow, scope |
| `doc_expressions.html` | Operators and conversions |
| `doc_datatypes.html` | Primitives, value types, reference types |
| `doc_script_func.html` | Function declarations, overloading, default args |
| `doc_script_class.html` | Script classes |
| `doc_script_handle.html` | Object handles (`@`) |
| `doc_script_shared.html` | Shared entities across modules |
| `doc_operator_precedence.html` | Precedence table |
| `doc_reserved_keywords.html` | Reserved words |
| `doc_script_bnf.html` | Formal grammar |
| `doc_script_stdlib.html` | Standard library (only partly what Openplanet exposes) |

## Rules that matter for the bridge

**Handles use `@`, and null checks use `is` / `!is`, never `==`.**
"An object handle is declared by appending the @ symbol to the data type." `==` and `!=` do a
*value* comparison on the objects behind the handles; `is` and `!is` compare identity. Every
engine object reaching us from `GetApp()` is a handle, so `if (editor !is null)` is the only
correct spelling.

```angelscript
auto editor = cast<CGameCtnEditorFree>(GetApp().Editor);
if (editor is null)
    return "map editor not open";
```

**Rebinding a handle needs `@` on the left.** `@g_server = Net::Socket();` binds; `g_server =
...` would assign through the handle to the object.

**`cast<T>(handle)` returns null on a type mismatch** rather than throwing. That is why the
`/status` endpoint can ask "is this editor a map editor, an Interface Designer, or a module
editor" by casting three times.

**Parameter references come in three flavours, and they are not interchangeable:**

- `&in` — input only; "the actual value it refers to normally is a copy of the original so the
  function doesn't accidentally modify the original value". Pair with `const` for strings.
- `&out` — "meant to allow the function to return additional values". The function receives an
  uninitialised reference, must assign to it, and the value is copied back on return. The
  bridge uses `string &out summary` to return a JSON body alongside an error string.
- `&inout` (or bare `&`) — points at the real object, no copy. Restricted: "Only reference
  types, i.e. that can have handles to them, are allowed to be passed as inout references."
  A `string&` parameter therefore will not compile; use `&in` or `&out`.

**Enums are qualified by their owning class.** Nested engine enums are written out in full:
`CGameEditorPluginMap::ECardinalDirections::North`. There is no `using`-style shortcut, and
an unqualified `North` is a compile error.

**Value types (`string`, `int3`, enums) are copied on assignment**; only handles alias.

## Openplanet additions that are not core AngelScript

- `yield()` returns control to the game for a frame. Plugin `Main()` runs as a coroutine, so
  any loop that waits must `yield()` or the game freezes. `WaitForMapEditor` in the bridge is
  a frame counter for exactly this reason.
- `print()`, `warn()`, `trace()` write to `Openplanet.log`.
- `GetApp()` returns the app object; child classes such as `CTrackMania` and
  `CGameManiaPlanet` need an explicit `cast<>`.
- `Json::Parse` returns `Json::Value@`, and `Json::Value` exposes `HasKey`, `GetType`,
  `Length`, `opIndex` and implicit conversions to `string`/`int`/`bool`. `Json::Type` values
  are `Unknown`, `String`, `Number`, `Object`, `Array`, `Boolean`, `Null`.
- The `string` type is Openplanet's, not the standard library's. It has `Length`, `SubStr`,
  `IndexOf` and one-argument `SubStr(start)`. Do not assume `Trim`, `ToLower` or `StartsWith`
  exist: `Main.as` hand-rolls `ToLowerAscii`, `IsDigit` and `IsWhitespace` instead of relying
  on them, and that is the safer default until a helper is confirmed against a live reload.

## Failure mode to design around

An uncaught script exception inside `Main()` stops the coroutine, which kills the HTTP bridge
until the plugin is reloaded. Request parsing is therefore written to *degrade*, not throw:
`ParseJsonObject` refuses anything that does not start with `{`, and every `JsonString` /
`JsonInt` / `JsonBool` helper type-checks before converting and falls back to a default.
