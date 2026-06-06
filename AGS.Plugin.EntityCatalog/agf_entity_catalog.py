#!/usr/bin/env python3
"""
Extract room, inventory, dialog, character, view, audio clip, GUI, font, and mouse cursor
ID/name data from an AGS Game.agf (XML) and write a single HTML file with section
navigation and tables.

Usage:
  python scripts/agf_entity_catalog.py path/to/Game.agf [-o out.html]

Default output: <agf_dir>/<agf_basename>_entity_catalog.html
"""

from __future__ import annotations

import argparse
import html
import re
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict
from pathlib import Path


def _elem_text(el: ET.Element | None, tag: str) -> str:
    if el is None:
        return ""
    child = el.find(tag)
    if child is None or child.text is None:
        return ""
    return child.text.strip()


def _parse_int(s: str) -> int | None:
    s = (s or "").strip()
    if not s or not re.fullmatch(r"-?\d+", s):
        return None
    return int(s, 10)


def _game_name(game: ET.Element) -> str:
    settings = game.find("Settings")
    if settings is None:
        return ""
    gn = settings.find("GameName")
    if gn is None or gn.text is None:
        return ""
    return gn.text.strip()


def load_game_root(path: Path) -> ET.Element:
    tree = ET.parse(path)
    root = tree.getroot()
    game = root.find("Game")
    if game is None:
        raise SystemExit(f"{path}: missing <Game>")
    return game


def collect_rooms(rooms_root: ET.Element | None) -> list[tuple[int, str]]:
    out: list[tuple[int, str]] = []
    if rooms_root is None:
        return out
    for ur in rooms_root.iter("UnloadedRoom"):
        num_s = _elem_text(ur, "Number")
        n = _parse_int(num_s)
        if n is None:
            continue
        desc = _elem_text(ur, "Description")
        out.append((n, desc))
    out.sort(key=lambda t: t[0])
    return out


def collect_inventory(inv_root: ET.Element | None) -> list[tuple[int, str, str]]:
    out: list[tuple[int, str, str]] = []
    if inv_root is None:
        return out
    for item in inv_root.iter("InventoryItem"):
        id_s = _elem_text(item, "ID")
        n = _parse_int(id_s)
        if n is None:
            continue
        name = _elem_text(item, "Name")
        desc = _elem_text(item, "Description")
        out.append((n, name, desc))
    out.sort(key=lambda t: t[0])
    return out


def collect_dialogs(dlg_root: ET.Element | None) -> list[tuple[int, str]]:
    out: list[tuple[int, str]] = []
    if dlg_root is None:
        return out
    for dlg in dlg_root.iter("Dialog"):
        id_s = _elem_text(dlg, "ID")
        n = _parse_int(id_s)
        if n is None:
            continue
        name = _elem_text(dlg, "Name")
        out.append((n, name))
    out.sort(key=lambda t: t[0])
    return out


def _collect_character_used_views(ch: ET.Element) -> tuple[int, ...]:
    """
    Return sorted unique non-zero view IDs referenced by a Character.

    We intentionally scan all direct child tags ending with "View" to stay
    compatible with modern AGF variants and custom/added view fields.
    """
    used: set[int] = set()
    for child in list(ch):
        tag = child.tag
        if not isinstance(tag, str) or not tag.endswith("View"):
            continue
        if child.text is None:
            continue
        n = _parse_int(child.text.strip())
        if n is None or n <= 0:
            continue
        used.add(n)
    return tuple(sorted(used))


def collect_characters(ch_root: ET.Element | None) -> list[tuple[int, str, str, tuple[int, ...]]]:
    out: list[tuple[int, str, str, tuple[int, ...]]] = []
    if ch_root is None:
        return out
    for ch in ch_root.iter("Character"):
        id_s = _elem_text(ch, "ID")
        n = _parse_int(id_s)
        if n is None:
            continue
        script = _elem_text(ch, "ScriptName")
        real = _elem_text(ch, "RealName")
        used_views = _collect_character_used_views(ch)
        out.append((n, script, real, used_views))
    out.sort(key=lambda t: t[0])
    return out


def collect_views(views_root: ET.Element | None) -> list[tuple[int, str]]:
    out: list[tuple[int, str]] = []
    if views_root is None:
        return out
    for v in views_root.iter("View"):
        id_s = _elem_text(v, "ID")
        n = _parse_int(id_s)
        if n is None:
            continue
        name = _elem_text(v, "Name")
        out.append((n, name))
    out.sort(key=lambda t: t[0])
    return out


def _basename_only(path_str: str) -> str:
    s = (path_str or "").strip()
    if not s:
        return ""
    return Path(s.replace("\\", "/")).name


def collect_audio_clips(audio_root: ET.Element | None) -> list[tuple[int, str, str, str]]:
    """ID, script name, file type, source filename (basename only)."""
    out: list[tuple[int, str, str, str]] = []
    if audio_root is None:
        return out
    for clip in audio_root.iter("AudioClip"):
        id_s = _elem_text(clip, "ID")
        n = _parse_int(id_s)
        if n is None:
            continue
        script = _elem_text(clip, "ScriptName")
        ftype = _elem_text(clip, "FileType")
        src = _basename_only(_elem_text(clip, "SourceFileName"))
        out.append((n, script, ftype, src))
    out.sort(key=lambda t: t[0])
    return out


def collect_guis(guis_root: ET.Element | None) -> list[tuple[int, str]]:
    out: list[tuple[int, str]] = []
    if guis_root is None:
        return out
    for gm in guis_root.iter("GUIMain"):
        ng = gm.find("NormalGUI")
        if ng is None:
            continue
        id_s = _elem_text(ng, "ID")
        n = _parse_int(id_s)
        if n is None:
            continue
        name = _elem_text(ng, "Name")
        out.append((n, name))
    out.sort(key=lambda t: t[0])
    return out


def collect_fonts(fonts_root: ET.Element | None) -> list[tuple[int, str, str]]:
    """ID, name, source filename (may be empty for built-in)."""
    out: list[tuple[int, str, str]] = []
    if fonts_root is None:
        return out
    for font in fonts_root.iter("Font"):
        id_s = _elem_text(font, "ID")
        n = _parse_int(id_s)
        if n is None:
            continue
        name = _elem_text(font, "Name")
        src = _elem_text(font, "SourceFilename")
        out.append((n, name, src))
    out.sort(key=lambda t: t[0])
    return out


def collect_mouse_cursors(cursors_root: ET.Element | None) -> list[tuple[int, str, str]]:
    """ID, name, view ID (string)."""
    out: list[tuple[int, str, str]] = []
    if cursors_root is None:
        return out
    for cur in cursors_root.iter("MouseCursor"):
        id_s = _elem_text(cur, "ID")
        n = _parse_int(id_s)
        if n is None:
            continue
        name = _elem_text(cur, "Name")
        view = _elem_text(cur, "View")
        out.append((n, name, view))
    out.sort(key=lambda t: t[0])
    return out


def esc(s: str) -> str:
    return html.escape(s, quote=True) if s else ""


def cell(s: str, empty_emdash: bool = True) -> str:
    if not s and empty_emdash:
        return "<td>—</td>"
    return f"<td>{esc(s)}</td>"


def build_html(
    *,
    source_path: Path,
    game_title: str,
    rooms: list[tuple[int, str]],
    inventory: list[tuple[int, str, str]],
    dialogs: list[tuple[int, str]],
    characters: list[tuple[int, str, str, tuple[int, ...]]],
    views: list[tuple[int, str]],
    audio_clips: list[tuple[int, str, str, str]],
    guis: list[tuple[int, str]],
    fonts: list[tuple[int, str, str]],
    mouse_cursors: list[tuple[int, str, str]],
) -> str:
    title = f"AGF entity catalog — {source_path.name}"
    h1_extra = f" — {esc(game_title)}" if game_title else ""
    view_to_characters: dict[int, list[str]] = defaultdict(list)
    for _, script, real, used_views in characters:
        label = script if not real else f"{script} ({real})"
        for vid in used_views:
            view_to_characters[vid].append(label)

    nav = """<nav class="nav">
  <a href="#rooms">Rooms</a>
  <a href="#inventory">Inventory</a>
  <a href="#dialogs">Dialogs</a>
  <a href="#characters">Characters</a>
  <a href="#views">Views</a>
  <a href="#audio">Audio</a>
  <a href="#guis">GUIs</a>
  <a href="#fonts">Fonts</a>
  <a href="#cursors">Mouse cursors</a>
  <label class="search" for="catalog-search">Filter:</label>
  <input id="catalog-search" type="search" placeholder="Type to filter all sections" autocomplete="off">
</nav>"""

    def section_rooms() -> str:
        rows = "".join(f"<tr><td>{n}</td>{cell(desc)}</tr>" for n, desc in rooms)
        if not rows:
            rows = "<tr><td colspan=\"2\">No UnloadedRoom entries found.</td></tr>"
        return f"""<section id="rooms">
<h2>Rooms <span class="count">({len(rooms)})</span></h2>
<p class="note">Room numbers and descriptions as stored under <code>Game/Rooms</code> (<code>UnloadedRoom</code>).</p>
<table>
<thead><tr><th>Number</th><th>Description</th></tr></thead>
<tbody>{rows}</tbody>
</table>
</section>"""

    def section_inventory() -> str:
        rows = "".join(
            f"<tr><td>{r[0]}</td>{cell(r[1])}{cell(r[2])}</tr>" for r in inventory
        )
        if not rows:
            rows = "<tr><td colspan=\"3\">No inventory items found.</td></tr>"
        return f"""<section id="inventory">
<h2>Inventory items <span class="count">({len(inventory)})</span></h2>
<table>
<thead><tr><th>ID</th><th>Name</th><th>Description</th></tr></thead>
<tbody>{rows}</tbody>
</table>
</section>"""

    def section_dialogs() -> str:
        rows = "".join(f"<tr><td>{r[0]}</td>{cell(r[1])}</tr>" for r in dialogs)
        if not rows:
            rows = "<tr><td colspan=\"2\">No dialogs found.</td></tr>"
        return f"""<section id="dialogs">
<h2>Dialogs <span class="count">({len(dialogs)})</span></h2>
<table>
<thead><tr><th>ID</th><th>Name</th></tr></thead>
<tbody>{rows}</tbody>
</table>
</section>"""

    def section_characters() -> str:
        def _format_used_views(used_views: tuple[int, ...]) -> str:
            if not used_views:
                return ""
            return ", ".join(str(v) for v in used_views)

        rows = "".join(
            f"<tr><td>{r[0]}</td>{cell(r[1])}{cell(r[2])}{cell(_format_used_views(r[3]))}</tr>"
            for r in characters
        )
        if not rows:
            rows = "<tr><td colspan=\"4\">No characters found.</td></tr>"
        return f"""<section id="characters">
<h2>Characters <span class="count">({len(characters)})</span></h2>
<table>
<thead><tr><th>ID</th><th>Script name</th><th>Real name</th><th>Used view IDs</th></tr></thead>
<tbody>{rows}</tbody>
</table>
</section>"""

    def section_views() -> str:
        def _format_associated_characters(view_id: int) -> str:
            names = sorted(set(view_to_characters.get(view_id, [])))
            if not names:
                return ""
            return ", ".join(names)

        rows = "".join(
            f"<tr><td>{r[0]}</td>{cell(r[1])}{cell(_format_associated_characters(r[0]))}</tr>"
            for r in views
        )
        if not rows:
            rows = "<tr><td colspan=\"3\">No views found.</td></tr>"
        return f"""<section id="views">
<h2>Views <span class="count">({len(views)})</span></h2>
<table>
<thead><tr><th>ID</th><th>Name</th><th>Associated characters</th></tr></thead>
<tbody>{rows}</tbody>
</table>
</section>"""

    def section_audio() -> str:
        rows = "".join(
            f"<tr><td>{r[0]}</td>{cell(r[1])}{cell(r[2])}{cell(r[3])}</tr>"
            for r in audio_clips
        )
        if not rows:
            rows = "<tr><td colspan=\"4\">No audio clips found.</td></tr>"
        return f"""<section id="audio">
<h2>Audio clips <span class="count">({len(audio_clips)})</span></h2>
<p class="note">From <code>Game/AudioClips</code>. Source file column is basename only.</p>
<table>
<thead><tr><th>ID</th><th>Script name</th><th>File type</th><th>Source file</th></tr></thead>
<tbody>{rows}</tbody>
</table>
</section>"""

    def section_guis() -> str:
        rows = "".join(f"<tr><td>{r[0]}</td>{cell(r[1])}</tr>" for r in guis)
        if not rows:
            rows = "<tr><td colspan=\"2\">No GUIs found.</td></tr>"
        return f"""<section id="guis">
<h2>GUIs <span class="count">({len(guis)})</span></h2>
<p class="note"><code>GUIMain</code> / <code>NormalGUI</code> entries under <code>Game/GUIs</code>.</p>
<table>
<thead><tr><th>ID</th><th>Name</th></tr></thead>
<tbody>{rows}</tbody>
</table>
</section>"""

    def section_fonts() -> str:
        rows = "".join(
            f"<tr><td>{r[0]}</td>{cell(r[1])}{cell(r[2])}</tr>" for r in fonts
        )
        if not rows:
            rows = "<tr><td colspan=\"3\">No fonts found.</td></tr>"
        return f"""<section id="fonts">
<h2>Fonts <span class="count">({len(fonts)})</span></h2>
<table>
<thead><tr><th>ID</th><th>Name</th><th>Source file</th></tr></thead>
<tbody>{rows}</tbody>
</table>
</section>"""

    def section_cursors() -> str:
        rows = "".join(
            f"<tr><td>{r[0]}</td>{cell(r[1])}{cell(r[2])}</tr>" for r in mouse_cursors
        )
        if not rows:
            rows = "<tr><td colspan=\"3\">No mouse cursors found.</td></tr>"
        return f"""<section id="cursors">
<h2>Mouse cursors <span class="count">({len(mouse_cursors)})</span></h2>
<table>
<thead><tr><th>ID</th><th>Name</th><th>View</th></tr></thead>
<tbody>{rows}</tbody>
</table>
</section>"""

    css = """
body { font-family: system-ui, Segoe UI, sans-serif; margin: 0; background: #f6f7f9; color: #1a1a1a; }
.wrap { max-width: 960px; margin: 0 auto; padding: 1rem 1.25rem 3rem; }
.nav {
  display: flex; flex-wrap: wrap; gap: 0.5rem 1rem;
  padding: 0.75rem 0; margin-bottom: 1rem;
  border-bottom: 1px solid #ccc; position: sticky; top: 0; background: #f6f7f9; z-index: 2;
}
.nav a { color: #0b57d0; text-decoration: none; }
.nav a:hover { text-decoration: underline; }
.search {
  margin-left: auto;
  font-size: 0.88rem;
  color: #333;
  align-self: center;
  white-space: nowrap;
}
#catalog-search {
  min-width: 10rem;
  max-width: 14rem;
  width: 14rem;
  padding: 0.25rem 0.4rem;
  border: 1px solid #bbb;
  border-radius: 4px;
  font: inherit;
}
h1 { font-size: 1.35rem; margin: 0 0 0.5rem; }
.meta { font-size: 0.9rem; color: #444; margin-bottom: 0.5rem; word-break: break-all; }
section { margin-top: 2rem; scroll-margin-top: 5.25rem; }
h2 { font-size: 1.15rem; margin: 0 0 0.75rem; }
.count { font-weight: normal; color: #555; font-size: 0.95rem; }
.note { font-size: 0.88rem; color: #555; margin: 0 0 0.75rem; }
table { width: 100%; border-collapse: collapse; background: #fff; box-shadow: 0 1px 2px rgba(0,0,0,.06); }
th, td { border: 1px solid #ddd; padding: 0.4rem 0.55rem; text-align: left; }
th { background: #e8eaef; font-weight: 600; }
tbody tr:nth-child(even) { background: #fafbfc; }
code { font-size: 0.9em; }
"""

    js = """
document.addEventListener("DOMContentLoaded", function () {
  const input = document.getElementById("catalog-search");
  if (!input) return;

  const tables = Array.from(document.querySelectorAll("section table"));

  function normalizeTokens(text) {
    return (text || "")
      .toLowerCase()
      .trim()
      .split(/\\s+/)
      .filter(Boolean);
  }

  function rowMatches(row, tokens) {
    if (tokens.length === 0) return true;
    const haystack = (row.textContent || "").toLowerCase();
    // AND semantics: every token must exist in row text.
    return tokens.every((token) => haystack.includes(token));
  }

  function applyFilter() {
    const tokens = normalizeTokens(input.value);
    for (const table of tables) {
      const rows = Array.from(table.querySelectorAll("tbody tr"));
      for (const row of rows) {
        const show = rowMatches(row, tokens);
        row.style.display = show ? "" : "none";
      }
    }
  }

  input.addEventListener("input", applyFilter);
});
"""

    body = f"""<div class="wrap">
{nav}
<h1>{esc(title)}{h1_extra}</h1>
<p class="meta">Source: {esc(str(source_path.resolve()))}</p>
{section_rooms()}
{section_inventory()}
{section_dialogs()}
{section_characters()}
{section_views()}
{section_audio()}
{section_guis()}
{section_fonts()}
{section_cursors()}
</div>"""

    return f"""<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{esc(title)}</title>
<style>{css}</style>
<script>{js}</script>
</head>
<body>
{body}
</body>
</html>
"""


def main() -> None:
    p = argparse.ArgumentParser(description="Build HTML entity catalog from AGS Game.agf")
    p.add_argument("agf", type=Path, help="Path to Game.agf")
    p.add_argument(
        "-o",
        "--output",
        type=Path,
        default=None,
        help="Output HTML path (default: next to .agf as <name>_entity_catalog.html)",
    )
    args = p.parse_args()
    agf_path = args.agf.expanduser().resolve()
    if not agf_path.is_file():
        print(f"Not a file: {agf_path}", file=sys.stderr)
        raise SystemExit(1)

    out_path = args.output
    if out_path is None:
        out_path = agf_path.parent / f"{agf_path.stem}_entity_catalog.html"
    else:
        out_path = out_path.expanduser().resolve()

    game = load_game_root(agf_path)
    gname = _game_name(game)

    rooms = collect_rooms(game.find("Rooms"))
    inventory = collect_inventory(game.find("InventoryItems"))
    dialogs = collect_dialogs(game.find("Dialogs"))
    characters = collect_characters(game.find("Characters"))
    views = collect_views(game.find("Views"))
    audio_clips = collect_audio_clips(game.find("AudioClips"))
    guis = collect_guis(game.find("GUIs"))
    fonts = collect_fonts(game.find("Fonts"))
    mouse_cursors = collect_mouse_cursors(game.find("Cursors"))

    doc = build_html(
        source_path=agf_path,
        game_title=gname,
        rooms=rooms,
        inventory=inventory,
        dialogs=dialogs,
        characters=characters,
        views=views,
        audio_clips=audio_clips,
        guis=guis,
        fonts=fonts,
        mouse_cursors=mouse_cursors,
    )
    out_path.write_text(doc, encoding="utf-8")
    print(f"Wrote {out_path}")


if __name__ == "__main__":
    main()
