using System.Collections.Generic;
using System.Net;
using System.Text;

namespace AGS.Plugin.EntityCatalog
{
    internal static class EntityCatalogHtmlExporter
    {
        public static string FormatHtml(CatalogDocument document)
        {
            var title = "AGF entity catalog — Game.agf";
            var h1Extra = string.IsNullOrEmpty(document.GameTitle)
                ? string.Empty
                : " — " + Esc(document.GameTitle);

            var parts = new StringBuilder();
            parts.AppendLine("<!DOCTYPE html>");
            parts.AppendLine("<html lang=\"en\">");
            parts.AppendLine("<head>");
            parts.AppendLine("<meta charset=\"utf-8\">");
            parts.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
            parts.AppendLine("<title>" + Esc(title) + "</title>");
            parts.AppendLine("<style>");
            parts.AppendLine(GetCss());
            parts.AppendLine("</style>");
            parts.AppendLine("<script>");
            parts.AppendLine(GetJs());
            parts.AppendLine("</script>");
            parts.AppendLine("</head>");
            parts.AppendLine("<body>");
            parts.AppendLine("<div class=\"wrap\">");
            parts.AppendLine(GetNav());
            parts.AppendLine("<h1>" + Esc(title) + h1Extra + "</h1>");
            parts.AppendLine("<p class=\"meta\">Source: " + Esc(document.SourceLabel ?? string.Empty) + "</p>");

            if (document.Sections != null)
            {
                foreach (var section in document.Sections)
                {
                    parts.AppendLine(RenderSection(section));
                }
            }

            parts.AppendLine("</div>");
            parts.AppendLine("</body>");
            parts.AppendLine("</html>");
            return parts.ToString();
        }

        private static string GetNav()
        {
            return @"<nav class=""nav"">
  <a href=""#rooms"">Rooms</a>
  <a href=""#inventory"">Inventory</a>
  <a href=""#dialogs"">Dialogs</a>
  <a href=""#characters"">Characters</a>
  <a href=""#views"">Views</a>
  <a href=""#audio"">Audio</a>
  <a href=""#guis"">GUIs</a>
  <a href=""#fonts"">Fonts</a>
  <a href=""#cursors"">Mouse cursors</a>
  <label class=""search"" for=""catalog-search"">Filter:</label>
  <input id=""catalog-search"" type=""search"" placeholder=""Type to filter all sections"" autocomplete=""off"">
</nav>";
        }

        private static string RenderSection(CatalogSection section)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<section id=\"" + EscAttr(section.Id) + "\">");
            sb.AppendLine("<h2>" + Esc(section.Title) + " <span class=\"count\">(" + section.Count + ")</span></h2>");
            if (!string.IsNullOrEmpty(section.Note))
            {
                sb.AppendLine("<p class=\"note\">" + EscNote(section.Note) + "</p>");
            }

            sb.AppendLine("<table>");
            sb.AppendLine("<thead><tr>");
            foreach (var header in section.ColumnHeaders ?? new string[0])
            {
                sb.AppendLine("<th>" + Esc(header) + "</th>");
            }
            sb.AppendLine("</tr></thead>");
            sb.AppendLine("<tbody>");

            if (section.Rows == null || section.Rows.Count == 0)
            {
                var colspan = section.ColumnHeaders == null || section.ColumnHeaders.Length == 0
                    ? 1
                    : section.ColumnHeaders.Length;
                sb.AppendLine("<tr><td colspan=\"" + colspan + "\">No entries found.</td></tr>");
            }
            else
            {
                foreach (var row in section.Rows)
                {
                    sb.Append("<tr>");
                    var cells = row.Cells ?? new string[0];
                    for (var i = 0; i < (section.ColumnHeaders == null ? cells.Length : section.ColumnHeaders.Length); i++)
                    {
                        var value = i < cells.Length ? cells[i] : string.Empty;
                        if (i == 0 && !string.IsNullOrEmpty(value) && char.IsDigit(value[0]))
                        {
                            sb.Append("<td>").Append(Esc(value)).Append("</td>");
                        }
                        else
                        {
                            sb.Append(Cell(value));
                        }
                    }
                    sb.AppendLine("</tr>");
                }
            }

            sb.AppendLine("</tbody>");
            sb.AppendLine("</table>");
            sb.AppendLine("</section>");
            return sb.ToString();
        }

        private static string Cell(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "<td>—</td>";
            }

            return "<td>" + Esc(value) + "</td>";
        }

        private static string EscNote(string note)
        {
            return note
                .Replace("Game/Rooms", "<code>Game/Rooms</code>")
                .Replace("UnloadedRoom", "<code>UnloadedRoom</code>")
                .Replace("Game/AudioClips", "<code>Game/AudioClips</code>")
                .Replace("GUIMain", "<code>GUIMain</code>")
                .Replace("NormalGUI", "<code>NormalGUI</code>")
                .Replace("Game/GUIs", "<code>Game/GUIs</code>");
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
            return @"
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
code { font-size: 0.9em; }";
        }

        private static string GetJs()
        {
            return @"document.addEventListener(""DOMContentLoaded"", function () {
  const input = document.getElementById(""catalog-search"");
  if (!input) return;

  const tables = Array.from(document.querySelectorAll(""section table""));

  function normalizeTokens(text) {
    return (text || """")
      .toLowerCase()
      .trim()
      .split(/\s+/)
      .filter(Boolean);
  }

  function rowMatches(row, tokens) {
    if (tokens.length === 0) return true;
    const haystack = (row.textContent || """").toLowerCase();
    return tokens.every((token) => haystack.includes(token));
  }

  function applyFilter() {
    const tokens = normalizeTokens(input.value);
    for (const table of tables) {
      const rows = Array.from(table.querySelectorAll(""tbody tr""));
      for (const row of rows) {
        const show = rowMatches(row, tokens);
        row.style.display = show ? """" : ""none"";
      }
    }
  }

  input.addEventListener(""input"", applyFilter);
});";
        }
    }
}
