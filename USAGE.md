# NovelForge — Usage Guide

## Project structure

- `*.nfscript` files — DSL scripts, anywhere under `Assets/`. Imported automatically into
  a `NovelScriptAsset` by `NovelScriptImporter`.
- `CharacterDefinition` assets (`NovelForge/Character Definition`) — one per character:
  id, display name, name color, and a sprite per emotion. Collected into a
  `CharacterLibrary` asset (`NovelForge/Character Library`) that a scene's
  `DialoguePresenter` references.
- `BackgroundLibrary` (`NovelForge/Background Library`) — id→sprite maps for `bg` and `cg`
  commands.
- `AudioLibrary` (`NovelForge/Audio Library`) — id→clip maps for `music` and `sfx`
  commands.
- Localization files — plain JSON, `{ "line-id": "translated text", ... }`. Loaded via
  `LocalizationTable.FromJson(textAsset.text)`.
- Save data — `JsonSaveStorage` (implements `ISaveStorage`) writes one JSON file per slot
  under `Application.persistentDataPath` by default; swap in a different `ISaveStorage`
  implementation for a different backend.

## DSL syntax

| Construct | Syntax | Notes |
|---|---|---|
| Label | `label <name>` | Jump/gosub target. Resets auto-generated localization ids. |
| Comment | `// text` | Attaches to the next command; round-trips through the Branch Graph editor. |
| Dialogue | `<CharacterId>: <text> [#emotion] [@id] [left\|right\|center]` | `#emotion` selects a sprite pose; `@id` is an explicit localization id (auto-generated as `<label>_<ordinal>` if omitted); position is a bare trailing `left`/`right`/`center` word. |
| Assignment | `set <var> = <value>` / `+=` / `-=` | Value: `true`/`false`, an int, a float, or a `"quoted string"`. |
| Condition | `if <var> <op> <value>` ... `else` ... `endif` | `op`: `== != > >= < <=`. `else`/`endif` optional/required as in any C-like language. |
| Choice | `choice` then one or more `"text" [@id] -> label` lines immediately after | Player picks one; jumps to the option's label. |
| Jump | `jump <label>` | Unconditional jump. |
| Subroutine call | `gosub <label>` | Jumps, pushing a return address. |
| Return | `return` | Pops the call stack (from the nearest `gosub`) or ends the script if the stack is empty. |
| Background | `bg <id>` | Crossfades to the background registered under `<id>`. |
| CG | `cg <id>` | Fades in a full-screen illustration. |
| Music | `music <id>` | Crossfades to the track registered under `<id>`. |
| Sound effect | `sfx <id>` | Plays a one-shot clip from a pooled voice. |
| Wait | `wait <seconds>` | Pauses the script for the given duration. |

## Editor tools

- **Script Editor** (double-click a `.nfscript` asset in the Project window) — syntax
  highlighting and autocomplete for the DSL above.
- **Character Editor** (`Window → NovelForge → Character Editor`) — browse/edit
  `CharacterDefinition` assets, preview emotion sprites, and validate character ids used
  in scripts against what's actually defined (and vice versa).
- **Branch Graph** (`Window → NovelForge → Branch Graph`, or right-click a `.nfscript`
  asset → **Open in Branch Graph**) — a read/write node graph over a script's `label`
  blocks; edits round-trip back to the original text, preserving comments.
- **`NovelScriptImporter`** — a `ScriptedImporter` that compiles every `.nfscript` on
  import and reports DSL syntax errors as Unity import errors at the exact line.

## Building a scene from scratch

1. Write a `.nfscript` file with at least one `label` and a `return`.
2. Create a `CharacterDefinition` per speaking character, a `CharacterLibrary` referencing
   them, and (if the script uses `bg`/`cg`/`music`/`sfx`) a `BackgroundLibrary`/
   `AudioLibrary`.
3. In a scene, add the presenters your script needs: `DialoguePresenter` (+ a
   `DialogueBoxView` with name/body `TMP_Text` and an advance `Button`, + an `ActorView`
   per on-screen character position, using `SpriteRenderer`s), `ChoiceView` (+ option
   `Button`s with a `TMP_Text` child each), `BackgroundPresenter` (+ two crossfade
   `SpriteRenderer` slots and a CG slot), `AudioPresenter` (+ two music `AudioSource`s for
   crossfading).
4. Add a `NovelRunner` component; assign the script asset and every presenter above to its
   fields.
5. Call `runner.Play()` from your own code (a button click, a `Start()`, wherever fits your
   game's flow) to compile and run the script.

`Samples~/GettingStarted` is a complete, working reference for all of the above — including
a title screen and save/load — see `DemoFlow.cs` for how a real game wires `NovelRunner`
together with save/load and screen navigation (`NovelRunner` itself stays minimal and
doesn't know about either).

## Save/Load

- `ISaveStorage` — the storage abstraction (`Save`/`Load`/`SlotExists`).
  `JsonSaveStorage` is the built-in implementation.
- `SaveLoadController` — orchestrates a save/load against a `PlaybackController` +
  `StoryContext` + `ISaveStorage`. Construct one after `NovelRunner.Prepare()` (its
  `.Playback`/`.Context` are only populated after `Prepare()` succeeds).
- `SaveLoadView` — a UGUI component presenting a fixed grid of named slots in either Save
  or Load mode (`ShowSaveMode()`/`ShowLoadMode()`), showing Occupied/Empty per slot.

**Important ordering for "Continue" flows:** call `NovelRunner.Prepare()` (which builds a
fresh `Playback` but does not start it), then `SaveLoadController.LoadInto()` (which
restores the snapshot onto that not-yet-running `Playback`), and only then
`NovelRunner.Begin()`. Unity runs a coroutine synchronously up to its first real suspend
point, so calling `Begin()`/`Play()` before restoring the snapshot can let a few commands
execute from the start of the script before the restore takes effect.

## Localization

- Format: flat JSON, `{ "line-id": "translated text" }`.
- Every dialogue line and every choice option has a line id — either explicit (`@id` in
  the script) or auto-generated as `<nearest label>_<ordinal>`. Ids must be unique across
  the whole script.
- Assign a `TextAsset` (the JSON file) to `NovelRunner`'s `localizationJson` field; it's
  parsed via `LocalizationTable.FromJson`. Leave it unassigned to skip localization
  entirely (dialogue/choice text is shown as written in the script, no warnings).
- A line id present in the script but missing from the translation table falls back to
  the source text with a `Debug.LogWarning` — useful for spotting incomplete translations.
  `Samples~/GettingStarted/Localization/ru.json` demonstrates this: it translates 4 of the
  script's ~13 line ids on purpose. Assign it to the demo's `NovelRunner` to see both the
  translated lines and the fallback warnings for the rest.
