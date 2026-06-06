using System.Collections.Generic;
using System.Net;
using System.Text;

namespace AGS.Plugin.SpeechViewer
{
    internal static class SpeechHtmlExporter
    {
        public static string FormatHtml(SpeechDocument document)
        {
            var dialogCount = document.DialogCount;
            var dialogSpeechRows = document.DialogSpeechRows;
            var scriptDs = document.ScriptDisplaySpeechRows;
            var scriptSay = document.ScriptSayRows;

            var parts = new StringBuilder();
            parts.AppendLine("<!DOCTYPE html>");
            parts.AppendLine("<html lang=\"en\">");
            parts.AppendLine("<head>");
            parts.AppendLine("<meta charset=\"utf-8\">");
            parts.AppendLine("<title>Voice script — " + Esc(document.AgfName) + "</title>");
            parts.AppendLine("<style>");
            parts.AppendLine(GetCss());
            parts.AppendLine("</style>");
            parts.AppendLine("</head>");
            parts.AppendLine("<body>");
            parts.AppendLine("<div class=\"layout\">");
            AppendToc(parts, document);
            AppendMain(parts, document, dialogCount, dialogSpeechRows, scriptDs, scriptSay);
            parts.AppendLine("</div>");
            parts.AppendLine("</body></html>");
            return parts.ToString();
        }

        private static void AppendToc(StringBuilder parts, SpeechDocument document)
        {
            parts.AppendLine("<nav class=\"toc\" aria-label=\"Table of contents\">");
            parts.AppendLine("<h2>Jump</h2>");
            parts.AppendLine("<ul><li><a href=\"#top\">Overview</a></li>");
            parts.AppendLine("<li><a href=\"#part-dialogs\">Part 1 — Dialog trees</a><ul>");

            if (!document.DialogsMissing && document.Dialogs != null)
            {
                foreach (var dialog in document.Dialogs)
                {
                    parts.AppendLine(
                        "<li><a href=\"#" + EscAttr(dialog.Anchor) + "\">dlg-" + dialog.Id + " — " + Esc(dialog.Name) + "</a>");
                    if (dialog.Branches != null && dialog.Branches.Count > 0)
                    {
                        parts.AppendLine("<ul>");
                        foreach (var branch in dialog.Branches)
                        {
                            parts.AppendLine(
                                "<li><a href=\"#" + EscAttr(branch.Anchor) + "\">" + Esc(branch.Title) + "</a></li>");
                        }
                        parts.AppendLine("</ul>");
                    }
                    parts.AppendLine("</li>");
                }
            }

            parts.AppendLine("</ul></li>");
            parts.AppendLine("<li><a href=\"#part-asc\">Part 2 — Scripted speech</a>");
            if (document.AscSections != null && document.AscSections.Count > 0)
            {
                parts.AppendLine("<ul>");
                foreach (var section in document.AscSections)
                {
                    parts.AppendLine(
                        "<li class=\"file-link\"><a href=\"#" + EscAttr(section.Anchor) + "\">" + Esc(section.Filename) + "</a></li>");
                }
                parts.AppendLine("</ul>");
            }
            parts.AppendLine("</li></ul>");
            parts.AppendLine("</nav>");
        }

        private static void AppendMain(
            StringBuilder parts,
            SpeechDocument document,
            int dialogCount,
            int dialogSpeechRows,
            int scriptDs,
            int scriptSay)
        {
            parts.AppendLine("<main>");
            parts.AppendLine("<h1 id=\"top\">Voice script export</h1>");
            parts.AppendLine(
                "<p class=\"intro\">Default language (no <code>.trs</code>). Sources: <code>" + Esc(document.AgfName) +
                "</code> and <code>*.asc</code> in <code>" + Esc(document.RepoName) + "/</code>. " +
                "Variables / non-literal speech are omitted unless noted.</p>");
            parts.AppendLine(
                "<p class=\"stats\">Dialogs: " + dialogCount + " · Dialog script lines: " + dialogSpeechRows +
                " · DisplaySpeech rows: " + scriptDs + " · player.Say rows: " + scriptSay + "</p>");

            parts.AppendLine("<section class=\"block\" id=\"part-dialogs\">");
            parts.AppendLine("<h2>Part 1 — Dialog trees (Game.agf)</h2>");

            if (document.DialogsMissing)
            {
                parts.AppendLine("<p>(No dialogs section in Game.agf.)</p>");
            }
            else if (document.Dialogs != null)
            {
                foreach (var dialog in document.Dialogs)
                {
                    parts.AppendLine("<article class=\"dlg\" id=\"" + EscAttr(dialog.Anchor) + "\">");
                    parts.AppendLine("<h3><code>dlg-" + dialog.Id + "</code> — " + Esc(dialog.Name) + "</h3>");

                    if (dialog.Options != null && dialog.Options.Count > 0)
                    {
                        parts.AppendLine("<p><strong>Player options</strong></p>");
                        parts.AppendLine("<table><thead><tr><th>ID</th><th>Text</th></tr></thead><tbody>");
                        foreach (var option in dialog.Options)
                        {
                            parts.AppendLine(
                                "<tr><td class=\"id\"><code>" + Esc(option.LineId) + "</code></td><td class=\"msg\">" +
                                Esc(option.Text) + "</td></tr>");
                        }
                        parts.AppendLine("</tbody></table>");
                    }

                    if (!dialog.HasSpeech)
                    {
                        parts.AppendLine("<p><em>No SPEAKER: lines in dialog script.</em></p>");
                        parts.AppendLine("</article>");
                        continue;
                    }

                    foreach (var branch in dialog.Branches)
                    {
                        parts.AppendLine("<div class=\"sub\" id=\"" + EscAttr(branch.Anchor) + "\">");
                        parts.AppendLine("<h4>" + Esc(branch.Title) + "</h4>");
                        parts.AppendLine("<table><thead><tr><th>Line ID</th><th>Speaker</th><th>Text</th></tr></thead><tbody>");
                        foreach (var row in branch.Rows)
                        {
                            parts.AppendLine(
                                "<tr><td class=\"id\"><code>" + Esc(row.LineId) + "</code></td><td>" + Esc(row.Speaker) +
                                "</td><td class=\"msg\">" + Esc(row.Text) + "</td></tr>");
                        }
                        parts.AppendLine("</tbody></table></div>");
                    }

                    parts.AppendLine("</article>");
                }
            }

            parts.AppendLine("</section>");

            parts.AppendLine("<section class=\"block\" id=\"part-asc\">");
            parts.AppendLine("<h2>Part 2 — Scripted speech (*.asc)</h2>");

            if (scriptDs == 0 && scriptSay == 0)
            {
                parts.AppendLine("<p><em>No literal DisplaySpeech / player.Say lines found in root *.asc.</em></p>");
            }
            else if (document.AscSections != null)
            {
                foreach (var section in document.AscSections)
                {
                    parts.AppendLine("<article class=\"dlg\" id=\"" + EscAttr(section.Anchor) + "\">");
                    if (!string.IsNullOrEmpty(section.Error))
                    {
                        parts.AppendLine("<h3>" + Esc(section.Filename) + "</h3>");
                        parts.AppendLine("<p><em>Read error: " + Esc(section.Error) + "</em></p>");
                        parts.AppendLine("</article>");
                        continue;
                    }

                    parts.AppendLine("<h3><code>" + Esc(section.Filename) + "</code></h3>");
                    parts.AppendLine(
                        "<table><thead><tr><th>Line ID</th><th>Speaker</th><th>Text</th><th>Notes</th></tr></thead><tbody>");
                    foreach (var row in section.Rows)
                    {
                        parts.AppendLine(
                            "<tr><td class=\"id\"><code>" + Esc(row.LineId) + "</code></td><td>" + Esc(row.Speaker) +
                            "</td><td class=\"msg\">" + Esc(row.Text) + "</td><td>" + Esc(row.Notes) + "</td></tr>");
                    }
                    parts.AppendLine("</tbody></table></article>");
                }
            }

            parts.AppendLine("</section>");
            parts.AppendLine("</main>");
        }

        private static string Esc(string value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }

        private static string EscAttr(string value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }

        private static string GetCss()
        {
            return @":root { --bg: #f6f7f9; --fg: #1a1d26; --muted: #5c6575; --border: #d8dde6;
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
}";
        }
    }
}
