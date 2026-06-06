using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace AGS.Plugin.GameStates.Core
{
    internal static class StateCatalogHtmlExporter
    {
        private static readonly Regex SymbolPattern = new Regex(
            @"\b(INV_\w+|DIALOG_\w+|ROOM\d+_\w+|GI_\w+)\b",
            RegexOptions.Compiled);

        public static string FormatHtml(StateCatalogDocument document)
        {
            var parts = new StringBuilder();
            parts.AppendLine("<!DOCTYPE html>");
            parts.AppendLine("<html lang=\"en\">");
            parts.AppendLine("<head>");
            parts.AppendLine("<meta charset=\"utf-8\">");
            parts.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
            parts.AppendLine("<title>Game state catalog</title>");
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
            parts.AppendLine("<h1>Game state catalog</h1>");
            parts.AppendLine("<p class=\"meta\">GlobalInt flags and cross-references for AGS scripts</p>");
            AppendSummary(parts, document);
            AppendStory(parts, document);
            AppendDoors(parts, document);
            AppendByRoom(parts, document);
            AppendReset(parts, document);
            AppendDetails(parts, document);
            AppendWarnings(parts, document);
            parts.AppendLine("</div>");
            parts.AppendLine("</body>");
            parts.AppendLine("</html>");
            return parts.ToString();
        }

        private static void AppendSummary(StringBuilder parts, StateCatalogDocument document)
        {
            var summary = document.Summary ?? new StateCatalogSummary();
            parts.AppendLine("<section id=\"summary\">");
            parts.AppendLine("<h2>Summary</h2>");
            parts.AppendLine("<ul>");
            parts.AppendLine("<li>Generated: " + Esc(document.GeneratedAt) + "</li>");
            parts.AppendLine("<li>Script files scanned: " + document.FilesScanned + "</li>");
            parts.AppendLine("<li>Story flags: " + summary.StoryFlags + "</li>");
            parts.AppendLine("<li>Door slots: " + summary.DoorSlots + "</li>");
            parts.AppendLine("<li>Engine slots: " + summary.EngineSlots + "</li>");
            parts.AppendLine("<li>Warnings: " + summary.Warnings + "</li>");
            parts.AppendLine("</ul>");
            parts.AppendLine("<p class=\"note\">Door convention: 0 = closed, 1 = open, 2 = locked (see <code>core_doors</code>).</p>");
            parts.AppendLine("</section>");
        }

        private static void AppendStory(StringBuilder parts, StateCatalogDocument document)
        {
            var story = document.Story ?? new List<FlagEntry>();
            parts.AppendLine("<section id=\"story\">");
            parts.AppendLine("<h2>Story flags <span class=\"count\">(" + story.Count + ")</span></h2>");
            parts.AppendLine("<table><thead><tr><th>Title</th><th>Symbol</th><th>Slot</th><th>Description</th><th>Initial</th><th>Sets</th><th>Gets</th><th>Rooms</th></tr></thead><tbody>");
            parts.AppendLine(FlagTableRows(story));
            parts.AppendLine("</tbody></table></section>");
        }

        private static void AppendDoors(StringBuilder parts, StateCatalogDocument document)
        {
            var doors = document.Doors ?? new List<FlagEntry>();
            parts.AppendLine("<section id=\"doors\">");
            parts.AppendLine("<h2>Door slots <span class=\"count\">(" + doors.Count + ")</span></h2>");
            parts.AppendLine("<pre class=\"door-map\">");
            if (doors.Count == 0)
            {
                parts.AppendLine(Esc("  (none documented)"));
            }
            else
            {
                foreach (var door in doors)
                {
                    var note = door.RegistryNote ?? door.Comment ?? string.Empty;
                    parts.AppendLine(Esc(string.Format("  {0,3}  {1}", door.Slot, note)));
                }
            }

            parts.AppendLine("</pre>");
            parts.AppendLine("<table><thead><tr><th>Slot</th><th>Description</th><th>Initial</th><th>Sets</th><th>Gets</th></tr></thead><tbody>");
            if (doors.Count == 0)
            {
                parts.AppendLine("<tr><td colspan=\"5\">None</td></tr>");
            }
            else
            {
                foreach (var door in doors)
                {
                    var desc = door.RegistryNote ?? door.Comment ?? string.Empty;
                    var init = door.InitialValue ?? "—";
                    var slotCell = "<a href=\"#" + Esc(door.Anchor) + "\">" + door.Slot + "</a>";
                    parts.AppendLine("<tr><td>" + slotCell + "</td><td>" + Esc(desc) + "</td><td>" + Esc(init) +
                                     "</td><td>" + door.SetCount + "</td><td>" + door.GetCount + "</td></tr>");
                }
            }

            parts.AppendLine("</tbody></table></section>");
        }

        private static void AppendByRoom(StringBuilder parts, StateCatalogDocument document)
        {
            parts.AppendLine("<section id=\"by-room\"><h2>By room</h2>");
            var byRoom = document.ByRoom ?? new Dictionary<string, List<FlagEntry>>();
            var flagsBySym = new Dictionary<string, FlagEntry>();
            if (document.Flags != null)
            {
                foreach (var flag in document.Flags.Values)
                {
                    var key = flag.Symbol ?? ("slot_" + flag.Slot);
                    flagsBySym[key] = flag;
                }
            }

            foreach (var pair in SortRooms(byRoom))
            {
                if (pair.Value == null || pair.Value.Count == 0)
                {
                    continue;
                }

                parts.AppendLine("<h3>" + Esc(RoomLabel(pair.Key, document.RoomTitles)) + "</h3><ul>");
                foreach (var flag in pair.Value)
                {
                    var sym = flag.Symbol ?? ("slot_" + flag.Slot);
                    parts.AppendLine("<li><a href=\"#" + Esc(flag.Anchor) + "\">" + Esc(flag.FriendlyTitle) +
                                     "</a> (<code>" + Esc(sym) + "</code>)</li>");
                }

                parts.AppendLine("</ul>");
            }

            parts.AppendLine("</section>");
        }

        private static void AppendReset(StringBuilder parts, StateCatalogDocument document)
        {
            var reset = document.ResetList ?? new List<FlagEntry>();
            var note = document.Profile != null ? document.Profile.ResetNoteHtml : "Values set in <code>game_start</code>.";
            parts.AppendLine("<section id=\"reset\">");
            parts.AppendLine("<h2>New game reset checklist <span class=\"count\">(" + reset.Count + ")</span></h2>");
            parts.AppendLine("<p class=\"note\">" + note + "</p>");
            parts.AppendLine("<table><thead><tr><th>Title</th><th>Symbol</th><th>Slot</th><th>Initial</th><th>Source</th></tr></thead><tbody>");
            if (reset.Count == 0)
            {
                parts.AppendLine("<tr><td colspan=\"5\">None</td></tr>");
            }
            else
            {
                foreach (var flag in reset)
                {
                    parts.AppendLine("<tr><td>" + Esc(flag.FriendlyTitle) + "</td><td><code>" + Esc(flag.Symbol ?? "—") +
                                     "</code></td><td>" + flag.Slot + "</td><td>" + Esc(flag.InitialValue ?? string.Empty) +
                                     "</td><td>" + Esc(flag.InitialSource ?? string.Empty) + "</td></tr>");
                }
            }

            parts.AppendLine("</tbody></table></section>");
        }

        private static void AppendDetails(StringBuilder parts, StateCatalogDocument document)
        {
            parts.AppendLine("<section id=\"details\"><h2>Flag details</h2>");
            var all = new List<FlagEntry>();
            if (document.Story != null) all.AddRange(document.Story);
            if (document.Doors != null) all.AddRange(document.Doors);
            if (document.Engine != null) all.AddRange(document.Engine);
            if (document.Other != null) all.AddRange(document.Other);
            all.Sort((a, b) =>
            {
                var cmp = string.CompareOrdinal(a.Symbol ?? string.Empty, b.Symbol ?? string.Empty);
                return cmp != 0 ? cmp : a.Slot.CompareTo(b.Slot);
            });

            var seen = new HashSet<int>();
            var related = document.RelatedDefines ?? new Dictionary<string, DefineInfo>();
            foreach (var flag in all)
            {
                if (seen.Contains(flag.Slot))
                {
                    continue;
                }

                seen.Add(flag.Slot);
                if (flag.SetCount + flag.GetCount == 0 && flag.InitialValue == null)
                {
                    continue;
                }

                var symLabel = flag.Symbol ?? ("slot " + flag.Slot);
                var desc = flag.Comment ?? flag.RegistryNote ?? string.Empty;
                parts.AppendLine("<div class=\"detail-card\" id=\"" + Esc(flag.Anchor) + "\">");
                parts.AppendLine("<h3>" + Esc(flag.FriendlyTitle) + "</h3>");
                parts.AppendLine("<p><code>" + Esc(symLabel) + "</code> · slot " + flag.Slot + "</p>");
                parts.AppendLine("<p>" + Esc(desc) + "</p>");
                if (flag.InitialValue != null)
                {
                    parts.AppendLine("<p>Initial on new game: <code>" + Esc(flag.InitialValue) + "</code> (" +
                                     Esc(flag.InitialSource ?? string.Empty) + ")</p>");
                }

                if (flag.Warnings != null && flag.Warnings.Count > 0)
                {
                    parts.AppendLine("<p class=\"warn\">Warnings: " + Esc(string.Join(", ", flag.Warnings.ToArray())) + "</p>");
                }

                parts.AppendLine("<p><strong>Set in</strong></p>");
                if (flag.Sets == null || flag.Sets.Count == 0)
                {
                    parts.AppendLine("<p class=\"note\">No set sites (excluding none).</p>");
                }
                else
                {
                    parts.AppendLine("<ul>");
                    foreach (var site in flag.Sets)
                    {
                        parts.AppendLine("<li><code>" + Esc(FormatRef(site, related)) + "</code></li>");
                    }

                    parts.AppendLine("</ul>");
                }

                parts.AppendLine("<p><strong>Read in</strong></p>");
                if (flag.Gets == null || flag.Gets.Count == 0)
                {
                    parts.AppendLine("<p class=\"note\">No read sites.</p>");
                }
                else
                {
                    parts.AppendLine("<ul>");
                    foreach (var site in flag.Gets)
                    {
                        parts.AppendLine("<li><code>" + Esc(FormatRef(site, related)) + "</code></li>");
                    }

                    parts.AppendLine("</ul>");
                }

                parts.AppendLine("</div>");
            }

            parts.AppendLine("</section>");
        }

        private static void AppendWarnings(StringBuilder parts, StateCatalogDocument document)
        {
            var warnings = document.CatalogWarnings ?? new List<CatalogWarning>();
            parts.AppendLine("<section id=\"warnings\">");
            parts.AppendLine("<h2>Warnings <span class=\"count\">(" + warnings.Count + ")</span></h2>");
            parts.AppendLine("<table><thead><tr><th>Kind</th><th>Symbol / arg</th><th>Slot / line</th><th>Detail</th></tr></thead><tbody>");
            if (warnings.Count == 0)
            {
                parts.AppendLine("<tr><td colspan=\"4\">None</td></tr>");
            }
            else
            {
                foreach (var warning in warnings)
                {
                    if (warning.Kind == "orphan_define")
                    {
                        parts.AppendLine("<tr><td>orphan_define</td><td><code>" + Esc(warning.Symbol) + "</code></td><td>" +
                                         warning.Slot + "</td><td>Defined in .ash but never referenced</td></tr>");
                    }
                    else if (warning.Kind == "magic_number")
                    {
                        parts.AppendLine("<tr><td>magic_number</td><td><code>" + Esc(warning.RawArg) + "</code></td><td>" +
                                         warning.Line + "</td><td>" + Esc(warning.File) + ": " + Esc(warning.Context) + "</td></tr>");
                    }
                    else
                    {
                        parts.AppendLine("<tr><td>" + Esc(warning.Kind) + "</td><td><code>" + Esc(warning.Symbol) +
                                         "</code></td><td>" + warning.Slot + "</td><td>" + Esc(warning.Detail ?? string.Empty) + "</td></tr>");
                    }
                }
            }

            parts.AppendLine("</tbody></table></section>");
        }

        private static string FlagTableRows(List<FlagEntry> flags)
        {
            if (flags == null || flags.Count == 0)
            {
                return "<tr><td colspan=\"8\">None</td></tr>";
            }

            var rows = new StringBuilder();
            foreach (var flag in flags)
            {
                var sym = flag.Symbol ?? "—";
                var link = "<a href=\"#" + Esc(flag.Anchor) + "\">" + Esc(flag.FriendlyTitle) + "</a>";
                var comment = flag.Comment ?? flag.RegistryNote ?? string.Empty;
                var init = flag.InitialValue ?? "—";
                rows.AppendLine("<tr><td>" + link + "</td><td><code>" + Esc(sym) + "</code></td><td>" + flag.Slot +
                                "</td><td>" + Esc(comment) + "</td><td>" + Esc(init) + "</td><td>" + flag.SetCount +
                                "</td><td>" + flag.GetCount + "</td><td>" + Esc(string.Join(", ", flag.Rooms.ToArray())) + "</td></tr>");
            }

            return rows.ToString();
        }

        private static string FormatRef(RefSite site, Dictionary<string, DefineInfo> related)
        {
            var loc = site.File + ":" + site.Line + " " + site.Function;
            var extras = new List<string>();
            foreach (Match match in SymbolPattern.Matches(site.Context ?? string.Empty))
            {
                var sym = match.Value;
                if ((related != null && related.ContainsKey(sym)) || sym.StartsWith("GI_"))
                {
                    extras.Add(sym);
                }
            }

            var extra = extras.Count > 0 ? " (" + string.Join(", ", extras.ToArray()) + ")" : string.Empty;
            return loc + " — " + (site.Context ?? string.Empty) + extra;
        }

        private static string RoomLabel(string room, Dictionary<string, string> roomTitles)
        {
            if (IsAllDigits(room))
            {
                string title;
                if (roomTitles != null && roomTitles.TryGetValue(room, out title) && !string.IsNullOrEmpty(title))
                {
                    return room + " — " + title;
                }
            }

            return room;
        }

        private static IEnumerable<KeyValuePair<string, List<FlagEntry>>> SortRooms(
            Dictionary<string, List<FlagEntry>> byRoom)
        {
            var keys = new List<string>(byRoom.Keys);
            keys.Sort((a, b) =>
            {
                int ai;
                int bi;
                var aNum = int.TryParse(a, out ai);
                var bNum = int.TryParse(b, out bi);
                if (aNum && bNum) return ai.CompareTo(bi);
                if (aNum) return -1;
                if (bNum) return 1;
                return string.CompareOrdinal(a, b);
            });

            foreach (var key in keys)
            {
                yield return new KeyValuePair<string, List<FlagEntry>>(key, byRoom[key]);
            }
        }

        private static bool IsAllDigits(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            foreach (var ch in value)
            {
                if (ch < '0' || ch > '9')
                {
                    return false;
                }
            }

            return true;
        }

        private static string Esc(string value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }

        private static string GetNav()
        {
            return @"<nav class=""nav"">
  <a href=""#summary"">Summary</a>
  <a href=""#story"">Story flags</a>
  <a href=""#doors"">Doors</a>
  <a href=""#by-room"">By room</a>
  <a href=""#reset"">New game reset</a>
  <a href=""#details"">Flag details</a>
  <a href=""#warnings"">Warnings</a>
  <label class=""search"" for=""catalog-search"">Filter:</label>
  <input id=""catalog-search"" type=""search"" placeholder=""Type to filter tables"" autocomplete=""off"">
</nav>";
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
.search { margin-left: auto; font-size: 0.88rem; color: #333; align-self: center; white-space: nowrap; }
#catalog-search { min-width: 10rem; max-width: 14rem; width: 14rem; padding: 0.25rem 0.4rem; border: 1px solid #bbb; border-radius: 4px; font: inherit; }
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
pre.door-map { background: #fff; border: 1px solid #ddd; padding: 0.75rem; font-size: 0.85rem; overflow-x: auto; }";
        }

        private static string GetJs()
        {
            return @"
document.addEventListener(""DOMContentLoaded"", function () {
  const input = document.getElementById(""catalog-search"");
  if (!input) return;
  const tables = Array.from(document.querySelectorAll(""section table""));
  function normalizeTokens(text) {
    return (text || """").toLowerCase().trim().split(/\s+/).filter(Boolean);
  }
  function rowMatches(row, tokens) {
    if (tokens.length === 0) return true;
    const haystack = (row.textContent || """").toLowerCase();
    return tokens.every((token) => haystack.includes(token));
  }
  function applyFilter() {
    const tokens = normalizeTokens(input.value);
    for (const table of tables) {
      for (const row of Array.from(table.querySelectorAll(""tbody tr""))) {
        row.style.display = rowMatches(row, tokens) ? """" : ""none"";
      }
    }
  }
  input.addEventListener(""input"", applyFilter);
});";
        }
    }
}
