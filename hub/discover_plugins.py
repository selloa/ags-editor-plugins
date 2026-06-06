#!/usr/bin/env python3
"""Discover AGS.Plugin.* folders and generate hub/content/index.md."""

from __future__ import annotations

import re
from dataclasses import dataclass
from datetime import date
from pathlib import Path

HUB_ROOT = Path(__file__).resolve().parent
REPO_ROOT = HUB_ROOT.parent
CONTENT_PATH = HUB_ROOT / "content" / "index.md"

PLUGIN_DIR_RE = re.compile(r"^AGS\.Plugin\.", re.IGNORECASE)
SKIP_DIR_PARTS = (" copy", "Backup", "Backup1")
TREE_ROOT_RE = re.compile(
    r'AddTreeRoot\s*\([^,]+,\s*[^,]+,\s*"([^"]+)"',
    re.MULTILINE,
)
REQUIRED_AGS_RE = re.compile(
    r'\[RequiredAGSVersion\s*\(\s*"([^"]+)"\s*\)\]',
    re.MULTILINE,
)
ASSEMBLY_RE = re.compile(r"<AssemblyName>([^<]+)</AssemblyName>")


@dataclass
class PluginInfo:
    folder: str
    display_name: str
    assembly: str
    ags_version: str
    description: str
    source_href: str


def should_skip_dir(name: str) -> bool:
    if not PLUGIN_DIR_RE.match(name):
        return True
    lower = name.lower()
    return any(part.lower() in lower for part in SKIP_DIR_PARTS)


def find_component_file(plugin_dir: Path) -> Path | None:
    candidates: list[Path] = []
    for path in plugin_dir.glob("*Component.cs"):
        rel = path.relative_to(plugin_dir).as_posix()
        if "Backup" in rel:
            continue
        candidates.append(path)
    if not candidates:
        return None
    return sorted(candidates)[0]


def read_display_name(component_file: Path) -> str:
    text = component_file.read_text(encoding="utf-8", errors="replace")
    match = TREE_ROOT_RE.search(text)
    if match:
        return match.group(1).strip()
    return component_file.parent.name.replace("AGS.Plugin.", "").replace(".", " ")


def read_assembly_name(plugin_dir: Path) -> str:
    csproj_files = sorted(plugin_dir.glob("*.csproj"))
    if not csproj_files:
        return plugin_dir.name
    text = csproj_files[0].read_text(encoding="utf-8", errors="replace")
    match = ASSEMBLY_RE.search(text)
    return match.group(1).strip() if match else plugin_dir.name


def read_ags_version(plugin_dir: Path) -> str:
    for name in ("PluginMain.cs", "SamplePlugin.cs"):
        path = plugin_dir / name
        if not path.is_file():
            continue
        text = path.read_text(encoding="utf-8", errors="replace")
        match = REQUIRED_AGS_RE.search(text)
        if match:
            return match.group(1).strip()
    return "—"


def read_description(plugin_dir: Path) -> str:
    blurb = plugin_dir / "hub.blurb"
    if blurb.is_file():
        line = blurb.read_text(encoding="utf-8", errors="replace").strip().splitlines()
        if line and line[0].strip():
            return line[0].strip()

    plugin_md = plugin_dir / "PLUGIN.md"
    if plugin_md.is_file():
        body = plugin_md.read_text(encoding="utf-8", errors="replace").strip()
        if body.startswith("---"):
            end = body.find("\n---", 3)
            if end >= 0:
                body = body[end + 4 :].strip()
        for para in re.split(r"\n\s*\n", body):
            para = para.strip()
            if para and not para.startswith("#"):
                return re.sub(r"\s+", " ", para)

    return "AGS-Editor-Plugin (.NET)"


def discover_plugins(repo_root: Path, repo_url: str, default_branch: str = "main") -> list[PluginInfo]:
    plugins: list[PluginInfo] = []
    for entry in sorted(repo_root.iterdir()):
        if not entry.is_dir() or should_skip_dir(entry.name):
            continue
        if not list(entry.glob("*.csproj")):
            continue

        component = find_component_file(entry)
        display_name = read_display_name(component) if component else entry.name
        assembly = read_assembly_name(entry)
        ags_version = read_ags_version(entry)
        description = read_description(entry)

        if repo_url:
            source_href = f"{repo_url.rstrip('/')}/tree/{default_branch}/{entry.name}"
        else:
            source_href = f"../{entry.name}/"

        plugins.append(
            PluginInfo(
                folder=entry.name,
                display_name=display_name,
                assembly=assembly,
                ags_version=ags_version,
                description=description,
                source_href=source_href,
            )
        )
    return plugins


def plugins_table(plugins: list[PluginInfo]) -> str:
    if not plugins:
        return "_Keine `AGS.Plugin.*`-Ordner gefunden._\n"

    lines = [
        "| Plugin | Ordner | AGS | Beschreibung |",
        "| --- | --- | --- | --- |",
    ]
    for plugin in plugins:
        name_link = f"[{plugin.display_name}]({plugin.source_href})"
        folder_cell = f"`{plugin.folder}`"
        ags_cell = f"`{plugin.ags_version}`"
        desc = plugin.description.replace("|", "\\|")
        lines.append(f"| {name_link} | {folder_cell} | {ags_cell} | {desc} |")
    return "\n".join(lines) + "\n"


def schnellstart_section() -> str:
    return """## Schnellstart

1. **`AGS.Types.dll`** aus deiner AGS-Installation nach `dependencies/AGS.Types.dll` kopieren (siehe [`dependencies/README.md`](../dependencies/README.md)).
2. Plugin-Projekt bauen (Visual Studio oder MSBuild), z. B. `AGS.Plugin.SpeechViewer`.
3. Die **`bin\\Debug\\*.dll`** in den AGS-Editor-Ordner legen und den Editor neu starten.

Neues Plugin: [`AGS.Plugin.Sample`](../AGS.Plugin.Sample/) kopieren, IDs umbenennen, bauen, deployen.

Ausführliche Doku: [`README.md`](../README.md) im Repo-Root.
"""


def build_notes_section() -> str:
    return """## Page build notes

- `hub/content/index.md` wird von `discover_plugins.py` vor jedem Build neu geschrieben.
- Optionale Plugin-Beschreibung: `hub.blurb` (eine Zeile) oder `PLUGIN.md` im Plugin-Ordner.
- Private Notizen: `docs/private/` (gitignored).
"""


def generate_index_md(plugins: list[PluginInfo], repo_url: str) -> str:
    today = date.today().isoformat()
    plugin_count = len(plugins)

    return f"""---
title: AGS Editor Plugin Collection
version: 1.0.0
date: {today}
status: published
description: Forkbare AGS 3.6 Editor-Plugins — Sample, Viewer und Experimente.
---

# AGS Editor Plugin Collection

*Sammlung von Adventure Game Studio Editor-Plugins (.NET), die du forken und erweitern kannst.*

Diese Seite listet automatisch alle **`AGS.Plugin.*`**-Ordner im Repository ({plugin_count} Plugins). Nach jedem Push wird sie per GitHub Action neu gebaut; Veröffentlichung über **GitHub Pages** aus dem Ordner **`docs/`**.

Offizielle AGS-Doku: [Editor Plugins](https://adventuregamestudio.github.io/ags-manual/EditorPlugins.html)

---

## Plugins

{plugins_table(plugins)}
---

{schnellstart_section()}

---

{build_notes_section()}
"""


def write_index_md(repo_url: str = "", default_branch: str = "main") -> list[PluginInfo]:
    plugins = discover_plugins(REPO_ROOT, repo_url, default_branch)
    CONTENT_PATH.parent.mkdir(parents=True, exist_ok=True)
    CONTENT_PATH.write_text(generate_index_md(plugins, repo_url), encoding="utf-8", newline="\n")
    return plugins


if __name__ == "__main__":
    from build import SITE

    found = write_index_md(
        SITE.get("repo_url", ""),
        SITE.get("default_branch", "main"),
    )
    print(f"Wrote {CONTENT_PATH.relative_to(HUB_ROOT)} ({len(found)} plugins)")
