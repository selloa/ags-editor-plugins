#!/usr/bin/env python3
"""Build static HTML pages from hub/content/*.md into docs/."""

from __future__ import annotations

import re
import shutil
import sys
import unicodedata
from datetime import date
from pathlib import Path

import markdown
from markdown.extensions.toc import TocExtension

ROOT = Path(__file__).resolve().parent
REPO_ROOT = ROOT.parent
STYLESHEET = ROOT / "assets" / "site.css"
DOCS_ROOT = REPO_ROOT / "docs"
DOCS_ASSETS = DOCS_ROOT / "assets"

# --- Site configuration (edit when forking this repo) ---
SITE = {
    "site_id": "ags-editor-plugins",
    "site_name": "AGS Editor Plugin Collection",
    "default_description": (
        "Forkbare AGS 3.6 Editor-Plugins — Sample, Viewer und Experimente."
    ),
    "author_name": "selloa",
    "author_url": "https://selloa.github.io/ags-editor-plugins/",
    "translate_languages": "de,en",
    "repo_url": "https://github.com/selloa/ags-editor-plugins",
    "default_branch": "master",
}

PAGES: list[tuple[str, str, str]] = [
    ("content/index.md", "../docs/index.html", "home"),
]

NAV_ITEMS: list[tuple[str, str, str]] = []

INTERNAL_SECTION = re.compile(r"^## Page build notes\b", re.MULTILINE)
HTML_COMMENT = re.compile(r"<!--.*?-->", re.DOTALL)
FRONT_MATTER = re.compile(r"^---\r?\n(.*?)\r?\n---\r?\n", re.DOTALL)
PRE_BLOCK = re.compile(r"<pre>(.*?)</pre>", re.DOTALL)
HEADING = re.compile(r'<h([1-4]) id="([^"]+)">(.*?)</h\1>', re.DOTALL)
ANCHOR_TAG = re.compile(r'<a href="([^"]*)"([^>]*)>', re.IGNORECASE)
VIDEO_HOST = re.compile(
    r"https?://(?:www\.)?(?:youtube\.com|youtu\.be|vimeo\.com|twitch\.tv|dailymotion\.com)",
    re.IGNORECASE,
)
URL_LINE = re.compile(r"^https?://\S+$")
TITLE_SKIP = re.compile(r"^(#|[-*]|\[|<|http|\*\*)")
DATE_WILDCARDS = frozenset({"*", "auto", "today"})


def slugify(value: str, separator: str) -> str:
    value = unicodedata.normalize("NFKD", value).encode("ascii", "ignore").decode("ascii")
    value = re.sub(r"[^\w\s-]", "", value).strip().lower()
    return re.sub(r"[-\s]+", separator, value)


def path_prefix(output_path: Path) -> str:
    rel = output_path.relative_to(DOCS_ROOT)
    depth = len(rel.parent.parts)
    return "../" * depth if depth else ""


def parse_front_matter(text: str) -> tuple[dict[str, str], str]:
    match = FRONT_MATTER.match(text)
    if not match:
        return {}, text

    meta: dict[str, str] = {}
    for line in match.group(1).splitlines():
        if ":" in line:
            key, value = line.split(":", 1)
            meta[key.strip()] = value.strip()
    return meta, text[match.end() :]


def resolve_build_date(meta: dict[str, str]) -> str:
    raw = meta.get("date", "").strip()
    if not raw or raw.lower() in DATE_WILDCARDS:
        return date.today().isoformat()
    return raw


def strip_internal_sections(text: str) -> str:
    match = INTERNAL_SECTION.search(text)
    if match:
        text = text[: match.start()].rstrip()
    return HTML_COMMENT.sub("", text).strip() + "\n"


def preprocess_bare_urls(text: str) -> str:
    lines = text.splitlines()
    result: list[str] = []
    i = 0
    while i < len(lines):
        if i + 1 < len(lines):
            title_line = lines[i].strip()
            url_line = lines[i + 1].strip()
            if (
                title_line
                and URL_LINE.match(url_line)
                and not TITLE_SKIP.match(title_line)
                and "[" not in title_line
            ):
                result.append(f"[{title_line}]({url_line})")
                i += 2
                while i < len(lines) and URL_LINE.match(lines[i].strip()):
                    result.append("")
                    result.append(f"[{title_line}]({lines[i].strip()})")
                    i += 1
                continue
        result.append(lines[i])
        i += 1
    return "\n".join(result) + "\n"


def tag_video_links(html: str) -> str:
    def add_video_class(match: re.Match[str]) -> str:
        href = match.group(1)
        rest = match.group(2)
        if not VIDEO_HOST.search(href):
            return match.group(0)
        if 'class="' in rest:
            return re.sub(
                r'class="([^"]*)"',
                r'class="\1 link-video"',
                match.group(0),
                count=1,
            )
        return f'<a href="{href}" class="link-video"{rest}>'

    return ANCHOR_TAG.sub(add_video_class, html)


def open_external_links_in_new_tab(html: str) -> str:
    def add_new_tab(match: re.Match[str]) -> str:
        href = match.group(1)
        rest = match.group(2)
        if not href.startswith(("http://", "https://")):
            return match.group(0)
        if 'target="' in rest or "target='" in rest:
            return match.group(0)
        return f'<a href="{href}" target="_blank" rel="noopener noreferrer"{rest}>'

    return ANCHOR_TAG.sub(add_new_tab, html)


def postprocess_body(html: str) -> str:
    html = PRE_BLOCK.sub(
        r'<div class="markview-code-block-wrapper"><pre>\1</pre></div>',
        html,
    )
    html = html.replace("<img ", '<img loading="lazy" ')
    html = re.sub(
        r'<input type="checkbox"\s+disabled\s*/?>',
        '<input type="checkbox">',
        html,
    )
    html = html.replace(
        "<owner>",
        '<span class="tag-badge tag-owner">owner</span>',
    )
    html = html.replace(
        "<fork>",
        '<span class="tag-badge tag-fork">fork</span>',
    )
    html = tag_video_links(html)
    html = open_external_links_in_new_tab(html)
    return html


def extract_sidebar_nav(body_html: str) -> str:
    items = []
    for level, slug, raw_title in HEADING.findall(body_html):
        title = re.sub(r"<[^>]+>", "", raw_title).strip()
        css_class = f"sidebar-h{level}"
        items.append(f'        <li class="{css_class}"><a href="#{slug}">{title}</a></li>')
    if not items:
        return ""
    links = "\n".join(items)
    return f"""  <aside class="site-sidebar" id="site-sidebar" aria-label="Page sections">
    <div class="site-sidebar-header">
      <p class="site-sidebar-title">On this page</p>
      <button type="button" class="site-sidebar-toggle" id="sidebar-hide" aria-controls="site-sidebar" aria-expanded="true" title="Hide navigation">Hide</button>
    </div>
    <nav>
      <ul>
{links}
      </ul>
    </nav>
  </aside>"""


def build_topnav(prefix: str, current: str) -> str:
    if not NAV_ITEMS:
        return ""
    links = []
    for page_id, label, href in NAV_ITEMS:
        current_attr = ' aria-current="page"' if page_id == current else ""
        links.append(
            f'      <li><a href="{prefix}{href}"{current_attr}>{label}</a></li>'
        )
    joined = "\n".join(links)
    return f"""    <nav class="site-topnav" aria-label="Site">
      <ul>
{joined}
      </ul>
    </nav>"""


def render_markdown(content: str) -> str:
    return markdown.markdown(
        content,
        extensions=[
            TocExtension(slugify=slugify, separator="-", toc_depth=6),
            "markdown.extensions.tables",
            "markdown.extensions.fenced_code",
            "markdown.extensions.sane_lists",
            "markdown.extensions.nl2br",
            "pymdownx.tasklist",
        ],
        extension_configs={
            "pymdownx.tasklist": {
                "custom_checkbox": False,
            },
        },
        output_format="html5",
    )


def build_html(
    meta: dict[str, str],
    body_html: str,
    sidebar_nav: str,
    *,
    source_name: str,
    output_file: Path,
    page_id: str,
) -> str:
    title = meta.get("title", SITE["site_name"])
    version = meta.get("version", "0.0.0")
    build_date = meta.get("date", "")
    status = meta.get("status", "draft")
    description = meta.get("description", SITE["default_description"])
    author_name = SITE["author_name"]
    author_url = SITE["author_url"]
    translate_languages = SITE["translate_languages"]

    prefix = path_prefix(output_file)
    topnav = build_topnav(prefix, page_id)
    sidebar_key = f"{SITE['site_id']}-sidebar-hidden-{page_id}"

    indented = "\n".join(
        f"    {line}" if line.strip() else line for line in body_html.splitlines()
    )

    return f"""<!DOCTYPE html>
<html lang="de">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <meta name="color-scheme" content="dark">
  <title>{title}</title>
  <meta name="description" content="{description}">
  <link rel="icon" type="image/svg+xml" href="{prefix}assets/favicon.svg">
  <link rel="stylesheet" href="{prefix}assets/site.css">
</head>
<body class="markdown-export" data-theme="dark">
  <header class="site-header">
    <div class="wrap">
      <div id="google_translate_element" class="site-translate"></div>
      <h1>{title}</h1>
      <p class="meta">Version {version} · {build_date} · {status} · by <a href="{author_url}" target="_blank" rel="noopener noreferrer">{author_name}</a></p>
{topnav}
    </div>
  </header>
  <div class="site-layout">
    <button type="button" class="site-sidebar-show" id="sidebar-show" hidden aria-controls="site-sidebar" title="Show navigation">Show nav</button>
{sidebar_nav}
    <main id="markview-container" class="markdown-body code-block-scroll">
{indented}
    </main>
  </div>
  <footer class="site-footer">
    <p>Built from <code>{source_name}</code> · v{version} · {build_date} · <a href="{author_url}" target="_blank" rel="noopener noreferrer">{author_name}</a></p>
  </footer>
  <a href="#" class="back-top" id="backTop" aria-label="Back to top">↑ Top</a>
  <script>
  (function () {{
    var hideBtn = document.getElementById('sidebar-hide');
    var showBtn = document.getElementById('sidebar-show');
    var storageKey = '{sidebar_key}';

    function setSidebarHidden(hidden) {{
      document.body.classList.toggle('sidebar-hidden', hidden);
      if (hideBtn) hideBtn.setAttribute('aria-expanded', hidden ? 'false' : 'true');
      if (showBtn) showBtn.hidden = !hidden;
      try {{ localStorage.setItem(storageKey, hidden ? '1' : '0'); }} catch (e) {{}}
    }}

    try {{
      if (localStorage.getItem(storageKey) === '1') setSidebarHidden(true);
    }} catch (e) {{}}

    if (hideBtn) hideBtn.addEventListener('click', function () {{ setSidebarHidden(true); }});
    if (showBtn) showBtn.addEventListener('click', function () {{ setSidebarHidden(false); }});

    var backTop = document.getElementById('backTop');
    if (backTop) {{
      window.addEventListener('scroll', function () {{
        backTop.classList.toggle('visible', window.scrollY > 400);
      }});
      backTop.addEventListener('click', function (e) {{
        e.preventDefault();
        window.scrollTo({{ top: 0, behavior: 'smooth' }});
      }});
    }}
  }})();
  </script>
  <script>
  function googleTranslateElementInit() {{
    new google.translate.TranslateElement(
      {{ pageLanguage: 'de', includedLanguages: '{translate_languages}', layout: google.translate.TranslateElement.InlineLayout.HORIZONTAL }},
      'google_translate_element'
    );
  }}
  </script>
  <script src="https://translate.google.com/translate_a/element.js?cb=googleTranslateElementInit"></script>
</body>
</html>
"""


def build_page(source_rel: str, output_rel: str, page_id: str) -> bool:
    source = ROOT / source_rel
    output = (ROOT / output_rel).resolve()

    if not source.exists():
        print(f"Error: source file not found: {source}", file=sys.stderr)
        return False

    raw = source.read_text(encoding="utf-8")
    meta, content = parse_front_matter(raw)
    meta = {**meta, "date": resolve_build_date(meta)}
    content = strip_internal_sections(content)
    content = preprocess_bare_urls(content)

    body_html = postprocess_body(render_markdown(content))
    sidebar_nav = extract_sidebar_nav(body_html)
    html = build_html(
        meta,
        body_html,
        sidebar_nav,
        source_name=source_rel,
        output_file=output,
        page_id=page_id,
    )

    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(html, encoding="utf-8", newline="\n")
    print(f"Wrote {output.relative_to(REPO_ROOT)} (from {source.name}, v{meta.get('version', '?')})")
    return True


def copy_assets() -> bool:
    if not STYLESHEET.exists():
        print(f"Error: stylesheet not found: {STYLESHEET}", file=sys.stderr)
        return False

    DOCS_ASSETS.mkdir(parents=True, exist_ok=True)
    for name in ("site.css", "favicon.svg"):
        src = ROOT / "assets" / name
        if not src.is_file():
            print(f"Error: asset not found: {src}", file=sys.stderr)
            return False
        shutil.copy2(src, DOCS_ASSETS / name)
        print(f"Wrote {DOCS_ASSETS.relative_to(REPO_ROOT) / name}")
    return True


def main() -> int:
    from discover_plugins import write_index_md

    plugins = write_index_md(
        SITE.get("repo_url", ""),
        SITE.get("default_branch", "main"),
    )
    print(f"Discovered {len(plugins)} plugin(s)")

    if not copy_assets():
        return 1

    ok = True
    for source_rel, output_rel, page_id in PAGES:
        if not build_page(source_rel, output_rel, page_id):
            ok = False

    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
