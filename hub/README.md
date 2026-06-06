# Hub Build System

Statischer Site-Generator für die **One-Page-Homepage** dieses Repos. Fork des template-hub — angepasst für automatisches Scannen von `AGS.Plugin.*`-Ordnern.

## Ausgabe

| Quelle | Ziel |
|--------|------|
| `hub/content/index.md` (generiert) | `docs/index.html` |
| `hub/assets/*` | `docs/assets/` |

`docs/private/` wird nicht gebaut und ist in `.gitignore` — für lokale Notizen.

## Lokal bauen

```powershell
pip install -r hub/requirements.txt
python hub/build.py
```

Oder unter Windows:

```text
hub\build.bat
```

Vorschau im Browser:

```text
hub\serve.bat
```

→ [http://localhost:8000/](http://localhost:8000/) (serviert den Ordner `docs/`)

## Plugin-Discovery

Vor jedem Build schreibt `discover_plugins.py` die Datei `hub/content/index.md` neu:

- Scannt **Top-Level**-Ordner `AGS.Plugin.*` im Repo-Root
- Ausschluss: `* copy*`, `Backup*`, Ordner ohne `.csproj`
- Metadaten aus `*Component.cs` (Tree-Label), `.csproj` (`AssemblyName`), `PluginMain.cs` (`RequiredAGSVersion`)
- Optionale Beschreibung: `hub.blurb` (eine Zeile) oder erster Absatz in `PLUGIN.md`

## Konfiguration

In `hub/build.py`, Block `SITE`:

- `repo_url` — GitHub-URL des Repos (z. B. `https://github.com/USER/REPO`). Wenn gesetzt, verlinken Plugin-Namen in der Tabelle auf `…/tree/main/AGS.Plugin.X`. Leer lassen für relative Pfade (lokale Vorschau).
- `author_name`, `author_url` — Footer / Meta-Zeile

## CI

`.github/workflows/build-hub.yml` baut bei Push auf `main`/`master` (wenn Plugins, `hub/`, `README.md` oder der Workflow selbst geändert wurden) und committet geänderte `docs/`-Dateien.

GitHub Pages aktivieren: **Settings → Pages → Deploy from branch `main` → Folder `/docs`**.
