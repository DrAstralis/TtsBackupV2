# TTS Asset Backup (WIP)

Desktop tool (WPF / .NET) for backing up **Tabletop Simulator** saves and their external assets.

The goal of this app is to:

- Load a TTS save file.
- Let you choose **which objects** (bags, decks, dice, etc.) to keep.
- Find all **remote assets** those objects depend on (images, decks, bundles, etc.).
- Optionally **download** those assets to a local folder.
- Optionally **rewrite asset URLs** in a new TTS save to point at your own hosting.
- Write a **manifest** so you know exactly what was backed up and what failed.

> ⚠️ Status: early work-in-progress.  
> Phase 2 is implemented: basic parsing + object tree browser.

---

## Features (current)

### ✔ Load and inspect saves

- Open a TTS save (`.json`).
- Parse the save and build an internal object tree:
  - Top-level `ObjectStates`
  - `ContainedObjects` (e.g. cards in decks, dice in bags)
  - `States` (alternate states) – currently just marked, not separately selectable.
- Display the hierarchy in a WPF `TreeView` so you can see:
  - Object type (`DeckCustom`, `Card`, `Die_6_Rounded`, etc.).
  - Structure of bags, decks, and other containers.

### ✔ Basic app shell

- WPF UI using MVVM.
- Dependency injection via `Microsoft.Extensions.Hosting`.
- Core logic separated into:
  - `TtsBackup.Core` (models + service contracts)
  - `TtsBackup.Infrastructure` (implementations: parsing, disk-space, settings)
  - `TtsBackup.Wpf` (UI + view models)

---

## Planned Features

The design for later phases is already sketched and the interfaces are in place:

- **Object selection**
  - Checkbox tree: select a bag/deck and auto-include its children, but allow unchecking specific items.
  - States auto-included when an object is selected.

- **Asset discovery**
  - Scan only known TTS asset URL fields (e.g. `CustomDeck.FaceURL`, `CustomImage.ImageURL`, etc.).
  - Distinguish asset types: image, mesh, assetbundle, deck face/back, decal, UI asset, environment (table/sky/LUT).

- **URL rewriting**
  - Global base-URL mode (e.g. move everything to `https://mycdn.example.com/tts/`).
  - Per-asset override for special cases.
  - Warnings for local paths (`C:\...`, `file://...`) so you know others can’t see those assets.

- **Asset download**
  - Optional: download assets referenced by the selected objects.
  - Global concurrency with adaptive per-host backoff (honour rate limits).
  - Deduplicate downloads by URL and content hash (download once, reference many times).
  - “Collapse shared assets” toggle: share one local file vs. multiple logical copies.

- **Disk space & robustness**
  - Runtime low-disk detection: pause job and let the user free space, then resume.
  - Safe writes with temp files and atomic renames.
  - Cleanup of partial downloads.

- **Export & manifest**
  - Write a new TTS save containing only the selected objects.
  - Optional transform normalization (reposition objects near table center).
  - Option to keep or strip table/sky/lighting from the source save.
  - JSON manifest listing all assets, their statuses, and which objects use them.

---

