# Phase 1 setup (AGS 3.6 + Cursor plugin scaffold)

This repository now includes `AGS.Plugin.CursorAgent`, a Phase 1 starter plugin based on the AGS editor plugin model from `EditorPlugins.md`.

## What is implemented

- Plugin entry class implementing `IAGSEditorPlugin`.
- `RequiredAGSVersion("3.6.0.0")` for AGS 3.6-first targeting.
- One editor component implementing `IEditorComponent`.
- One project tree root and one top-level menu (`Cursor Agent`).
- A dockable panel with:
  - original/proposed text fields,
  - mock diff loader (`Test mock`),
  - editor context capture action (with graceful fallback if selection is unavailable),
  - transport import action (`Import`) for clipboard/file response ingestion with parse validation,
  - manual preview/apply/undo flow.
- Component state persistence via `ToXml`/`FromXml`.

## Local machine setup

1. Copy `AGS.Types.dll` from your AGS install into:
   - `dependencies/AGS.Types.dll`
2. Build `AGS.Plugin.CursorAgent.csproj`.
3. Copy `AGS.Plugin.CursorAgent.dll` into your AGS editor install folder.
4. Start AGS editor and verify:
   - `Cursor Agent` menu appears.
   - `Cursor Agent` root node appears in project tree.
   - panel opens and "Load mock diff" populates both text areas.

## Next coding steps in this chat

- Replace mock data with file/selection capture from AGS editor context.
- Add line-based patch preview model (hunks/add/remove/replace).
- Implement a safe manual apply engine with conflict checks and rollback.
