#!/usr/bin/env python3
"""
Scan AGS script files for GlobalInt flags, registry comments, and set/read sites;
write a searchable HTML or Markdown state catalog for designers.

Run from repo root:
  python scripts/generate_state_catalog.py
  python scripts/generate_state_catalog.py --format md
  python scripts/generate_state_catalog.py -o custom.html --json state.json
"""

from __future__ import annotations

import argparse
import html
import json
import re
import sys
from collections import defaultdict
from dataclasses import asdict, dataclass, field
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

PAT_DEFINE = re.compile(r"^\s*#define\s+(\w+)\s+(\d+)\s*(?://(.*))?\s*$")
PAT_SLOT_LINE = re.compile(r"^\s*//\s*(\d{3})(?:-(\d{3}))?\s*-\s*(.+?)\s*$")
PAT_GI_CALL = re.compile(
    r"\b(Get|Set)GlobalInt\s*\(\s*([^,)]+?)\s*(?:,\s*([^)]+?))?\s*\)"
)
PAT_SECTION = re.compile(r"^#sectionstart\s+(\w+)")
PAT_FUNCTION = re.compile(r"^function\s+(\w+)\s*\(")
PAT_ROOM_FILE = re.compile(r"^room(\d+)\.asc$", re.I)

DOOR_SLOT_MIN = 24
DOOR_SLOT_MAX = 33
ENGINE_SLOTS = frozenset({2, 4, 12})


@dataclass
class RefSite:
    file: str
    line: int
    op: str  # get | set
    function: str
    room: str
    value: str | None
    context: str
    raw_arg: str


@dataclass
class FlagEntry:
    slot: int
    symbol: str | None
    category: str
    comment: str
    friendly_title: str
    registry_note: str
    initial_value: str | None = None
    initial_source: str | None = None
    sets: list[RefSite] = field(default_factory=list)
    gets: list[RefSite] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)

    @property
    def set_count(self) -> int:
        return len(self.sets)

    @property
    def get_count(self) -> int:
        return len(self.gets)

    @property
    def rooms(self) -> list[str]:
        seen: set[str] = set()
        out: list[str] = []
        for site in self.sets + self.gets:
            if site.room.isdigit() and site.room not in seen:
                seen.add(site.room)
                out.append(site.room)
        return sorted(out, key=int)

    @property
    def anchor(self) -> str:
        if self.symbol:
            return f"flag-{self.symbol}"
        return f"flag-slot-{self.slot}"


def load_room_titles(agf_text: str) -> dict[str, str]:
    titles: dict[str, str] = {}
    for m in re.finditer(
        r"<UnloadedRoom>\s*<Description(?:[^>]*)>([^<]*)</Description>\s*<Number>(\d+)</Number>",
        agf_text,
    ):
        titles[m.group(2)] = (m.group(1) or "").strip()
    return titles


def parse_defines(ash_paths: list[Path]) -> dict[str, dict]:
    """name -> {slot, comment, prefixes}"""
    out: dict[str, dict] = {}
    for path in ash_paths:
        if not path.is_file():
            continue
        lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
        pending_comment = ""
        for line in lines:
            stripped = line.strip()
            if stripped.startswith("//") and not stripped.startswith("///"):
                body = stripped[2:].strip()
                if body and not body.startswith("#define"):
                    pending_comment = body
                continue
            m = PAT_DEFINE.match(line)
            if not m:
                pending_comment = ""
                continue
            name, slot_s, inline_comment = m.group(1), m.group(2), (m.group(3) or "").strip()
            slot = int(slot_s)
            comment = inline_comment or pending_comment
            pending_comment = ""
            prefix = "other"
            if name.startswith("GI_IRS_") or name.startswith("GI_"):
                prefix = "gi"
            elif name.startswith("INV_"):
                prefix = "inv"
            elif name.startswith("DIALOG_"):
                prefix = "dialog"
            elif name.startswith("ROOM"):
                prefix = "room"
            out[name] = {"slot": slot, "comment": comment, "prefix": prefix, "file": path.name}
    return out


def parse_slot_registry(gasc_path: Path) -> dict[int, str]:
    """slot -> description from GlobalScript.asc comment block."""
    slots: dict[int, str] = {}
    if not gasc_path.is_file():
        return slots
    in_block = False
    for line in gasc_path.read_text(encoding="utf-8", errors="replace").splitlines():
        if "RESERVED GLOBALINT SLOTS" in line:
            in_block = True
            continue
        if not in_block:
            continue
        if line.strip().startswith("////"):
            continue
        m = PAT_SLOT_LINE.match(line)
        if not m:
            if line.strip() and not line.strip().startswith("//"):
                break
            continue
        start = int(m.group(1))
        end = int(m.group(2)) if m.group(2) else start
        desc = m.group(3).strip()
        for slot in range(start, end + 1):
            slots[slot] = desc
    return slots


def room_from_file(filename: str) -> str:
    m = PAT_ROOM_FILE.match(filename)
    if m:
        return m.group(1)
    stem = Path(filename).stem
    if stem == "episode_irs_audit":
        return "episode"
    if stem == "GlobalScript":
        return "global"
    if stem == "core_doors":
        return "doors"
    if stem.startswith("core_"):
        return stem.replace("core_", "")
    return stem


def extract_function_body(text: str, func_name: str) -> str:
    pat = re.compile(rf"^function\s+{re.escape(func_name)}\s*\(\)\s*\{{", re.MULTILINE)
    m = pat.search(text)
    if not m:
        return ""
    start = m.end()
    depth = 1
    i = start
    while i < len(text) and depth > 0:
        ch = text[i]
        if ch == "{":
            depth += 1
        elif ch == "}":
            depth -= 1
        i += 1
    return text[start : i - 1]


def resolve_gi_arg(raw: str, defines: dict[str, dict]) -> tuple[int | None, str, str | None]:
    """Return (slot, resolved_label, warning)."""
    arg = raw.strip()
    if re.fullmatch(r"-?\d+", arg):
        return int(arg), arg, None
    if arg in defines:
        return defines[arg]["slot"], arg, None
    # Parameter variable (e.g. GI in core_doors) — not a concrete slot
    if re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", arg) and arg not in defines:
        return None, arg, "dynamic_arg"
    return None, arg, "unresolved"


def scan_gi_usage(asc_paths: list[Path], defines: dict[str, dict]) -> tuple[list[RefSite], list[dict]]:
    sites: list[RefSite] = []
    magic_warnings: list[dict] = []
    for path in sorted(asc_paths):
        if not path.is_file():
            continue
        lines = path.read_text(encoding="utf-8", errors="replace").splitlines()
        current_func = ""
        current_section = ""
        file_room = room_from_file(path.name)
        for line_no, line in enumerate(lines, start=1):
            stripped = line.lstrip()
            if stripped.startswith("//"):
                continue
            sm = PAT_SECTION.match(stripped)
            if sm:
                current_section = sm.group(1)
                continue
            fm = PAT_FUNCTION.match(stripped)
            if fm:
                current_func = fm.group(1)
                continue
            func_label = current_section or current_func or "(top)"
            for m in PAT_GI_CALL.finditer(line):
                op_raw, arg_raw, val_raw = m.group(1), m.group(2), m.group(3)
                op = "get" if op_raw == "Get" else "set"
                slot, label, warn = resolve_gi_arg(arg_raw, defines)
                value = val_raw.strip() if val_raw else None
                context = stripped.strip()
                site = RefSite(
                    file=path.name,
                    line=line_no,
                    op=op,
                    function=func_label,
                    room=file_room,
                    value=value,
                    context=context,
                    raw_arg=arg_raw.strip(),
                )
                if warn == "dynamic_arg":
                    continue
                if slot is None:
                    magic_warnings.append(
                        {
                            "file": path.name,
                            "line": line_no,
                            "raw_arg": arg_raw.strip(),
                            "context": context,
                        }
                    )
                    continue
                sites.append(site)
    return sites, magic_warnings


def parse_initial_values(
    gasc_path: Path,
    episode_path: Path,
    defines: dict[str, dict],
) -> dict[int, tuple[str, str]]:
    """slot -> (value, source_function)"""
    inits: dict[int, tuple[str, str]] = {}
    for path, func in ((gasc_path, "game_start"), (episode_path, "EpisodeIRS_GameStartInit")):
        if not path.is_file():
            continue
        body = extract_function_body(path.read_text(encoding="utf-8", errors="replace"), func)
        for line in body.splitlines():
            stripped = line.strip()
            if stripped.startswith("//"):
                continue
            for m in PAT_GI_CALL.finditer(line):
                if m.group(1) != "Set":
                    continue
                slot, _, warn = resolve_gi_arg(m.group(2), defines)
                if slot is None or warn:
                    continue
                val = (m.group(3) or "").strip()
                inits[slot] = (val, func)
    return inits


def infer_category(symbol: str | None, slot: int, registry_note: str) -> str:
    if symbol and symbol.startswith("GI_IRS_"):
        return "story"
    if DOOR_SLOT_MIN <= slot <= DOOR_SLOT_MAX:
        note_lower = registry_note.lower()
        if "door" in note_lower or "room" in note_lower and "<->" in registry_note:
            return "door"
    if slot in ENGINE_SLOTS:
        return "engine"
    note_lower = registry_note.lower()
    if "(free)" in note_lower or "legacy" in note_lower:
        return "legacy"
    if symbol and symbol.startswith("GI_"):
        return "story"
    return "other"


def friendly_title(symbol: str | None, slot: int, registry_note: str) -> str:
    if symbol:
        s = symbol
        for prefix in ("GI_IRS_", "GI_"):
            if s.startswith(prefix):
                s = s[len(prefix) :]
                break
        words = s.split("_")
        parts: list[str] = []
        for w in words:
            if not w:
                continue
            if w.isupper() and len(w) > 1:
                parts.append(w)
            else:
                parts.append(w.capitalize())
        if parts:
            return " ".join(parts)
    if registry_note:
        return registry_note.split(";")[0].strip()
    return f"GlobalInt slot {slot}"


def build_model(
    *,
    defines: dict[str, dict],
    slot_registry: dict[int, str],
    sites: list[RefSite],
    inits: dict[int, tuple[str, str]],
    magic_warnings: list[dict],
    asc_files: list[Path],
    room_titles: dict[str, str],
) -> dict:
    slot_to_symbol: dict[int, str] = {}
    for name, info in defines.items():
        if info["prefix"] != "gi" and not name.startswith("GI_"):
            continue
        slot = info["slot"]
        if slot not in slot_to_symbol:
            slot_to_symbol[slot] = name

    referenced_slots: set[int] = set()
    for site in sites:
        if re.fullmatch(r"-?\d+", site.raw_arg):
            referenced_slots.add(int(site.raw_arg))
        elif site.raw_arg in defines:
            referenced_slots.add(defines[site.raw_arg]["slot"])

    all_slots: set[int] = set(slot_registry.keys()) | set(slot_to_symbol.keys()) | referenced_slots

    flags: dict[int, FlagEntry] = {}
    for slot in sorted(all_slots):
        symbol = slot_to_symbol.get(slot)
        registry_note = slot_registry.get(slot, "")
        comment = ""
        if symbol and symbol in defines:
            comment = defines[symbol].get("comment", "")
        cat = infer_category(symbol, slot, registry_note)
        flags[slot] = FlagEntry(
            slot=slot,
            symbol=symbol,
            category=cat,
            comment=comment,
            friendly_title=friendly_title(symbol, slot, registry_note),
            registry_note=registry_note,
        )

    for site in sites:
        slot = None
        if re.fullmatch(r"-?\d+", site.raw_arg):
            slot = int(site.raw_arg)
        elif site.raw_arg in defines:
            slot = defines[site.raw_arg]["slot"]
        if slot is None:
            continue
        if slot not in flags:
            flags[slot] = FlagEntry(
                slot=slot,
                symbol=None,
                category="other",
                comment="",
                friendly_title=friendly_title(None, slot, ""),
                registry_note=slot_registry.get(slot, ""),
            )
        if site.op == "set":
            flags[slot].sets.append(site)
        else:
            flags[slot].gets.append(site)

    for slot, (val, src) in inits.items():
        if slot not in flags:
            flags[slot] = FlagEntry(
                slot=slot,
                symbol=slot_to_symbol.get(slot),
                category=infer_category(slot_to_symbol.get(slot), slot, slot_registry.get(slot, "")),
                comment=defines.get(slot_to_symbol.get(slot) or "", {}).get("comment", "")
                if slot_to_symbol.get(slot)
                else "",
                friendly_title=friendly_title(slot_to_symbol.get(slot), slot, slot_registry.get(slot, "")),
                registry_note=slot_registry.get(slot, ""),
            )
        flags[slot].initial_value = val
        flags[slot].initial_source = src

    gi_defines = {
        n: info for n, info in defines.items() if n.startswith("GI_IRS_") or n.startswith("GI_")
    }
    referenced_symbols: set[str] = set()
    for site in sites:
        if site.raw_arg in gi_defines:
            referenced_symbols.add(site.raw_arg)

    orphan_defines: list[str] = []
    for name in sorted(gi_defines):
        if name not in referenced_symbols:
            orphan_defines.append(name)

    catalog_warnings: list[dict] = []
    for name in orphan_defines:
        catalog_warnings.append({"kind": "orphan_define", "symbol": name, "slot": gi_defines[name]["slot"]})
    for mw in magic_warnings:
        catalog_warnings.append({"kind": "magic_number", **mw})

    for slot, entry in flags.items():
        non_init_sets = [s for s in entry.sets if s.function not in ("game_start", "EpisodeIRS_GameStartInit")]
        if entry.get_count == 0 and non_init_sets:
            entry.warnings.append("set_never_read")
        if entry.set_count == 0 and entry.get_count > 0 and entry.initial_value is None:
            entry.warnings.append("read_never_set")

    story = [f for f in flags.values() if f.category == "story"]
    doors = [f for f in flags.values() if f.category == "door"]
    engine = [f for f in flags.values() if f.category == "engine"]
    legacy = [f for f in flags.values() if f.category == "legacy"]
    other = [f for f in flags.values() if f.category == "other"]

    by_room: dict[str, list[FlagEntry]] = defaultdict(list)
    for entry in flags.values():
        for room in entry.rooms:
            by_room[room].append(entry)
    for room in by_room:
        by_room[room].sort(key=lambda e: (e.symbol or f"slot_{e.slot}", e.slot))

    reset_list = sorted(
        [f for f in flags.values() if f.initial_value is not None],
        key=lambda e: (e.symbol or f"slot_{e.slot}", e.slot),
    )

    return {
        "generated_at": datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M UTC"),
        "files_scanned": len(asc_files),
        "room_titles": room_titles,
        "summary": {
            "story_flags": len(story),
            "door_slots": len(doors),
            "engine_slots": len(engine),
            "legacy_slots": len(legacy),
            "warnings": len(catalog_warnings) + sum(len(f.warnings) for f in flags.values()),
        },
        "flags": {str(k): serialize_flag(v) for k, v in flags.items()},
        "story": [serialize_flag(f) for f in sorted(story, key=lambda e: e.symbol or str(e.slot))],
        "doors": [serialize_flag(f) for f in sorted(doors, key=lambda e: e.slot)],
        "engine": [serialize_flag(f) for f in sorted(engine, key=lambda e: e.slot)],
        "legacy": [serialize_flag(f) for f in sorted(legacy, key=lambda e: e.slot)],
        "other": [serialize_flag(f) for f in sorted(other, key=lambda e: e.slot)],
        "by_room": {
            room: [f.symbol if f.symbol else f"slot_{f.slot}" for f in entries]
            for room, entries in sorted(by_room.items(), key=lambda x: (not x[0].isdigit(), x[0]))
        },
        "reset_list": [serialize_flag(f) for f in reset_list],
        "catalog_warnings": catalog_warnings,
        "related_defines": {
            k: v for k, v in defines.items() if v["prefix"] in ("inv", "dialog", "room")
        },
    }


def serialize_flag(entry: FlagEntry) -> dict:
    d = asdict(entry)
    d["set_count"] = entry.set_count
    d["get_count"] = entry.get_count
    d["rooms"] = entry.rooms
    d["anchor"] = entry.anchor
    return d


def esc(s: str) -> str:
    return html.escape(s or "", quote=True)


def format_ref(site: dict, related: dict[str, dict]) -> str:
    loc = f"{site['file']}:{site['line']} {site['function']}"
    ctx = site["context"]
    extras: list[str] = []
    for sym in re.findall(r"\b(INV_\w+|DIALOG_\w+|ROOM\d+_\w+|GI_\w+)\b", ctx):
        if sym in related or sym.startswith("GI_"):
            extras.append(sym)
    extra = f" ({', '.join(extras)})" if extras else ""
    return f"{loc} — {ctx}{extra}"


def build_html(model: dict) -> str:
    room_titles = model["room_titles"]
    related = model.get("related_defines", {})

    def room_label(room: str) -> str:
        if room.isdigit():
            title = room_titles.get(room, "")
            return f"{room} — {title}" if title else room
        return room

    nav = """<nav class="nav">
  <a href="#summary">Summary</a>
  <a href="#story">Story flags</a>
  <a href="#doors">Doors</a>
  <a href="#by-room">By room</a>
  <a href="#reset">New game reset</a>
  <a href="#details">Flag details</a>
  <a href="#warnings">Warnings</a>
  <label class="search" for="catalog-search">Filter:</label>
  <input id="catalog-search" type="search" placeholder="Type to filter tables" autocomplete="off">
</nav>"""

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
.meta { font-size: 0.9rem; color: #444; margin-bottom: 0.5rem; }
section { margin-top: 2rem; scroll-margin-top: 5.25rem; }
h2 { font-size: 1.15rem; margin: 0 0 0.75rem; }
h3 { font-size: 1rem; margin: 1.25rem 0 0.5rem; }
.count { font-weight: normal; color: #555; font-size: 0.95rem; }
.note { font-size: 0.88rem; color: #555; margin: 0 0 0.75rem; }
table { width: 100%; border-collapse: collapse; background: #fff; box-shadow: 0 1px 2px rgba(0,0,0,.06); }
th, td { border: 1px solid #ddd; padding: 0.4rem 0.55rem; text-align: left; vertical-align: top; }
th { background: #e8eaef; font-weight: 600; }
tbody tr:nth-child(even) { background: #fafbfc; }
code { font-size: 0.9em; }
.detail-card { background: #fff; border: 1px solid #ddd; padding: 0.75rem 1rem; margin: 0.75rem 0; box-shadow: 0 1px 2px rgba(0,0,0,.06); }
.detail-card ul { margin: 0.35rem 0 0.5rem 1.1rem; padding: 0; }
.warn { color: #9a6700; }
pre.door-map { background: #fff; border: 1px solid #ddd; padding: 0.75rem; font-size: 0.85rem; overflow-x: auto; }
"""

    js = """
document.addEventListener("DOMContentLoaded", function () {
  const input = document.getElementById("catalog-search");
  if (!input) return;
  const tables = Array.from(document.querySelectorAll("section table"));
  function normalizeTokens(text) {
    return (text || "").toLowerCase().trim().split(/\\s+/).filter(Boolean);
  }
  function rowMatches(row, tokens) {
    if (tokens.length === 0) return true;
    const haystack = (row.textContent || "").toLowerCase();
    return tokens.every((token) => haystack.includes(token));
  }
  function applyFilter() {
    const tokens = normalizeTokens(input.value);
    for (const table of tables) {
      for (const row of Array.from(table.querySelectorAll("tbody tr"))) {
        row.style.display = rowMatches(row, tokens) ? "" : "none";
      }
    }
  }
  input.addEventListener("input", applyFilter);
});
"""

    s = model["summary"]
    summary_section = f"""<section id="summary">
<h2>Summary</h2>
<ul>
  <li>Generated: {esc(model["generated_at"])}</li>
  <li>Script files scanned: {model["files_scanned"]}</li>
  <li>Story flags: {s["story_flags"]}</li>
  <li>Door slots: {s["door_slots"]}</li>
  <li>Engine slots: {s["engine_slots"]}</li>
  <li>Warnings: {s["warnings"]}</li>
</ul>
<p class="note">Door convention: 0 = closed, 1 = open, 2 = locked (see <code>core_doors</code>).</p>
</section>"""

    def flag_table_rows(flags: list[dict]) -> str:
        rows = []
        for f in flags:
            sym = f.get("symbol") or "—"
            anchor = f.get("anchor", "")
            title = f.get("friendly_title", "")
            link = f'<a href="#{esc(anchor)}">{esc(title)}</a>' if anchor else esc(title)
            comment = f.get("comment") or f.get("registry_note") or ""
            rooms = ", ".join(f.get("rooms") or [])
            init = f.get("initial_value")
            init_s = init if init is not None else "—"
            rows.append(
                f"<tr><td>{link}</td><td><code>{esc(sym)}</code></td><td>{f['slot']}</td>"
                f"<td>{esc(comment)}</td><td>{esc(init_s)}</td>"
                f"<td>{f.get('set_count', 0)}</td><td>{f.get('get_count', 0)}</td>"
                f"<td>{esc(rooms)}</td></tr>"
            )
        return "".join(rows) or '<tr><td colspan="8">None</td></tr>'

    story_section = f"""<section id="story">
<h2>Story flags <span class="count">({len(model["story"])})</span></h2>
<table>
<thead><tr><th>Title</th><th>Symbol</th><th>Slot</th><th>Description</th><th>Initial</th><th>Sets</th><th>Gets</th><th>Rooms</th></tr></thead>
<tbody>{flag_table_rows(model["story"])}</tbody>
</table>
</section>"""

    door_lines = []
    for f in model["doors"]:
        note = f.get("registry_note") or f.get("comment") or ""
        door_lines.append(f"  {f['slot']:3d}  {note}")
    door_map = "\n".join(door_lines) if door_lines else "  (none documented)"

    doors_section = f"""<section id="doors">
<h2>Door slots <span class="count">({len(model["doors"])})</span></h2>
<pre class="door-map">{esc(door_map)}</pre>
<table>
<thead><tr><th>Slot</th><th>Description</th><th>Initial</th><th>Sets</th><th>Gets</th></tr></thead>
<tbody>"""
    for f in model["doors"]:
        desc = f.get("registry_note") or f.get("comment") or ""
        init = f.get("initial_value") or "—"
        anchor = f.get("anchor", "")
        slot_cell = f'<a href="#{esc(anchor)}">{f["slot"]}</a>' if anchor else str(f["slot"])
        doors_section += (
            f"<tr><td>{slot_cell}</td><td>{esc(desc)}</td><td>{esc(init)}</td>"
            f"<td>{f.get('set_count', 0)}</td><td>{f.get('get_count', 0)}</td></tr>"
        )
    if not model["doors"]:
        doors_section += '<tr><td colspan="5">None</td></tr>'
    doors_section += "</tbody></table></section>"

    by_room_parts = ['<section id="by-room"><h2>By room</h2>']
    by_room_data = model.get("by_room", {})
    room_keys = sorted(
        by_room_data.keys(),
        key=lambda r: (0, int(r)) if r.isdigit() else (1, r),
    )
    for room in room_keys:
        symbols = by_room_data[room]
        if not symbols:
            continue
        by_room_parts.append(f"<h3>{esc(room_label(room))}</h3><ul>")
        flags_by_sym = {f.get("symbol") or f"slot_{f['slot']}": f for f in model["flags"].values()}
        for sym in symbols:
            f = flags_by_sym.get(sym)
            if f:
                by_room_parts.append(
                    f'<li><a href="#{esc(f["anchor"])}">{esc(f.get("friendly_title", sym))}</a> '
                    f'(<code>{esc(f.get("symbol") or sym)}</code>)</li>'
                )
            else:
                by_room_parts.append(f"<li><code>{esc(sym)}</code></li>")
        by_room_parts.append("</ul>")
    by_room_parts.append("</section>")
    by_room_section = "\n".join(by_room_parts)

    reset_rows = []
    for f in model["reset_list"]:
        sym = f.get("symbol") or "—"
        reset_rows.append(
            f"<tr><td>{esc(f.get('friendly_title', ''))}</td><td><code>{esc(sym)}</code></td>"
            f"<td>{f['slot']}</td><td>{esc(f.get('initial_value') or '')}</td>"
            f"<td>{esc(f.get('initial_source') or '')}</td></tr>"
        )
    reset_section = f"""<section id="reset">
<h2>New game reset checklist <span class="count">({len(model["reset_list"])})</span></h2>
<p class="note">Values set in <code>game_start</code> or <code>EpisodeIRS_GameStartInit</code>.</p>
<table>
<thead><tr><th>Title</th><th>Symbol</th><th>Slot</th><th>Initial</th><th>Source</th></tr></thead>
<tbody>{"".join(reset_rows) or '<tr><td colspan="5">None</td></tr>'}</tbody>
</table>
</section>"""

    detail_parts = ['<section id="details"><h2>Flag details</h2>']
    all_detail_flags = model["story"] + model["doors"] + model["engine"] + model["other"]
    all_detail_flags.sort(key=lambda f: (f.get("symbol") or "", f["slot"]))
    seen_slots: set[int] = set()
    for f in all_detail_flags:
        if f["slot"] in seen_slots:
            continue
        seen_slots.add(f["slot"])
        if f.get("set_count", 0) + f.get("get_count", 0) == 0 and not f.get("initial_value"):
            continue
        sym = f.get("symbol")
        desc = f.get("comment") or f.get("registry_note") or ""
        init = f.get("initial_value")
        init_line = f"<p>Initial on new game: <code>{esc(init)}</code> ({esc(f.get('initial_source') or '')})</p>" if init is not None else ""
        warn_html = ""
        if f.get("warnings"):
            warn_html = '<p class="warn">Warnings: ' + ", ".join(esc(w) for w in f["warnings"]) + "</p>"
        sets_html = "<ul>" + "".join(f"<li><code>{esc(format_ref(s, related))}</code></li>" for s in f.get("sets", [])) + "</ul>"
        gets_html = "<ul>" + "".join(f"<li><code>{esc(format_ref(s, related))}</code></li>" for s in f.get("gets", [])) + "</ul>"
        if not f.get("sets"):
            sets_html = "<p class=\"note\">No set sites (excluding none).</p>"
        if not f.get("gets"):
            gets_html = "<p class=\"note\">No read sites.</p>"
        sym_label = sym if sym else f"slot {f['slot']}"
        detail_parts.append(
            f'<div class="detail-card" id="{esc(f["anchor"])}">'
            f"<h3>{esc(f.get('friendly_title', ''))}</h3>"
            f"<p><code>{esc(sym_label)}</code> · slot {f['slot']}</p>"
            f"<p>{esc(desc)}</p>{init_line}{warn_html}"
            f"<p><strong>Set in</strong></p>{sets_html}"
            f"<p><strong>Read in</strong></p>{gets_html}"
            f"</div>"
        )
    detail_parts.append("</section>")
    details_section = "\n".join(detail_parts)

    warn_rows = []
    for w in model.get("catalog_warnings", []):
        kind = w.get("kind", "")
        if kind == "orphan_define":
            warn_rows.append(
                f"<tr><td>orphan_define</td><td><code>{esc(w.get('symbol', ''))}</code></td>"
                f"<td>{w.get('slot', '')}</td><td>Defined in .ash but never referenced</td></tr>"
            )
        elif kind == "magic_number":
            warn_rows.append(
                f"<tr><td>magic_number</td><td><code>{esc(w.get('raw_arg', ''))}</code></td>"
                f"<td>{w.get('line', '')}</td><td>{esc(w.get('file', ''))}: {esc(w.get('context', ''))}</td></tr>"
            )
    for f in model["flags"].values():
        for w in f.get("warnings", []):
            sym = f.get("symbol") or f"slot_{f['slot']}"
            warn_rows.append(
                f"<tr><td>{esc(w)}</td><td><code>{esc(sym)}</code></td>"
                f"<td>{f['slot']}</td><td>Heuristic QA hint</td></tr>"
            )
    warnings_section = f"""<section id="warnings">
<h2>Warnings <span class="count">({len(warn_rows)})</span></h2>
<table>
<thead><tr><th>Kind</th><th>Symbol / arg</th><th>Slot / line</th><th>Detail</th></tr></thead>
<tbody>{"".join(warn_rows) or '<tr><td colspan="4">None</td></tr>'}</tbody>
</table>
</section>"""

    body = f"""<div class="wrap">
{nav}
<h1>Game state catalog</h1>
<p class="meta">GlobalInt flags and cross-references for AGS scripts</p>
{summary_section}
{story_section}
{doors_section}
{by_room_section}
{reset_section}
{details_section}
{warnings_section}
</div>"""

    return f"""<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Game state catalog</title>
<style>{css}</style>
<script>{js}</script>
</head>
<body>
{body}
</body>
</html>
"""


def build_markdown(model: dict) -> str:
    related = model.get("related_defines", {})
    room_titles = model["room_titles"]
    lines: list[str] = [
        "# Game state catalog",
        "",
        f"Generated: {model['generated_at']}",
        "",
        "## Summary",
        "",
        f"- Script files scanned: {model['files_scanned']}",
        f"- Story flags: {model['summary']['story_flags']}",
        f"- Door slots: {model['summary']['door_slots']}",
        f"- Warnings: {model['summary']['warnings']}",
        "",
        "Door convention: 0 = closed, 1 = open, 2 = locked.",
        "",
        f"## Story flags ({len(model['story'])})",
        "",
        "| Title | Symbol | Slot | Description | Initial | Sets | Gets | Rooms |",
        "| --- | --- | --- | --- | --- | --- | --- | --- |",
    ]
    for f in model["story"]:
        sym = f.get("symbol") or "—"
        desc = f.get("comment") or f.get("registry_note") or ""
        rooms = ", ".join(f.get("rooms") or [])
        init = f.get("initial_value") or "—"
        lines.append(
            f"| {f.get('friendly_title', '')} | `{sym}` | {f['slot']} | {desc} | {init} | "
            f"{f.get('set_count', 0)} | {f.get('get_count', 0)} | {rooms} |"
        )

    lines.extend(["", f"## Door slots ({len(model['doors'])})", ""])
    for f in model["doors"]:
        desc = f.get("registry_note") or f.get("comment") or ""
        lines.append(f"- **{f['slot']}** — {desc}")
    lines.append("")

    lines.extend(["## By room", ""])
    for room, symbols in sorted(
        model.get("by_room", {}).items(),
        key=lambda x: (0, int(x[0])) if x[0].isdigit() else (1, x[0]),
    ):
        title = room_titles.get(room, "") if room.isdigit() else ""
        heading = f"{room} — {title}" if title else room
        lines.append(f"### {heading}")
        for sym in symbols:
            lines.append(f"- `{sym}`")
        lines.append("")

    lines.extend(["## New game reset checklist", ""])
    for f in model["reset_list"]:
        sym = f.get("symbol") or "—"
        lines.append(
            f"- **{f.get('friendly_title', '')}** (`{sym}`, slot {f['slot']}) "
            f"= `{f.get('initial_value', '')}` via `{f.get('initial_source', '')}`"
        )
    lines.append("")

    lines.extend(["## Flag details", ""])
    flags_by_slot = {int(k): v for k, v in model["flags"].items()}
    for f in sorted(flags_by_slot.values(), key=lambda x: (x.get("symbol") or "", x["slot"])):
        if f.get("set_count", 0) + f.get("get_count", 0) == 0 and not f.get("initial_value"):
            continue
        sym = f.get("symbol") or f"slot {f['slot']}"
        lines.append(f"### {f.get('friendly_title', sym)}")
        lines.append("")
        lines.append(f"- Symbol: `{sym}` · slot {f['slot']}")
        desc = f.get("comment") or f.get("registry_note") or ""
        if desc:
            lines.append(f"- {desc}")
        if f.get("initial_value") is not None:
            lines.append(
                f"- Initial: `{f['initial_value']}` ({f.get('initial_source', '')})"
            )
        if f.get("warnings"):
            lines.append(f"- Warnings: {', '.join(f['warnings'])}")
        lines.append("")
        lines.append("**Set in**")
        for s in f.get("sets", []):
            lines.append(f"- `{format_ref(s, related)}`")
        if not f.get("sets"):
            lines.append("- (none)")
        lines.append("")
        lines.append("**Read in**")
        for s in f.get("gets", []):
            lines.append(f"- `{format_ref(s, related)}`")
        if not f.get("gets"):
            lines.append("- (none)")
        lines.append("")

    lines.extend(["## Warnings", ""])
    for w in model.get("catalog_warnings", []):
        if w.get("kind") == "orphan_define":
            lines.append(f"- orphan_define: `{w.get('symbol')}` (slot {w.get('slot')})")
        elif w.get("kind") == "magic_number":
            lines.append(
                f"- magic_number: `{w.get('raw_arg')}` at {w.get('file')}:{w.get('line')}"
            )
    for f in model["flags"].values():
        for w in f.get("warnings", []):
            sym = f.get("symbol") or f"slot_{f['slot']}"
            lines.append(f"- {w}: `{sym}` (slot {f['slot']})")

    return "\n".join(lines) + "\n"


def collect_asc_files(root: Path) -> list[Path]:
    return sorted(root.glob("*.asc"))


def collect_ash_files(root: Path) -> list[Path]:
    return sorted(root.glob("*.ash"))


def main() -> None:
    p = argparse.ArgumentParser(description="Generate AGS GlobalInt state catalog (HTML or Markdown)")
    p.add_argument("--format", choices=("html", "md"), default="html", help="Output format (default: html)")
    p.add_argument("-o", "--output", type=Path, default=None, help="Output file path")
    p.add_argument("--agf", type=Path, default=ROOT / "Game.agf", help="Path to Game.agf")
    p.add_argument("--json", type=Path, default=None, help="Optional path to dump JSON model")
    args = p.parse_args()

    agf_path = args.agf.expanduser().resolve()
    if not agf_path.is_file():
        print(f"Not a file: {agf_path}", file=sys.stderr)
        raise SystemExit(1)

    room_titles = load_room_titles(agf_path.read_text(encoding="utf-8", errors="replace"))
    ash_files = collect_ash_files(ROOT)
    asc_files = collect_asc_files(ROOT)
    defines = parse_defines(ash_files)
    slot_registry = parse_slot_registry(ROOT / "GlobalScript.asc")
    sites, magic_warnings = scan_gi_usage(asc_files, defines)
    inits = parse_initial_values(
        ROOT / "GlobalScript.asc",
        ROOT / "episode_irs_audit.asc",
        defines,
    )

    model = build_model(
        defines=defines,
        slot_registry=slot_registry,
        sites=sites,
        inits=inits,
        magic_warnings=magic_warnings,
        asc_files=asc_files,
        room_titles=room_titles,
    )

    if args.output is not None:
        out_path = args.output.expanduser().resolve()
    elif args.format == "md":
        out_path = ROOT / "Game_state_catalog.md"
    else:
        out_path = ROOT / "Game_state_catalog.html"

    if args.format == "md":
        content = build_markdown(model)
    else:
        content = build_html(model)

    out_path.write_text(content, encoding="utf-8")
    print(f"Wrote {out_path}")

    if args.json is not None:
        json_path = args.json.expanduser().resolve()
        json_path.write_text(json.dumps(model, indent=2), encoding="utf-8")
        print(f"Wrote {json_path}")


if __name__ == "__main__":
    main()
