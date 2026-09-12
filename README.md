# NovelForge

Personal Unity toolkit for building visual novels: DSL-scripted dialogue, branching,
save/load, localization, and a read/write branch-graph editor. Distributed as a local/git
UPM package — built for reuse across my own projects, not for a general audience.

## Install

Add to your project's `Packages/manifest.json`:

```json
"com.novelforge.core": "file:../../NovelForge"
```

(or a git URL, if pushed to a remote — adjust the path/URL to wherever this package lives
relative to your project).

## Quick start

Window → Package Manager → NovelForge → Samples → **Getting Started** → Import. Open the
imported `GettingStarted.unity` scene and press Play. It's a ~2 minute playthrough that
exercises dialogue, character sprites/emotions, branching (`if`/`else`), a player choice,
reusable dialogue via `gosub`/`return`, backgrounds, a CG illustration, music/SFX, and
save/load.

## Features

- DSL-scripted dialogue, branching (`if`/`else`), reusable snippets (`gosub`/`return`),
  player choices
- Save/load with variables and flags
- Character sprites with emotions and screen positions
- Localization (text; JSON translation tables keyed by stable line ids)
- Music, SFX and voice audio
- Backgrounds and full-screen CG illustrations
- In-editor DSL script editor with syntax highlighting and autocomplete
- In-editor read/write branch-graph visualization
- `Character Editor` window for managing character definitions and their sprite/emotion
  mappings against actual script usage

See [USAGE.md](USAGE.md) for the DSL reference, project structure, and a step-by-step
guide to building a scene from scratch.

For the architectural reasoning behind any of this, see the design specs under
[`docs/superpowers/specs/`](docs/superpowers/specs/).
