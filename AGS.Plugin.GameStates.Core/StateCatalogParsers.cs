using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AGS.Plugin.GameStates.Core
{
    internal static class StateCatalogParsers
    {
        private static readonly Regex DefinePattern = new Regex(
            @"^\s*#define\s+(\w+)\s+(\d+)\s*(?://(.*))?\s*$",
            RegexOptions.Compiled);

        private static readonly Regex SlotLinePattern = new Regex(
            @"^\s*//\s*(\d{3})(?:-(\d{3}))?\s*-\s*(.+?)\s*$",
            RegexOptions.Compiled);

        private static readonly Regex GiCallPattern = new Regex(
            @"\b(Get|Set)GlobalInt\s*\(\s*([^,)]+?)\s*(?:,\s*([^)]+?))?\s*\)",
            RegexOptions.Compiled);

        private static readonly Regex SectionPattern = new Regex(
            @"^#sectionstart\s+(\w+)",
            RegexOptions.Compiled);

        private static readonly Regex FunctionPattern = new Regex(
            @"^function\s+(\w+)\s*\(",
            RegexOptions.Compiled);

        private static readonly Regex IntegerPattern = new Regex(
            @"^-?\d+$",
            RegexOptions.Compiled);

        private static readonly Regex IdentifierPattern = new Regex(
            @"^[A-Za-z_][A-Za-z0-9_]*$",
            RegexOptions.Compiled);

        public static Dictionary<string, DefineInfo> ParseDefines(IEnumerable<KeyValuePair<string, string>> ashFiles)
        {
            var result = new Dictionary<string, DefineInfo>(StringComparer.Ordinal);
            foreach (var pair in ashFiles)
            {
                ParseDefinesFromText(pair.Key, pair.Value, result);
            }

            return result;
        }

        private static void ParseDefinesFromText(
            string fileName,
            string text,
            Dictionary<string, DefineInfo> result)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var pendingComment = string.Empty;
            foreach (var rawLine in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                var line = rawLine;
                var stripped = line.Trim();
                if (stripped.StartsWith("//", StringComparison.Ordinal) &&
                    !stripped.StartsWith("///", StringComparison.Ordinal))
                {
                    var body = stripped.Substring(2).Trim();
                    if (!string.IsNullOrEmpty(body) && !body.StartsWith("#define", StringComparison.Ordinal))
                    {
                        pendingComment = body;
                    }

                    continue;
                }

                var match = DefinePattern.Match(line);
                if (!match.Success)
                {
                    pendingComment = string.Empty;
                    continue;
                }

                var name = match.Groups[1].Value;
                var slot = int.Parse(match.Groups[2].Value);
                var inlineComment = match.Groups[3].Success ? match.Groups[3].Value.Trim() : string.Empty;
                var comment = !string.IsNullOrEmpty(inlineComment) ? inlineComment : pendingComment;
                pendingComment = string.Empty;

                var prefix = "other";
                if (name.StartsWith("GI_IRS_", StringComparison.Ordinal) || name.StartsWith("GI_", StringComparison.Ordinal))
                {
                    prefix = "gi";
                }
                else if (name.StartsWith("INV_", StringComparison.Ordinal))
                {
                    prefix = "inv";
                }
                else if (name.StartsWith("DIALOG_", StringComparison.Ordinal))
                {
                    prefix = "dialog";
                }
                else if (name.StartsWith("ROOM", StringComparison.Ordinal))
                {
                    prefix = "room";
                }

                result[name] = new DefineInfo
                {
                    Slot = slot,
                    Comment = comment,
                    Prefix = prefix,
                    File = fileName,
                };
            }
        }

        public static Dictionary<int, string> ParseSlotRegistry(string globalScriptText)
        {
            var slots = new Dictionary<int, string>();
            if (string.IsNullOrEmpty(globalScriptText))
            {
                return slots;
            }

            var inBlock = false;
            foreach (var rawLine in globalScriptText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                if (rawLine.IndexOf("RESERVED GLOBALINT SLOTS", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    inBlock = true;
                    continue;
                }

                if (!inBlock)
                {
                    continue;
                }

                if (rawLine.TrimStart().StartsWith("////", StringComparison.Ordinal))
                {
                    continue;
                }

                var match = SlotLinePattern.Match(rawLine);
                if (!match.Success)
                {
                    var trimmed = rawLine.Trim();
                    if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("//", StringComparison.Ordinal))
                    {
                        break;
                    }

                    continue;
                }

                var start = int.Parse(match.Groups[1].Value);
                var end = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : start;
                var desc = match.Groups[3].Value.Trim();
                for (var slot = start; slot <= end; slot++)
                {
                    slots[slot] = desc;
                }
            }

            return slots;
        }

        public static string ExtractFunctionBody(string text, string functionName)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(functionName))
            {
                return string.Empty;
            }

            var pattern = new Regex(
                "^function\\s+" + Regex.Escape(functionName) + "\\s*\\(\\)\\s*\\{",
                RegexOptions.Multiline | RegexOptions.Compiled);
            var match = pattern.Match(text);
            if (!match.Success)
            {
                return string.Empty;
            }

            var start = match.Index + match.Length;
            var depth = 1;
            var i = start;
            while (i < text.Length && depth > 0)
            {
                var ch = text[i];
                if (ch == '{')
                {
                    depth++;
                }
                else if (ch == '}')
                {
                    depth--;
                }

                i++;
            }

            return text.Substring(start, Math.Max(0, i - start - 1));
        }

        public static GiResolveResult ResolveGiArg(string raw, Dictionary<string, DefineInfo> defines)
        {
            var arg = (raw ?? string.Empty).Trim();
            if (IntegerPattern.IsMatch(arg))
            {
                return new GiResolveResult(int.Parse(arg), arg, null);
            }

            DefineInfo defineInfo;
            if (defines != null && defines.TryGetValue(arg, out defineInfo))
            {
                return new GiResolveResult(defineInfo.Slot, arg, null);
            }

            if (IdentifierPattern.IsMatch(arg))
            {
                return new GiResolveResult(null, arg, "dynamic_arg");
            }

            return new GiResolveResult(null, arg, "unresolved");
        }

        public static ScanResult ScanGiUsage(
            IEnumerable<KeyValuePair<string, string>> ascFiles,
            Dictionary<string, DefineInfo> defines,
            IStateCatalogProfile profile)
        {
            var sites = new List<RefSite>();
            var magicWarnings = new List<CatalogWarning>();
            var sorted = new List<KeyValuePair<string, string>>();
            foreach (var pair in ascFiles)
            {
                sorted.Add(pair);
            }

            sorted.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));

            foreach (var pair in sorted)
            {
                ScanFile(pair.Key, pair.Value, defines, profile, sites, magicWarnings);
            }

            return new ScanResult(sites, magicWarnings);
        }

        private static void ScanFile(
            string fileName,
            string text,
            Dictionary<string, DefineInfo> defines,
            IStateCatalogProfile profile,
            List<RefSite> sites,
            List<CatalogWarning> magicWarnings)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var currentFunc = string.Empty;
            var currentSection = string.Empty;
            var fileRoom = profile.RoomFromFile(fileName);

            for (var lineNo = 0; lineNo < lines.Length; lineNo++)
            {
                var line = lines[lineNo];
                var stripped = line.TrimStart();
                if (stripped.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                var sectionMatch = SectionPattern.Match(stripped);
                if (sectionMatch.Success)
                {
                    currentSection = sectionMatch.Groups[1].Value;
                    continue;
                }

                var functionMatch = FunctionPattern.Match(stripped);
                if (functionMatch.Success)
                {
                    currentFunc = functionMatch.Groups[1].Value;
                    continue;
                }

                var funcLabel = !string.IsNullOrEmpty(currentSection)
                    ? currentSection
                    : (!string.IsNullOrEmpty(currentFunc) ? currentFunc : "(top)");

                foreach (Match match in GiCallPattern.Matches(line))
                {
                    var opRaw = match.Groups[1].Value;
                    var argRaw = match.Groups[2].Value;
                    var valRaw = match.Groups[3].Success ? match.Groups[3].Value : null;
                    var op = opRaw == "Get" ? "get" : "set";
                    var resolved = ResolveGiArg(argRaw, defines);
                    if (resolved.Warning == "dynamic_arg")
                    {
                        continue;
                    }

                    if (!resolved.Slot.HasValue)
                    {
                        magicWarnings.Add(new CatalogWarning
                        {
                            Kind = "magic_number",
                            RawArg = argRaw.Trim(),
                            Line = lineNo + 1,
                            File = fileName,
                            Context = stripped.Trim(),
                        });
                        continue;
                    }

                    sites.Add(new RefSite
                    {
                        File = fileName,
                        Line = lineNo + 1,
                        Op = op,
                        Function = funcLabel,
                        Room = fileRoom,
                        Value = valRaw != null ? valRaw.Trim() : null,
                        Context = stripped.Trim(),
                        RawArg = argRaw.Trim(),
                    });
                }
            }
        }

        public static Dictionary<int, KeyValuePair<string, string>> ParseInitialValues(
            Dictionary<string, string> scriptTexts,
            Dictionary<string, DefineInfo> defines,
            IStateCatalogProfile profile)
        {
            var inits = new Dictionary<int, KeyValuePair<string, string>>();
            foreach (var target in profile.InitScanTargets)
            {
                string text;
                if (!TryGetScriptText(scriptTexts, target.ScriptFileName, out text))
                {
                    continue;
                }

                var body = ExtractFunctionBody(text, target.FunctionName);
                foreach (var rawLine in body.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
                {
                    var stripped = rawLine.Trim();
                    if (stripped.StartsWith("//", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    foreach (Match match in GiCallPattern.Matches(stripped))
                    {
                        if (match.Groups[1].Value != "Set")
                        {
                            continue;
                        }

                        var resolved = ResolveGiArg(match.Groups[2].Value, defines);
                        if (!resolved.Slot.HasValue || resolved.Warning != null)
                        {
                            continue;
                        }

                        var val = match.Groups[3].Success ? match.Groups[3].Value.Trim() : string.Empty;
                        inits[resolved.Slot.Value] = new KeyValuePair<string, string>(val, target.FunctionName);
                    }
                }
            }

            return inits;
        }

        public static bool TryGetScriptText(
            Dictionary<string, string> scriptTexts,
            string fileName,
            out string text)
        {
            text = null;
            if (scriptTexts == null || string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            if (scriptTexts.TryGetValue(fileName, out text))
            {
                return !string.IsNullOrEmpty(text);
            }

            var altName = fileName.EndsWith(".asc", StringComparison.OrdinalIgnoreCase)
                ? fileName.Substring(0, fileName.Length - 4) + ".ash"
                : fileName.EndsWith(".ash", StringComparison.OrdinalIgnoreCase)
                    ? fileName.Substring(0, fileName.Length - 4) + ".asc"
                    : null;

            if (altName != null && scriptTexts.TryGetValue(altName, out text))
            {
                return !string.IsNullOrEmpty(text);
            }

            return false;
        }

        internal sealed class GiResolveResult
        {
            public GiResolveResult(int? slot, string label, string warning)
            {
                Slot = slot;
                Label = label;
                Warning = warning;
            }

            public int? Slot { get; private set; }
            public string Label { get; private set; }
            public string Warning { get; private set; }
        }

        internal sealed class ScanResult
        {
            public ScanResult(List<RefSite> sites, List<CatalogWarning> magicWarnings)
            {
                Sites = sites;
                MagicWarnings = magicWarnings;
            }

            public List<RefSite> Sites { get; private set; }
            public List<CatalogWarning> MagicWarnings { get; private set; }
        }
    }
}
