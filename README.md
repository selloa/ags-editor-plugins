Speech Viewer
<img width="1920" height="1027" alt="Speech Viewer" src="https://github.com/user-attachments/assets/154f58fd-2c36-462a-9a0d-ab88e0800d29" />
Entity Catalog
<img width="1920" height="1027" alt="Entity Catalog" src="https://github.com/user-attachments/assets/5d6fbb92-6bae-496a-ab16-e42f61ec9021" />

# AGS Editor Plugin Collection

Sammlung von **Adventure Game Studio (AGS) 3.6** Editor-Plugins (.NET), die du forken, als Vorlage nutzen oder erweitern kannst.

Ziel dieses Repos: anderen (und dir selbst) den Einstieg erleichtern — von einem minimalen Sample über read-only Viewer bis hin zu experimenteller Cursor-Agent-Integration.

Offizielle AGS-Doku zu Editor-Plugins: [Editor Plugins](https://adventuregamestudio.github.io/ags-manual/EditorPlugins.html)

---

## Plugins in diesem Repo

| Ordner | Zweck | Einstieg |
|--------|--------|----------|
| [`AGS.Plugin.Sample`](AGS.Plugin.Sample/) | Offizielles Sample aus dem AGS-Handbuch — minimaler Menü-/Tree-/Pane-Scaffold | **Neues Plugin starten** |
| [`AGS.Plugin.SpeechViewer`](AGS.Plugin.SpeechViewer/) | Dialog- und Script-Speech im Editor anzeigen, filtern, als HTML exportieren | Viewer + Parser-Port aus Python |
| [`AGS.Plugin.EntityCatalog`](AGS.Plugin.EntityCatalog/) | Rooms, Inventory, Dialogs, Characters, Views, Audio, GUIs, Fonts, Cursors als Tabellen | Viewer + Gatherer aus `Game`-API |
| [`AGS.Plugin.CursorAgent`](AGS.Plugin.CursorAgent/) | Experiment: Script-Kontext erfassen, Patch-Vorschau/-Apply, Cursor-Response-Import | Fortgeschritten / Agent-Prototyp |

Jedes Plugin ist eine **eigene Class Library** mit eigenem `.csproj` / `.sln` und liefert **eine DLL**, die du in den AGS-Editor-Ordner legst. Mehrere Plugins können gleichzeitig geladen werden, solange ihre **Component-IDs** eindeutig sind.

---

## Voraussetzungen

- **AGS 3.6** (Editor + `AGS.Types.dll`)
- **Visual Studio** oder Build Tools mit MSBuild
- **.NET Framework 4.7.2** (in den Plugin-Projekten eingestellt)
- Optional: **Python 3** — zum Vergleich mit den Referenz-Skripten in Speech Viewer / Entity Catalog

---

## Einmalige Einrichtung

1. **`AGS.Types.dll` bereitstellen** (pro Rechner einmal):

   ```text
   dependencies/AGS.Types.dll
   ```

   Anleitung: [`dependencies/README.md`](dependencies/README.md)

2. **Plugin bauen** (Beispiel Speech Viewer):

   ```powershell
   msbuild AGS.Plugin.SpeechViewer\AGS.Plugin.SpeechViewer.csproj /p:Configuration=Debug
   ```

   Oder die `.sln` im jeweiligen Plugin-Ordner in Visual Studio öffnen.

3. **DLL deployen**: `bin\Debug\AGS.Plugin.<Name>.dll` in den **AGS Editor-Installationsordner** kopieren (dort liegen auch die anderen Editor-DLLs).

4. **AGS Editor neu starten**, ein Spiel öffnen, Menü / Project Tree des Plugins prüfen.

---

## Neues Plugin anlegen (Kurzfassung)

1. **`AGS.Plugin.Sample`** oder ein fertiges Viewer-Plugin als Vorlage kopieren:

   ```text
   AGS.Plugin.Sample  →  AGS.Plugin.MeinPlugin
   ```

2. **Umbenennen** (konsistent in allen Dateien):
   - Namespace / AssemblyName im `.csproj`
   - Klassen (`PluginMain`, `…Component`, `…Pane`)
   - Menü- und Tree-Labels

3. **Eindeutige IDs** setzen (sonst kollidieren Plugins):

   ```csharp
   private const string ComponentId = "MeinPluginComponent";
   private const string RootNodeControlId = "MeinPluginRootNode";
   // … weitere MenuCommand-IDs
   ```

4. **`[RequiredAGSVersion("3.6.0.0")]`** am Plugin-Einstieg belassen oder anpassen.

5. Referenz auf `..\dependencies\AGS.Types.dll` beibehalten.

6. Bauen, DLL deployen, testen.

Typisches Gerüst (wie im Sample):

- `PluginMain` implementiert `IAGSEditorPlugin` und registriert eine Component.
- `…Component` implementiert `IEditorComponent` (Menü, Project-Tree, `RefreshDataFromGame`).
- `…Pane` erbt von `EditorContentPanel` (Dockable Panel UI).

Die Viewer-Plugins (Speech Viewer, Entity Catalog) zeigen ein erweitertes Muster: **Gatherer** (Daten aus `IAGSEditor.CurrentGame`), **Pane** mit Tree + Grid, **Filter**, optional **HTML-Export**.

---

## Python-Referenzskripte

Einige Plugins haben ein begleitendes Python-Skript als **Spezifikation / Offline-Export** (nicht Teil der DLL):

| Plugin | Skript |
|--------|--------|
| Speech Viewer | [`voice_script_export.py`](AGS.Plugin.SpeechViewer/voice_script_export.py) |
| Entity Catalog | [`agf_entity_catalog.py`](AGS.Plugin.EntityCatalog/agf_entity_catalog.py) |

Die C#-Plugins lesen bevorzugt **Live-Daten** aus dem Editor (`CurrentGame`), inkl. uns gespeicherter Änderungen.

---

## Hub / GitHub Pages

Die öffentliche **One-Page-Übersicht** aller Plugins wird aus `hub/` nach **`docs/`** gebaut (für GitHub Pages: Branch `main`, Ordner **`/docs`**).

**Lokal bauen:**

```powershell
pip install -r hub/requirements.txt
python hub/build.py
```

Oder `hub\build.bat` — Vorschau mit `hub\serve.bat` → [http://localhost:8000/](http://localhost:8000/).

Neue **`AGS.Plugin.*`**-Ordner im Repo-Root erscheinen automatisch in der Plugin-Tabelle. Optional pro Plugin:

- **`hub.blurb`** — eine Zeile Kurzbeschreibung für die Hub-Tabelle
- **`PLUGIN.md`** — erster Absatz wird als Beschreibung verwendet

Nach Push auf `main`/`master` baut die GitHub Action [`.github/workflows/build-hub.yml`](.github/workflows/build-hub.yml) die Seite neu und committet `docs/index.html` sowie `docs/assets/`.

**GitHub Pages aktivieren (einmalig):**

1. Repo auf GitHub pushen
2. **Settings → Pages → Build and deployment → Deploy from branch `main` → `/docs`**
3. Unter **Site URL** die veröffentlichte Adresse ablesen

In `hub/build.py` → `SITE["repo_url"]` die GitHub-URL eintragen, damit Plugin-Links in der Tabelle auf die Source-Ordner zeigen (statt relativer Pfade).

Private Notizen: **`docs/private/`** (gitignored, wird nicht gebaut).

Details zum Hub-Build: [`hub/README.md`](hub/README.md).

---

## Repo-Struktur

```text
dependencies/          # AGS.Types.dll (lokal, siehe README dort)
AGS.Plugin.Sample/     # Minimalvorlage
AGS.Plugin.SpeechViewer/
AGS.Plugin.EntityCatalog/
AGS.Plugin.CursorAgent/
hub/                   # Site-Generator (Markdown → docs/)
docs/                  # Generierte Pages + Planungsnotizen; docs/private/ lokal
```

Build-Artefakte (`bin/`, `obj/`, `.vs/`) und Backup-/Copy-Ordner sind in [`.gitignore`](.gitignore) ausgeschlossen.

---

## Hinweise für Forks & Veröffentlichung

- Pro Plugin **eigenes Prefix** für IDs und Assembly-Namen wählen.
- `AGS.Types.dll` nicht committen — nur lokal unter `dependencies/`.
- Viewer-Plugins sind **read-only** gegenüber dem Spiel; Cursor Agent **ändert** Script-Text — immer mit Vorsicht und Preview testen.
- Lizenz: noch nicht festgelegt — vor öffentlichem Release eine LICENSE-Datei ergänzen (z. B. MIT), falls gewünscht.

---

## Weiterführend

- [`docs/phase1-setup.md`](docs/phase1-setup.md) — Cursor Agent Phase-1-Setup
- [`docs/cursor-agent-response-contract.md`](docs/cursor-agent-response-contract.md) — JSON-Vertrag für Agent-Responses (Cursor Agent)

Feedback und PRs willkommen, sobald das Repo auf GitHub steht.
