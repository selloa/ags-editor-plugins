---
title: AGS Editor Plugin Collection
version: 1.0.0
date: 2026-06-06
status: published
description: Forkbare AGS 3.6 Editor-Plugins — Sample, Viewer und Experimente.
---

# AGS Editor Plugin Collection

*Sammlung von Adventure Game Studio Editor-Plugins (.NET), die du forken und erweitern kannst.*

Diese Seite listet automatisch alle **`AGS.Plugin.*`**-Ordner im Repository (6 Plugins). Nach jedem Push wird sie per GitHub Action neu gebaut; Veröffentlichung über **GitHub Pages** aus dem Ordner **`docs/`**.

Offizielle AGS-Doku: [Editor Plugins](https://adventuregamestudio.github.io/ags-manual/EditorPlugins.html)

---

## Plugins

| Plugin | Ordner | AGS | Beschreibung |
| --- | --- | --- | --- |
| [Cursor Agent](https://github.com/selloa/ags-editor-plugins/tree/master/AGS.Plugin.CursorAgent) | `AGS.Plugin.CursorAgent` | `3.6.0.0` | AGS-Editor-Plugin (.NET) |
| [Entity Catalog](https://github.com/selloa/ags-editor-plugins/tree/master/AGS.Plugin.EntityCatalog) | `AGS.Plugin.EntityCatalog` | `3.6.0.0` | AGS-Editor-Plugin (.NET) |
| [Game States](https://github.com/selloa/ags-editor-plugins/tree/master/AGS.Plugin.GameStates) | `AGS.Plugin.GameStates` | `3.6.0.0` | Scans GlobalInt defines and Get/SetGlobalInt usage across scripts; exports a searchable state catalog for any AGS project. |
| [Game States (AOTT)](https://github.com/selloa/ags-editor-plugins/tree/master/AGS.Plugin.GameStates.AOTT) | `AGS.Plugin.GameStates.AOTT` | `3.6.0.0` | Audit of the Tentacle GlobalInt catalog — includes EpisodeIRS_GameStartInit and episode room grouping. |
| [Sample plugin](https://github.com/selloa/ags-editor-plugins/tree/master/AGS.Plugin.Sample) | `AGS.Plugin.Sample` | `3.0.0.0` | AGS-Editor-Plugin (.NET) |
| [Speech Viewer](https://github.com/selloa/ags-editor-plugins/tree/master/AGS.Plugin.SpeechViewer) | `AGS.Plugin.SpeechViewer` | `3.6.0.0` | AGS-Editor-Plugin (.NET) |

---

## Schnellstart

1. **`AGS.Types.dll`** aus deiner AGS-Installation nach `dependencies/AGS.Types.dll` kopieren (siehe [`dependencies/README.md`](../dependencies/README.md)).
2. Plugin-Projekt bauen (Visual Studio oder MSBuild), z. B. `AGS.Plugin.SpeechViewer`.
3. Die **`bin\Debug\*.dll`** in den AGS-Editor-Ordner legen und den Editor neu starten.

Neues Plugin: [`AGS.Plugin.Sample`](../AGS.Plugin.Sample/) kopieren, IDs umbenennen, bauen, deployen.

Ausführliche Doku: [`README.md`](../README.md) im Repo-Root.


---

## Page build notes

- `hub/content/index.md` wird von `discover_plugins.py` vor jedem Build neu geschrieben.
- Optionale Plugin-Beschreibung: `hub.blurb` (eine Zeile) oder `PLUGIN.md` im Plugin-Ordner.
- Private Notizen: `docs/private/` (gitignored).

