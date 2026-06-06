#!/usr/bin/env python3
"""
Export default-language voice lines from Game.agf dialog trees and root *.asc scripts.

- Dialogs: option labels and SPEAKER: lines (grouped by @N branch). Ignores *.trs.
- Scripts: DisplaySpeech(first, "literal") and player.Say("literal") with string literals only.
  Non-literal / format strings are skipped or tagged (dynamic).

Usage:
  python scripts/voice_script_export.py [path/to/Game.agf] [--format html|markdown] [-o out.html]

Default Game.agf: repo root Game.agf (parent of scripts/).
Default output: <repo>/voice-work/VoiceSpeech.html (HTML with nav); use --format markdown for .md.

Portability (other AGS projects): Expects Editor-style XML Game.agf (Game/Dialogs/Dialog with Script
CDATA). Scripted speech is only collected from *.asc files in the same directory as Game.agf — not
subfolders. Patterns matched: dialog SPEAKER: lines, DisplaySpeech(..., "literal"), player.Say("...").
Games that rely on cEdna.Say, GetTranslation, or non-literal DisplaySpeech will be incomplete unless
extended.
"""

from __future__ import annotations

import argparse
import html as html_module
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

# Match clear_dialog_trs_translations.py — dialog script directives, not spoken lines.
SKIP_SCRIPT_PREFIXES = (
    "goto-dialog",
    "goto-previous",
    "run-script",
    "option-off",
    "set-speech-view",
    "return",
    "stop",
    "activate",
)

SPEAKER_RE = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*$")
RE_DISPLAY = re.compile(r"DisplaySpeech\s*\(", re.IGNORECASE)
RE_SAY = re.compile(r"\bplayer\.Say\s*\(", re.IGNORECASE)
RE_BRANCH = re.compile(r"^@(\d+)\b")
RE_INT_ARG = re.compile(r"^\s*(\d+)\s*$")


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


def load_game_root(path: Path) -> ET.Element:
    tree = ET.parse(path)
    root = tree.getroot()
    game = root.find("Game")
    if game is None:
        raise SystemExit(f"{path}: missing <Game>")
    return game


def collect_characters(game: ET.Element) -> dict[int, tuple[str, str]]:
    """id -> (ScriptName, RealName)."""
    out: dict[int, tuple[str, str]] = {}
    ch_root = game.find("Characters")
    if ch_root is None:
        return out
    for ch in ch_root.iter("Character"):
        n = _parse_int(_elem_text(ch, "ID"))
        if n is None:
            continue
        script = _elem_text(ch, "ScriptName")
        real = _elem_text(ch, "RealName")
        out[n] = (script, real)
    return out


def player_character_id(game: ET.Element) -> int | None:
    pc = game.find("PlayerCharacter")
    if pc is None or pc.text is None:
        return None
    return _parse_int(pc.text.strip())


def char_label(chars: dict[int, tuple[str, str]], cid: int | None) -> str:
    if cid is None:
        return "(unknown)"
    t = chars.get(cid)
    if not t:
        return f"ID {cid} (not in Game.agf)"
    script, real = t
    if real and real != script:
        return f"{script} — {real}"
    return script or f"ID {cid}"


def md_cell(s: str) -> str:
    s = s.replace("\r\n", "\n").replace("\r", "\n")
    s = s.replace("\n", " ").replace("|", "\\|")
    return s


def parse_quoted_string(src: str, start: int) -> tuple[str | None, int]:
    """
    Parse AGS-style string starting at start (must be ' or \").
    Returns (decoded, index_after_closing_quote) or (None, start) on failure.
    """
    if start >= len(src):
        return None, start
    q = src[start]
    if q not in "\"'":
        return None, start
    i = start + 1
    out: list[str] = []
    while i < len(src):
        c = src[i]
        if c == "\\" and i + 1 < len(src):
            out.append(src[i + 1])
            i += 2
            continue
        if c == q:
            return "".join(out), i + 1
        out.append(c)
        i += 1
    return None, start


def skip_ws(s: str, i: int) -> int:
    while i < len(s) and s[i] in " \t":
        i += 1
    return i


def find_matching_paren(s: str, open_idx: int) -> int | None:
    """open_idx points at '('. Returns index after ')' or None."""
    depth = 0
    i = open_idx
    while i < len(s):
        c = s[i]
        if c == "(":
            depth += 1
        elif c == ")":
            depth -= 1
            if depth == 0:
                return i + 1
        elif c in "\"'":
            lit, j = parse_quoted_string(s, i)
            if lit is None:
                return None
            i = j
            continue
        i += 1
    return None


def extract_display_speech_calls(
    line: str, line_no: int, basename: str
) -> list[tuple[str, str, str, str]]:
    """
    Returns list of (id, char_expr, text, note) for literal second-arg DisplaySpeech on this line.
    """
    results: list[tuple[str, str, str, str]] = []
    for m in RE_DISPLAY.finditer(line):
        lp = line.find("(", m.end() - 1)
        if lp < 0:
            continue
        end_call = find_matching_paren(line, lp)
        if end_call is None:
            continue
        inner = line[lp + 1 : end_call - 1]
        comma = _first_top_level_comma(inner)
        if comma is None:
            continue
        first = inner[:comma].strip()
        rest = inner[comma + 1 :]
        j = skip_ws(rest, 0)
        text, _ = parse_quoted_string(rest, j)
        if text is None:
            continue
        note = ""
        if "%" in text:
            note = " (contains % — verify if dynamic)"
        vid = f"asc-{basename}-L{line_no}"
        results.append((vid, first, text, note))
    return results


def extract_player_say_calls(
    line: str, line_no: int, basename: str
) -> list[tuple[str, str, str]]:
    """Returns (id, text, note)."""
    results: list[tuple[str, str, str]] = []
    for m in RE_SAY.finditer(line):
        lp = line.find("(", m.end() - 1)
        if lp < 0:
            continue
        end_call = find_matching_paren(line, lp)
        if end_call is None:
            continue
        inner = line[lp + 1 : end_call - 1].strip()
        j = skip_ws(inner, 0)
        text, _ = parse_quoted_string(inner, j)
        if text is None:
            continue
        note = ""
        if "%" in text:
            note = "dynamic / verify — format string"
        vid = f"asc-{basename}-L{line_no}-say"
        results.append((vid, text, note))
    return results


def _first_top_level_comma(s: str) -> int | None:
    depth = 0
    i = 0
    while i < len(s):
        c = s[i]
        if c in "\"'":
            lit, j = parse_quoted_string(s, i)
            if lit is None:
                return None
            i = j
            continue
        if c == "(":
            depth += 1
        elif c == ")":
            depth -= 1
        elif c == "," and depth == 0:
            return i
        i += 1
    return None


def resolve_speaker_expr(expr: str, player_id: int | None, chars: dict[int, tuple[str, str]]) -> str:
    e = expr.strip()
    if e == "player.ID":
        return f"player → {char_label(chars, player_id)}"
    mi = RE_INT_ARG.match(e)
    if mi:
        return char_label(chars, int(mi.group(1), 10))
    return e


def parse_dialog_script_body(script_body: str, dialog_id: int) -> list[tuple[str, str, str, str]]:
    """
    Speech-only rows: (branch_tag, line_id, speaker, message).
    branch_tag is 'root' or '@N'; line_id is dlg-{id}-root-L{n} or dlg-{id}-@N-L{n}.
    """
    rows: list[tuple[str, str, str, str]] = []
    current_branch = "root"
    seq_by_branch: dict[str, int] = {}

    if not script_body:
        return []

    for raw in script_body.splitlines():
        line = raw.strip()
        if not line:
            continue
        if line.startswith("//"):
            continue
        bm = RE_BRANCH.match(line)
        if bm:
            current_branch = f"@{bm.group(1)}"
            if current_branch not in seq_by_branch:
                seq_by_branch[current_branch] = 0
            continue
        if line.startswith("@"):
            continue
        low = line.lower()
        if any(low.startswith(p) for p in SKIP_SCRIPT_PREFIXES):
            continue
        if ":" not in line:
            continue
        speaker, _, msg = line.partition(":")
        speaker = speaker.strip()
        msg = msg.strip()
        if not msg or not speaker:
            continue
        if not SPEAKER_RE.match(speaker):
            continue
        if "root" not in seq_by_branch:
            seq_by_branch["root"] = 0
        seq_by_branch[current_branch] = seq_by_branch.get(current_branch, 0) + 1
        n = seq_by_branch[current_branch]
        branch_tag = current_branch if current_branch != "root" else "root"
        sid = f"dlg-{dialog_id}-{branch_tag}-L{n}"
        rows.append((branch_tag, sid, speaker, msg))

    return rows


def collect_dialog_option_rows(dlg: ET.Element, dialog_id: int) -> list[tuple[str, str]]:
    """(option_id, text) non-empty text only."""
    out: list[tuple[str, str]] = []
    for opt in dlg.findall("DialogOptions/DialogOption"):
        oid = _parse_int(_elem_text(opt, "ID"))
        if oid is None:
            continue
        for t in opt.findall("Text"):
            tx = (t.text or "").strip()
            if tx:
                out.append((str(oid), tx))
            break
    return out


def _branch_sort_key(b: str) -> tuple[int, int]:
    return (0, int(b[1:])) if b.startswith("@") else (-1, 0)


def _html_id(*parts: str) -> str:
    raw = "-".join(parts)
    s = re.sub(r"[^a-zA-Z0-9_-]+", "-", raw.strip())
    s = s.strip("-").lower()
    return s or "section"


def gather_voice_document(
    game: ET.Element,
    agf_path: Path,
    repo_root: Path,
) -> tuple[dict, int, int, int, int]:
    """
    Build a plain dict for Markdown / HTML renderers.
    Keys: agf_name, repo_name, dialogs_missing, dialogs, asc_sections, script_ds, script_say.
    Each dialog: id, name, anchor, options [(rid, text)], branches [{anchor, title, rows [(sid,sp,msg)]}].
    Each asc_section: filename, anchor, error?, rows [(vid, sp, msg, note)].
    """
    chars = collect_characters(game)
    player_id = player_character_id(game)

    dialog_root = game.find("Dialogs")
    dialog_count = 0
    dialog_speech_rows = 0
    dialogs_out: list[dict] = []
    dialogs_missing = dialog_root is None

    if dialog_root is not None:
        dialogs = list(dialog_root.iter("Dialog"))
        dialogs.sort(key=lambda d: (_parse_int(_elem_text(d, "ID")) or 0,))
        for dlg in dialogs:
            did = _parse_int(_elem_text(dlg, "ID"))
            if did is None:
                continue
            dname = _elem_text(dlg, "Name") or f"dialog_{did}"
            dialog_count += 1
            d_anchor = _html_id("dialog", str(did))

            opts = collect_dialog_option_rows(dlg, did)
            opt_rows: list[tuple[str, str]] = []
            for oid, tx in opts:
                opt_rows.append((f"dlg-{did}-opt-{oid}", tx))

            sc_el = dlg.find("Script")
            script_text = sc_el.text if sc_el is not None and sc_el.text else ""
            speech_rows = parse_dialog_script_body(script_text, did)

            branches_out: list[dict] = []
            if speech_rows:
                by_branch: dict[str, list[tuple[str, str, str]]] = {}
                for branch_tag, sid, speaker, msg in speech_rows:
                    by_branch.setdefault(branch_tag, []).append((sid, speaker, msg))

                for branch_tag in sorted(by_branch.keys(), key=_branch_sort_key):
                    dialog_speech_rows += len(by_branch[branch_tag])
                    title = "Root (before first `@N`)" if branch_tag == "root" else f"Branch {branch_tag}"
                    b_slug = "root" if branch_tag == "root" else branch_tag.lstrip("@")
                    b_anchor = _html_id("dialog", str(did), "b", b_slug)
                    branches_out.append(
                        {
                            "anchor": b_anchor,
                            "title": title,
                            "rows": by_branch[branch_tag],
                        }
                    )

            dialogs_out.append(
                {
                    "id": did,
                    "name": dname,
                    "anchor": d_anchor,
                    "options": opt_rows,
                    "branches": branches_out,
                    "has_speech": bool(branches_out),
                }
            )

    asc_files = sorted(repo_root.glob("*.asc"))
    script_ds = 0
    script_say = 0
    asc_sections: list[dict] = []

    for path in asc_files:
        stem = path.stem
        a_anchor = _html_id("asc", stem)
        try:
            text = path.read_text(encoding="utf-8", errors="replace")
        except OSError as e:
            asc_sections.append({"filename": path.name, "anchor": a_anchor, "error": str(e), "rows": []})
            continue

        file_rows: list[tuple[str, str, str, str]] = []
        for li, line in enumerate(text.splitlines(), start=1):
            for vid, first, msg, note in extract_display_speech_calls(line, li, stem):
                sp = resolve_speaker_expr(first, player_id, chars)
                note_suffix = note.strip() if note else ""
                file_rows.append((vid, sp, msg, note_suffix))
            for idx, (vid, msg, note) in enumerate(extract_player_say_calls(line, li, stem)):
                if idx > 0:
                    vid = f"{vid}-{idx + 1}"
                note_suffix = note.strip() if note else ""
                file_rows.append((vid, "player (Say)", msg, note_suffix))

        if not file_rows:
            continue

        script_ds += sum(1 for r in file_rows if not r[0].endswith("-say"))
        script_say += sum(1 for r in file_rows if r[0].endswith("-say"))
        asc_sections.append({"filename": path.name, "anchor": a_anchor, "rows": file_rows})

    doc = {
        "agf_name": agf_path.name,
        "repo_name": repo_root.name,
        "dialogs_missing": dialogs_missing,
        "dialogs": dialogs_out,
        "asc_sections": asc_sections,
    }
    return doc, dialog_count, dialog_speech_rows, script_ds, script_say


def format_markdown(doc: dict, dialog_count: int, dialog_speech_rows: int, script_ds: int, script_say: int) -> str:
    lines_out: list[str] = []
    lines_out.append("# Voice script export (default language)")
    lines_out.append("")
    lines_out.append(f"Generated from `{doc['agf_name']}` and `*.asc` under `{doc['repo_name']}/`.")
    lines_out.append("")
    lines_out.append("Translation files (`.trs`) are not used. Lines built from variables, ")
    lines_out.append("`StrFormat`, or non-literal arguments are omitted here unless noted.")
    lines_out.append("")
    lines_out.append(
        f"*Dialogs: {dialog_count} · Dialog script lines: {dialog_speech_rows} · "
        f"DisplaySpeech rows: {script_ds} · player.Say rows: {script_say}*"
    )
    lines_out.append("")

    lines_out.append("## Part 1 — Dialog trees (Game.agf)")
    lines_out.append("")

    if doc["dialogs_missing"]:
        lines_out.append("(No `<Dialogs>` section found.)")
        lines_out.append("")
    else:
        for d in doc["dialogs"]:
            did = d["id"]
            lines_out.append(f"### dlg-{did} — {md_cell(d['name'])}")
            lines_out.append("")
            if d["options"]:
                lines_out.append("**Player options**")
                lines_out.append("")
                lines_out.append("| ID | Text |")
                lines_out.append("| --- | --- |")
                for rid, tx in d["options"]:
                    lines_out.append(f"| `{rid}` | {md_cell(tx)} |")
                lines_out.append("")
            if not d["has_speech"]:
                lines_out.append("(No `SPEAKER:` lines in dialog script.)")
                lines_out.append("")
                continue
            for br in d["branches"]:
                lines_out.append(f"#### {br['title']}")
                lines_out.append("")
                lines_out.append("| Line ID | Speaker | Text |")
                lines_out.append("| --- | --- | --- |")
                for sid, speaker, msg in br["rows"]:
                    lines_out.append(f"| `{sid}` | {md_cell(speaker)} | {md_cell(msg)} |")
                lines_out.append("")

    lines_out.append("## Part 2 — Scripted speech (`*.asc`)")
    lines_out.append("")

    if script_ds == 0 and script_say == 0:
        lines_out.append("(No literal `DisplaySpeech` / `player.Say` lines found in root `*.asc`.)")
        lines_out.append("")
    else:
        for sec in doc["asc_sections"]:
            if sec.get("error"):
                lines_out.append(f"### {sec['filename']} — (read error: {sec['error']})")
                lines_out.append("")
                continue
            lines_out.append(f"### `{sec['filename']}`")
            lines_out.append("")
            lines_out.append("| Line ID | Speaker | Text | Notes |")
            lines_out.append("| --- | --- | --- | --- |")
            for vid, sp, msg, note in sec["rows"]:
                lines_out.append(
                    f"| `{vid}` | {md_cell(sp)} | {md_cell(msg)} |{md_cell(note)}|"
                )
            lines_out.append("")

    return "\n".join(lines_out) + "\n"


def format_html(doc: dict, dialog_count: int, dialog_speech_rows: int, script_ds: int, script_say: int) -> str:
    def esc(s: str) -> str:
        return html_module.escape(s, quote=True)

    parts: list[str] = []
    parts.append("<!DOCTYPE html>")
    parts.append('<html lang="en">')
    parts.append("<head>")
    parts.append('<meta charset="utf-8">')
    parts.append(f"<title>Voice script — {esc(doc['agf_name'])}</title>")
    parts.append("<style>")
    parts.append(
        """
:root { --bg: #f6f7f9; --fg: #1a1d26; --muted: #5c6575; --border: #d8dde6;
        --navw: 16rem; --accent: #2a6bb5; }
* { box-sizing: border-box; }
body { margin: 0; font-family: system-ui, Segoe UI, Roboto, sans-serif; background: var(--bg);
       color: var(--fg); line-height: 1.45; }
.layout { display: flex; min-height: 100vh; }
nav.toc { position: sticky; top: 0; align-self: flex-start; max-height: 100vh; overflow: auto;
          width: var(--navw); flex-shrink: 0; padding: 1rem 0.75rem 2rem;
          border-right: 1px solid var(--border); background: #fff; font-size: 0.875rem; }
nav.toc h2 { margin: 0 0 0.5rem; font-size: 0.75rem; text-transform: uppercase; letter-spacing: 0.04em;
            color: var(--muted); }
nav.toc a { color: var(--accent); text-decoration: none; display: block; padding: 0.2rem 0; }
nav.toc a:hover { text-decoration: underline; }
nav.toc ul { list-style: none; margin: 0 0 1rem; padding: 0 0 0 0.5rem; }
nav.toc ul ul { margin-top: 0.25rem; margin-bottom: 0.5rem; padding-left: 0.75rem;
                border-left: 1px solid var(--border); }
nav.toc li { margin: 0.1rem 0; }
main { flex: 1; padding: 1.5rem 2rem 3rem; max-width: 56rem; }
main > h1 { margin-top: 0; font-size: 1.5rem; }
.intro { color: var(--muted); margin-bottom: 1.5rem; font-size: 0.95rem; }
.stats { font-size: 0.85rem; color: var(--muted); margin-bottom: 2rem; }
section.block { margin-bottom: 2.5rem; }
section.block > h2 { font-size: 1.15rem; border-bottom: 2px solid var(--border); padding-bottom: 0.35rem; }
article.dlg, .sub { scroll-margin-top: 1rem; }
article.dlg { margin: 1.75rem 0; padding-top: 0.5rem; }
article.dlg h3 { font-size: 1.05rem; margin: 0 0 0.75rem; }
article.dlg h3 code { font-size: 0.95em; }
.sub { margin: 1rem 0 1rem; }
.sub h4 { font-size: 0.95rem; margin: 0 0 0.5rem; color: var(--muted); }
table { width: 100%; border-collapse: collapse; font-size: 0.88rem; margin: 0.5rem 0 1rem;
         background: #fff; border: 1px solid var(--border); border-radius: 6px; overflow: hidden; }
th, td { text-align: left; padding: 0.45rem 0.6rem; border-bottom: 1px solid var(--border); vertical-align: top; }
th { background: #eef1f6; font-weight: 600; }
tr:last-child td { border-bottom: none; }
td.id code, th:first-child { font-family: ui-monospace, Consolas, monospace; font-size: 0.82rem; }
td.msg { white-space: pre-wrap; }
nav.toc .file-link { font-size: 0.8rem; word-break: break-all; }
@media (max-width: 900px) {
  .layout { flex-direction: column; }
  nav.toc { position: relative; max-height: none; width: 100%; border-right: none;
            border-bottom: 1px solid var(--border); }
}
"""
    )
    parts.append("</style>")
    parts.append("</head>")
    parts.append("<body>")
    parts.append('<div class="layout">')
    parts.append('<nav class="toc" aria-label="Table of contents">')
    parts.append("<h2>Jump</h2>")
    parts.append('<ul><li><a href="#top">Overview</a></li>')
    parts.append('<li><a href="#part-dialogs">Part 1 — Dialog trees</a><ul>')

    if not doc["dialogs_missing"]:
        for d in doc["dialogs"]:
            parts.append(f'<li><a href="#{esc(d["anchor"])}">dlg-{d["id"]} — {esc(d["name"])}</a>')
            if d["branches"]:
                parts.append("<ul>")
                for br in d["branches"]:
                    parts.append(f'<li><a href="#{esc(br["anchor"])}">{esc(br["title"])}</a></li>')
                parts.append("</ul>")
            parts.append("</li>")

    parts.append("</ul></li>")
    parts.append('<li><a href="#part-asc">Part 2 — Scripted speech</a>')
    if doc["asc_sections"]:
        parts.append("<ul>")
        for sec in doc["asc_sections"]:
            if sec.get("error"):
                parts.append(f'<li><a href="#{esc(sec["anchor"])}">{esc(sec["filename"])}</a></li>')
            else:
                parts.append(
                    f'<li class="file-link"><a href="#{esc(sec["anchor"])}">{esc(sec["filename"])}</a></li>'
                )
        parts.append("</ul>")
    parts.append("</li></ul>")
    parts.append("</nav>")

    parts.append("<main>")
    parts.append('<h1 id="top">Voice script export</h1>')
    parts.append(
        f'<p class="intro">Default language (no <code>.trs</code>). Sources: <code>{esc(doc["agf_name"])}</code> '
        f'and <code>*.asc</code> in <code>{esc(doc["repo_name"])}/</code>. '
        "Variables / non-literal speech are omitted unless noted.</p>"
    )
    parts.append(
        f'<p class="stats">Dialogs: {dialog_count} · Dialog script lines: {dialog_speech_rows} · '
        f"DisplaySpeech rows: {script_ds} · player.Say rows: {script_say}</p>"
    )

    parts.append('<section class="block" id="part-dialogs">')
    parts.append("<h2>Part 1 — Dialog trees (Game.agf)</h2>")

    if doc["dialogs_missing"]:
        parts.append("<p>(No dialogs section in Game.agf.)</p>")
    else:
        for d in doc["dialogs"]:
            parts.append(f'<article class="dlg" id="{esc(d["anchor"])}">')
            parts.append(f"<h3><code>dlg-{d['id']}</code> — {esc(d['name'])}</h3>")
            if d["options"]:
                parts.append("<p><strong>Player options</strong></p>")
                parts.append("<table><thead><tr><th>ID</th><th>Text</th></tr></thead><tbody>")
                for rid, tx in d["options"]:
                    parts.append(
                        f'<tr><td class="id"><code>{esc(rid)}</code></td><td class="msg">{esc(tx)}</td></tr>'
                    )
                parts.append("</tbody></table>")
            if not d["has_speech"]:
                parts.append("<p><em>No SPEAKER: lines in dialog script.</em></p>")
                parts.append("</article>")
                continue
            for br in d["branches"]:
                parts.append(f'<div class="sub" id="{esc(br["anchor"])}">')
                parts.append(f"<h4>{esc(br['title'])}</h4>")
                parts.append(
                    "<table><thead><tr><th>Line ID</th><th>Speaker</th><th>Text</th></tr></thead><tbody>"
                )
                for sid, speaker, msg in br["rows"]:
                    parts.append(
                        f'<tr><td class="id"><code>{esc(sid)}</code></td><td>{esc(speaker)}</td>'
                        f'<td class="msg">{esc(msg)}</td></tr>'
                    )
                parts.append("</tbody></table></div>")
            parts.append("</article>")

    parts.append("</section>")

    parts.append('<section class="block" id="part-asc">')
    parts.append("<h2>Part 2 — Scripted speech (*.asc)</h2>")

    if script_ds == 0 and script_say == 0:
        parts.append("<p><em>No literal DisplaySpeech / player.Say lines found in root *.asc.</em></p>")
    else:
        for sec in doc["asc_sections"]:
            parts.append(f'<article class="dlg" id="{esc(sec["anchor"])}">')
            err = sec.get("error")
            if err:
                parts.append(f"<h3>{esc(sec['filename'])}</h3>")
                parts.append(f"<p><em>Read error: {esc(err)}</em></p>")
                parts.append("</article>")
                continue
            parts.append(f"<h3><code>{esc(sec['filename'])}</code></h3>")
            parts.append(
                "<table><thead><tr><th>Line ID</th><th>Speaker</th><th>Text</th><th>Notes</th></tr></thead><tbody>"
            )
            for vid, sp, msg, note in sec["rows"]:
                parts.append(
                    f'<tr><td class="id"><code>{esc(vid)}</code></td><td>{esc(sp)}</td>'
                    f'<td class="msg">{esc(msg)}</td><td>{esc(note)}</td></tr>'
                )
            parts.append("</tbody></table></article>")

    parts.append("</section>")
    parts.append("</main>")
    parts.append("</div>")
    parts.append("</body></html>")

    return "\n".join(parts) + "\n"


def main() -> None:
    repo_root = Path(__file__).resolve().parents[1]
    p = argparse.ArgumentParser(
        description="Export voice script (HTML or Markdown) from Game.agf and repo-root *.asc"
    )
    p.add_argument(
        "agf",
        type=Path,
        nargs="?",
        default=repo_root / "Game.agf",
        help="Path to Game.agf (default: <repo>/Game.agf)",
    )
    p.add_argument(
        "-o",
        "--output",
        type=Path,
        default=None,
        help="Output path (default: voice-work/VoiceSpeech.html or .md, from --format)",
    )
    p.add_argument(
        "--format",
        choices=("html", "markdown"),
        default="html",
        help="Output format (default: html). If -o has a .md or .html suffix, format follows the suffix.",
    )
    args = p.parse_args()
    agf_path = args.agf.expanduser().resolve()
    if not agf_path.is_file():
        print(f"Not a file: {agf_path}", file=sys.stderr)
        raise SystemExit(1)

    fmt = args.format
    out_path = args.output
    if out_path is not None:
        out_path = out_path.expanduser().resolve()
        suf = out_path.suffix.lower()
        if suf == ".md":
            fmt = "markdown"
        elif suf == ".html" or suf == ".htm":
            fmt = "html"
    else:
        voice_dir = repo_root / "voice-work"
        voice_dir.mkdir(parents=True, exist_ok=True)
        out_path = voice_dir / (
            "VoiceSpeech.md" if fmt == "markdown" else "VoiceSpeech.html"
        )

    game = load_game_root(agf_path)
    doc, dc, dsr, sds, ssy = gather_voice_document(game, agf_path, repo_root)
    if fmt == "html":
        text = format_html(doc, dc, dsr, sds, ssy)
    else:
        text = format_markdown(doc, dc, dsr, sds, ssy)

    out_path.write_text(text, encoding="utf-8")
    print(f"Wrote {out_path}", file=sys.stderr)
    print(
        f"Dialogs: {dc}, dialog script lines: {dsr}, DisplaySpeech rows: {sds}, player.Say rows: {ssy}",
        file=sys.stderr,
    )


if __name__ == "__main__":
    main()
